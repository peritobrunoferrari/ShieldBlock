using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;
using ShieldBlock.Core;
using ShieldBlock.Export;
using ShieldBlock.Icp;
using ShieldBlock.Utils;

namespace ShieldBlock.UI
{
    public class MainForm : Form
    {
        // ===== ESTADO =====
        private readonly ShieldBlockEngine    _engine;
        private readonly DriveDetector        _drives;
        private readonly PartitionAnalyzer    _parts;
        private readonly LogSession           _session;
        private readonly ExportManager        _exporter = new ExportManager();
        private readonly AutoLogger           _autoLogger;
        private readonly MachineFingerprint.Snapshot _fingerprint;
        private readonly UsbWatcher           _usbWatcher;
        private readonly RegistryTamperGuard  _tamperGuard;
        private readonly FileSystemMonitor    _fsMonitor;
        private readonly ProcessAuditor       _procAuditor;
        private readonly HeartbeatService     _heartbeat;
        private readonly ForensicToolWatcher  _forensicWatcher;
        private IcpBrasilConfig              _icpConfig;
        private string                        _icpConfigPath;
        private Label                         _icpBadge;
        private readonly string               _logsDir;
        private readonly DateTime             _sessionStart;
        private PeritoConfig                  _perito;
        private string                        _peritoPath;
        private Label                         _peritoInfoBox;
        private Timer                         _uiClock;

        // ===== UI =====
        private StatusCard _cardWriteBlock;
        private StatusCard _cardAutoMount;
        private StatusCard _cardEvents;
        private StatusCard _cardSession;
        private DataGridView _gridDrives;
        private DataGridView _gridDisks;
        private DataGridView _gridParts;
        private RichTextBox _logBox;
        private TextBox _filterBox;
        private Label _statusBar;
        private Label _chainHashLabel;
        private Label _sessionTimerLabel;
        private Label _integrityBadge;
        private readonly List<LogLineCache> _logCache = new List<LogLineCache>();

        private class LogLineCache
        {
            public string Category;
            public string Description;
            public string Timestamp;
            public int Seq;
        }

        public MainForm()
        {
            _peritoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                       "Config", "perito.json");
            _icpConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                       "Config", "icp.json");
            _icpConfig = IcpBrasilConfig.Load(_icpConfigPath);
            _perito = PeritoConfig.Load(_peritoPath);
            _logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

            _engine  = new ShieldBlockEngine();
            _drives  = new DriveDetector();
            _parts   = new PartitionAnalyzer();
            _session = new LogSession(_perito);
            _sessionStart = DateTime.Now;
            _fingerprint = MachineFingerprint.Capture();

            _autoLogger = new AutoLogger(_logsDir, _session, _fingerprint);
            _session.AutoLogger = _autoLogger;
            foreach (var ev in _session.Events)
                _autoLogger.WriteEvent(ev);

            try { SessionRegistry.AppendOpening(_logsDir, _session, _fingerprint); }
            catch (Exception ex) { _session.Append("INDEX_ERROR", ex.Message); }

            _usbWatcher  = new UsbWatcher();
            _tamperGuard = new RegistryTamperGuard();
            _fsMonitor   = new FileSystemMonitor();
            _procAuditor = new ProcessAuditor();
            _heartbeat   = new HeartbeatService(30000);
            _forensicWatcher = new ForensicToolWatcher();

            WireEvents();

            BuildForm();
            ApplyDarkTitleBarHint();
            RefreshAll();

            _usbWatcher.Start();
            _tamperGuard.Initialize();
            _fsMonitor.StartForAllDataDrives();
            _forensicWatcher.Start();
            _heartbeat.Start();
            StartUiClock();

            _session.Append("FINGERPRINT",
                "Hostname=" + _fingerprint.HostName
                + " | User=" + _fingerprint.UserName
                + " | SID=" + _fingerprint.UserSid
                + " | MachineGUID=" + _fingerprint.MachineGuid
                + " | exeHash=" + SafeShort(_fingerprint.ExecutableHash, 16));
            _session.Append("UI_READY",
                "Interface pronta. Auto-log: " + _autoLogger.FilePath);
        }

        private static string SafeShort(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n);
        }

        private void WireEvents()
        {
            _engine.OnAction += (cat, desc) =>
            {
                _session.Append(cat, desc);
                AppendLogLine(cat, desc);
                if (cat == "WRITE_BLOCK")
                    _tamperGuard.SetExpected(_engine.IsWriteBlocked(), null);
                else if (cat == "AUTOMOUNT")
                    _tamperGuard.SetExpected(null, !_engine.IsAutoMountEnabled());
                RefreshStatus();
            };

            Action<string, string> bus = (cat, desc) =>
            {
                if (InvokeRequired)
                {
                    try { BeginInvoke(new Action(() => { _session.Append(cat, desc); AppendLogLine(cat, desc); })); }
                    catch { }
                }
                else
                {
                    _session.Append(cat, desc);
                    AppendLogLine(cat, desc);
                }
            };
            _usbWatcher.OnEvent  += bus;
            _tamperGuard.OnEvent += bus;
            _fsMonitor.OnEvent   += bus;
            _procAuditor.OnEvent += bus;
            _forensicWatcher.OnEvent += bus;

            _heartbeat.OnTick += () =>
            {
                _session.Append("HEARTBEAT",
                    "Tick #" + _heartbeat.TickCount
                    + " | WriteBlock=" + (_engine.IsWriteBlocked() ? "ON" : "OFF")
                    + " | AutoMount=" + (_engine.IsAutoMountEnabled() ? "ON" : "OFF"));
                AppendLogLine("HEARTBEAT",
                    "Tick #" + _heartbeat.TickCount + " (status verificado)");
                _tamperGuard.Check();
                if (_heartbeat.TickCount % 4 == 0) _procAuditor.Snapshot();
                _forensicWatcher.TickScan();
                RefreshStatus();
            };
        }

        private void StartUiClock()
        {
            _uiClock = new Timer { Interval = 1000 };
            _uiClock.Tick += (s, e) => UpdateSessionTimer();
            _uiClock.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                _heartbeat.Stop();
                if (_uiClock != null) _uiClock.Stop();
                _session.Append("SESSION_END",
                    "Sessao encerrada (motivo: " + e.CloseReason + ")");
                try { SessionRegistry.AppendClosing(_logsDir, _session); } catch { }

                if (_autoLogger != null)
                {
                    _autoLogger.WriteFooter(_session);
                    _autoLogger.Dispose();
                }

                try { _usbWatcher.Dispose(); } catch { }
                try { _fsMonitor.Dispose(); } catch { }
                try { _heartbeat.Dispose(); } catch { }
                try { _forensicWatcher.Dispose(); } catch { }
                try { if (_uiClock != null) _uiClock.Dispose(); } catch { }
            }
            catch { }
            base.OnFormClosing(e);
        }

        // ============= UI BUILD =============
        private void BuildForm()
        {
TryLoadIcon();
            Text          = "ShieldBlock v6.5 - USB Write Blocker Forense + Auditoria de Tools";
            Width         = 1320;
            Height        = 860;
            MinimumSize   = new Size(1100, 720);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor     = Theme.Background;
            ForeColor     = Theme.TextPrimary;
            Font          = new Font(Theme.FontFamily, Theme.FontSizeBase);

            BuildHeader();
            BuildChainOfCustodyBanner();
            BuildStatusBar();
            BuildTabs();
        }


        private void TryLoadIcon()
        {
            try
            {
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Resources", "shield.ico");
                if (File.Exists(p))
                    this.Icon = new System.Drawing.Icon(p);
            }
            catch { }
        }

        private void BuildHeader()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top, Height = 70, BackColor = Theme.HeaderBar
            };

            // Logo SHIELD pequeno (lado esquerdo)
            var logo = new ShieldLogo
            {
                LogoColor = Theme.AccentBlue,
                ShowText = false,
                ShowSubtitle = false,
                Size = new Size(54, 54),
                Location = new Point(12, 8),
                BackColor = Theme.HeaderBar
            };
            header.Controls.Add(logo);

            // Brand + versao
            var brand = UiHelpers.MakeLabel("SHIELDBLOCK", 20f, true, Theme.AccentBlue);
            brand.Location = new Point(76, 8);
            header.Controls.Add(brand);

            var version = UiHelpers.MakeLabel(
                "v6.5  -  Forensic USB Write Blocker Auditavel",
                Theme.FontSizeBase, false, Theme.TextMuted);
            version.Location = new Point(80, 38);
            header.Controls.Add(version);

            // Credito do desenvolvedor (meio do header)
            var credit = UiHelpers.MakeLabel(
                "Desenvolvido por Bruno Ferrari  |  Shield Pericias Digitais",
                Theme.FontSizeSmall, false, Theme.TextSecondary);
            credit.Location = new Point(80, 54);
            header.Controls.Add(credit);

            // Botoes do canto direito
            var btnAbout = UiHelpers.MakeButton("SOBRE", Theme.AccentTeal);
            btnAbout.Width = 90; btnAbout.Height = 32;
            btnAbout.Click += (s, e) => ShowAboutDialog();

            var btnEditPerito = UiHelpers.MakeButton("EDITAR PERITO", Theme.AccentMauve);
            btnEditPerito.Width = 160; btnEditPerito.Height = 32;
            btnEditPerito.Click += (s, e) => OpenPeritoEditor();

            
            var btnIcp = UiHelpers.MakeButton("ICP-BRASIL", Theme.AccentBlue);
            btnIcp.Width = 130; btnIcp.Height = 32;
            btnIcp.Click += (s, e) => OpenIcpDialog();

            _icpBadge = UiHelpers.MakeLabel("", Theme.FontSizeSmall, true, Theme.AccentGreen);
            _icpBadge.AutoSize = true;
            UpdateIcpBadge();

            var adminBadge = UiHelpers.MakeLabel(
                IsAdmin() ? "* ADMIN" : "* NAO-ADMIN", Theme.FontSizeBase, true,
                IsAdmin() ? Theme.StatusActive : Theme.StatusInactive);
            adminBadge.AutoSize = true;

            header.Controls.Add(btnAbout);
            header.Controls.Add(btnEditPerito);
            header.Controls.Add(btnIcp);
            header.Controls.Add(_icpBadge);
            header.Controls.Add(adminBadge);

            EventHandler layoutRight = (s, e) =>
            {
                adminBadge.Top = 26;
                adminBadge.Left = header.Width - adminBadge.Width - 16;
                btnEditPerito.Top = 20;
                btnEditPerito.Left = adminBadge.Left - btnEditPerito.Width - 16;
                btnAbout.Top = 20;
                btnAbout.Left = btnEditPerito.Left - btnAbout.Width - 8;
                btnIcp.Top = 20;
                btnIcp.Left = btnAbout.Left - btnIcp.Width - 8;
                _icpBadge.Top = 4;
                _icpBadge.Left = btnIcp.Left;
            };
            header.Resize += layoutRight;
            header.HandleCreated += layoutRight;
            Controls.Add(header);
        }

        private void BuildChainOfCustodyBanner()
        {
            var banner = new Panel
            {
                Dock = DockStyle.Top, Height = 38, BackColor = Theme.AccentBlue
            };
            banner.Paint += (s, e) =>
            {
                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    banner.ClientRectangle, Theme.AccentBlue,
                    System.Drawing.Color.FromArgb(60, 80, 180),
                    System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
                {
                    e.Graphics.FillRectangle(brush, banner.ClientRectangle);
                }
            };

            var lockIcon = UiHelpers.MakeLabel("[CADEIA DE CUSTODIA]",
                Theme.FontSizeBase, true, Color.White);
            lockIcon.Location = new Point(16, 10);
            banner.Controls.Add(lockIcon);

            _integrityBadge = UiHelpers.MakeLabel("INTEGRIDADE OK",
                Theme.FontSizeBase, true, Color.White);
            _integrityBadge.Location = new Point(200, 10);
            banner.Controls.Add(_integrityBadge);

            _sessionTimerLabel = UiHelpers.MakeLabel("Tempo de sessao: 00:00:00",
                Theme.FontSizeBase, false, Color.White);
            _sessionTimerLabel.Location = new Point(360, 10);
            banner.Controls.Add(_sessionTimerLabel);

            _chainHashLabel = UiHelpers.MakeLabel("Hash atual: ----",
                Theme.FontSizeSmall, false, Color.FromArgb(220, 220, 230));
            _chainHashLabel.Location = new Point(580, 12);
            banner.Controls.Add(_chainHashLabel);

            EventHandler layout = (s, e) =>
            {
                _chainHashLabel.Left = 580;
                _chainHashLabel.Width = Math.Max(100, banner.Width - 600);
            };
            banner.Resize += layout;
            banner.HandleCreated += layout;

            Controls.Add(banner);
        }

        private void BuildStatusBar()
        {
            _statusBar = new Label
            {
                Dock = DockStyle.Bottom, Height = 26,
                BackColor = Theme.HeaderBar, ForeColor = Theme.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 16, 0),
                Font = new Font(Theme.FontFamily, Theme.FontSizeSmall),
                Text = "Pronto."
            };
            Controls.Add(_statusBar);
        }

        private void BuildTabs()
        {
            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font(Theme.FontFamily, Theme.FontSizeBase, FontStyle.Bold),
                Appearance = TabAppearance.FlatButtons,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(150, 34)
            };
            tabs.TabPages.Add(BuildDashboardTab());
            tabs.TabPages.Add(BuildPartitionsTab());
            tabs.TabPages.Add(BuildLogsTab());
            tabs.TabPages.Add(BuildHistoryTab());
            tabs.TabPages.Add(BuildExportTab());
            Controls.Add(tabs);
            tabs.BringToFront();
        }

        private TabPage BuildDashboardTab()
        {
            var tab = new TabPage("  DASHBOARD  ") { BackColor = Theme.Background };
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                Padding = new Padding(Theme.Padding), BackColor = Theme.Background
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var cardRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1,
                BackColor = Theme.Background
            };
            for (int i = 0; i < 4; i++)
                cardRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

            _cardWriteBlock = new StatusCard
            {
                Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0),
                Icon = "[#]", Title = "Write Block", Value = "-",
                Accent = Theme.AccentRed, FillBackground = true
            };
            _cardAutoMount = new StatusCard
            {
                Dock = DockStyle.Fill, Margin = new Padding(6, 0, 6, 0),
                Icon = "[*]", Title = "AutoMount", Value = "-",
                Accent = Theme.AccentBlue, FillBackground = true
            };
            _cardEvents = new StatusCard
            {
                Dock = DockStyle.Fill, Margin = new Padding(6, 0, 6, 0),
                Icon = "(*)", Title = "Eventos", Value = "0", Accent = Theme.AccentGreen
            };
            _cardSession = new StatusCard
            {
                Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0),
                Icon = "<>", Title = "Sessao", Value = _session.SessionId,
                Accent = Theme.AccentMauve
            };
            cardRow.Controls.Add(_cardWriteBlock, 0, 0);
            cardRow.Controls.Add(_cardAutoMount, 1, 0);
            cardRow.Controls.Add(_cardEvents, 2, 0);
            cardRow.Controls.Add(_cardSession, 3, 0);
            root.Controls.Add(cardRow, 0, 0);

            var btnRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = Theme.Background,
                Padding = new Padding(0, 12, 0, 0), WrapContents = false
            };
            var btnEnable  = UiHelpers.MakeButton("HABILITAR WRITE BLOCK", Theme.AccentRed);
            var btnDisable = UiHelpers.MakeButton("DESABILITAR WRITE BLOCK", Theme.AccentTeal);
            var btnAmOff   = UiHelpers.MakeButton("DESABILITAR AUTOMOUNT", Theme.AccentYellow);
            var btnAmOn    = UiHelpers.MakeButton("HABILITAR AUTOMOUNT", Theme.AccentBlue);
            var btnRefresh = UiHelpers.MakeButton("ATUALIZAR DRIVES", Theme.AccentMauve);
            var btnClearCache = UiHelpers.MakeButton("LIMPAR CACHE DE DRIVES", Theme.AccentYellow);

            btnEnable.Width = 220;  btnEnable.Margin  = new Padding(0, 0, 6, 0);
            btnDisable.Width = 220; btnDisable.Margin = new Padding(0, 0, 6, 0);
            btnAmOff.Width = 190;   btnAmOff.Margin   = new Padding(0, 0, 6, 0);
            btnAmOn.Width = 190;    btnAmOn.Margin    = new Padding(0, 0, 6, 0);
            btnRefresh.Width = 170; btnRefresh.Margin = new Padding(0, 0, 6, 0);
            btnClearCache.Width = 210;

            btnEnable.Click  += (s, e) => { _engine.EnableWriteBlock();  Notify("Write Block habilitado"); };
            btnDisable.Click += (s, e) => { _engine.DisableWriteBlock(); Notify("Write Block desabilitado"); };
            btnAmOff.Click   += (s, e) => { _engine.DisableAutoMount();  Notify("AutoMount desabilitado"); };
            btnAmOn.Click    += (s, e) => { _engine.EnableAutoMount();   Notify("AutoMount habilitado"); };
            btnRefresh.Click += (s, e) => { RefreshAll(); Notify("Drives atualizados"); };
            btnClearCache.Click += (s, e) => ClearMountedDevicesCache();

            btnRow.Controls.Add(btnEnable);
            btnRow.Controls.Add(btnDisable);
            btnRow.Controls.Add(btnAmOff);
            btnRow.Controls.Add(btnAmOn);
            btnRow.Controls.Add(btnRefresh);
            btnRow.Controls.Add(btnClearCache);
            root.Controls.Add(btnRow, 0, 1);

            _gridDrives = new DataGridView { Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
            UiHelpers.StyleGrid(_gridDrives);
            _gridDrives.Columns.Add("Letter", "Letra");
            _gridDrives.Columns.Add("Label", "Rotulo");
            _gridDrives.Columns.Add("FileSystem", "FS");
            _gridDrives.Columns.Add("DriveType", "Tipo");
            _gridDrives.Columns.Add("Total", "Total");
            _gridDrives.Columns.Add("Free", "Livre");
            _gridDrives.Columns.Add("Ready", "Status");
            root.Controls.Add(_gridDrives, 0, 2);

            tab.Controls.Add(root);
            return tab;
        }

        private TabPage BuildPartitionsTab()
        {
            var tab = new TabPage("  PARTICOES  ") { BackColor = Theme.Background };
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
                Padding = new Padding(Theme.Padding), BackColor = Theme.Background
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            root.Controls.Add(UiHelpers.MakeLabel("DISCOS FISICOS DETECTADOS",
                Theme.FontSizeMedium, true, Theme.AccentBlue), 0, 0);
            _gridDisks = new DataGridView { Dock = DockStyle.Fill };
            UiHelpers.StyleGrid(_gridDisks);
            _gridDisks.Columns.Add("DeviceID", "DeviceID");
            _gridDisks.Columns.Add("Model", "Modelo");
            _gridDisks.Columns.Add("Interface", "Interface");
            _gridDisks.Columns.Add("Media", "Midia");
            _gridDisks.Columns.Add("Serial", "Serial");
            _gridDisks.Columns.Add("Size", "Tamanho");
            _gridDisks.Columns.Add("Parts", "Parts");
            root.Controls.Add(_gridDisks, 0, 1);

            var lblParts = UiHelpers.MakeLabel("DETALHES DAS PARTICOES",
                Theme.FontSizeMedium, true, Theme.AccentBlue);
            lblParts.Margin = new Padding(0, 12, 0, 0);
            root.Controls.Add(lblParts, 0, 2);

            _gridParts = new DataGridView { Dock = DockStyle.Fill };
            UiHelpers.StyleGrid(_gridParts);
            _gridParts.Columns.Add("DiskIndex", "Disk");
            _gridParts.Columns.Add("Index", "Part");
            _gridParts.Columns.Add("Offset", "Offset");
            _gridParts.Columns.Add("Size", "Tamanho");
            _gridParts.Columns.Add("BlockSize", "Bloco");
            _gridParts.Columns.Add("Blocks", "Blocos");
            _gridParts.Columns.Add("Logical", "Volume");
            _gridParts.Columns.Add("FileSystem", "FS");
            _gridParts.Columns.Add("Serial", "Serial");
            _gridParts.Columns.Add("Boot", "Boot");
            root.Controls.Add(_gridParts, 0, 3);

            var refreshBtn = UiHelpers.MakeButton("ATUALIZAR DISCOS E PARTICOES", Theme.AccentMauve);
            refreshBtn.Width = 300;
            refreshBtn.Margin = new Padding(0, 12, 0, 0);
            refreshBtn.Click += (s, e) => { RefreshAll(); Notify("Particoes atualizadas"); };
            var btnPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            btnPanel.Controls.Add(refreshBtn);
            root.Controls.Add(btnPanel, 0, 4);

            tab.Controls.Add(root);
            return tab;
        }

        private TabPage BuildLogsTab()
        {
            var tab = new TabPage("  LOGS  ") { BackColor = Theme.Background };
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                Padding = new Padding(Theme.Padding), BackColor = Theme.Background
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = UiHelpers.MakeLabel(
                "AUDIT TRAIL ao vivo - Sessao " + _session.SessionId
                + "  | Auto-log: " + Path.GetFileName(_autoLogger.FilePath),
                Theme.FontSizeMedium, true, Theme.AccentGreen);
            root.Controls.Add(header, 0, 0);

            var filterPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            var lblFilter = UiHelpers.MakeLabel("Filtro: ",
                Theme.FontSizeBase, true, Theme.TextSecondary);
            lblFilter.Location = new Point(0, 8);

            _filterBox = new TextBox
            {
                BackColor = Theme.Surface, ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font(Theme.FontFamily, Theme.FontSizeBase),
                Location = new Point(60, 4), Width = 400
            };
            _filterBox.TextChanged += (s, e) => RebuildLogBox();

            var lblHint = UiHelpers.MakeLabel(
                "(digite texto ou categoria - vazio mostra tudo)",
                Theme.FontSizeSmall, false, Theme.TextMuted);
            lblHint.Location = new Point(470, 8);

            filterPanel.Controls.Add(lblFilter);
            filterPanel.Controls.Add(_filterBox);
            filterPanel.Controls.Add(lblHint);
            root.Controls.Add(filterPanel, 0, 1);

            _logBox = new RichTextBox
            {
                Dock = DockStyle.Fill, BackColor = Theme.TerminalBg,
                ForeColor = Theme.TerminalText,
                Font = new Font("Consolas", Theme.FontSizeBase),
                BorderStyle = BorderStyle.None, ReadOnly = true, WordWrap = false
            };
            root.Controls.Add(_logBox, 0, 2);

            tab.Controls.Add(root);
            return tab;
        }

        private TabPage BuildHistoryTab()
        {
            var tab = new TabPage("  HISTORICO  ") { BackColor = Theme.Background };
            var panel = new HistoryPanel(_logsDir);
            tab.Controls.Add(panel);
            return tab;
        }

        private TabPage BuildExportTab()
        {
            var tab = new TabPage("  EXPORTAR  ") { BackColor = Theme.Background };
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
                Padding = new Padding(24), BackColor = Theme.Background
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var hdr = UiHelpers.MakeLabel("GERAR RELATORIO PERICIAL",
                Theme.FontSizeTitle, true, Theme.AccentBlue);
            var sub = UiHelpers.MakeLabel(
                "Auto-log ja esta gravando em disco. Aqui voce gera relatorios formatados.",
                Theme.FontSizeBase, false, Theme.TextSecondary);
            var hdrPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            hdr.Location = new Point(0, 0);
            sub.Location = new Point(0, 32);
            hdrPanel.Controls.Add(hdr);
            hdrPanel.Controls.Add(sub);
            root.Controls.Add(hdrPanel, 0, 0);

            var btnRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = Theme.Background, WrapContents = false
            };
            var btnPdf  = UiHelpers.MakeButton("PDF (Laudo)", Theme.AccentRed);
            var btnHtml = UiHelpers.MakeButton("HTML", Theme.AccentBlue);
            var btnJson = UiHelpers.MakeButton("JSON", Theme.AccentGreen);
            var btnXml  = UiHelpers.MakeButton("XML DFRWS", Theme.AccentYellow);
            foreach (var b in new[] { btnPdf, btnHtml, btnJson, btnXml })
            {
                b.Width = 180; b.Margin = new Padding(0, 0, 12, 0);
            }
            btnPdf.Click  += (s, e) => DoExport(ExportManager.Format.PDF);
            btnHtml.Click += (s, e) => DoExport(ExportManager.Format.HTML);
            btnJson.Click += (s, e) => DoExport(ExportManager.Format.JSON);
            btnXml.Click  += (s, e) => DoExport(ExportManager.Format.XML_DFRWS);
            btnRow.Controls.Add(btnPdf);
            btnRow.Controls.Add(btnHtml);
            btnRow.Controls.Add(btnJson);
            btnRow.Controls.Add(btnXml);
            root.Controls.Add(btnRow, 0, 1);

            var logRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = Theme.Background,
                Padding = new Padding(0, 16, 0, 0), WrapContents = false
            };
            var lblLogs = UiHelpers.MakeLabel("APENAS LOGS:",
                Theme.FontSizeBase, true, Theme.TextSecondary);
            lblLogs.Margin = new Padding(0, 8, 12, 0);

            var btnLogTxt     = UiHelpers.MakeButton("LOGS (TXT)",  Theme.AccentTeal);
            var btnLogJson    = UiHelpers.MakeButton("LOGS (JSON)", Theme.AccentTeal);
            var btnOpenFolder = UiHelpers.MakeButton("ABRIR PASTA DE LOGS", Theme.AccentMauve);
            foreach (var b in new[] { btnLogTxt, btnLogJson, btnOpenFolder })
            {
                b.Width = 200; b.Margin = new Padding(0, 0, 12, 0);
            }
            btnLogTxt.Click     += (s, e) => DoExportLogsOnly("txt");
            btnLogJson.Click    += (s, e) => DoExportLogsOnly("json");
            btnOpenFolder.Click += (s, e) => OpenLogsFolder();

            logRow.Controls.Add(lblLogs);
            logRow.Controls.Add(btnLogTxt);
            logRow.Controls.Add(btnLogJson);
            logRow.Controls.Add(btnOpenFolder);
            root.Controls.Add(logRow, 0, 2);

            var sep = UiHelpers.MakeLabel("PERITO + FINGERPRINT DA MAQUINA",
                Theme.FontSizeMedium, true, Theme.AccentBlue);
            sep.Margin = new Padding(0, 16, 0, 8);
            root.Controls.Add(sep, 0, 3);

            _peritoInfoBox = new Label
            {
                Dock = DockStyle.Fill, BackColor = Theme.Surface,
                ForeColor = Theme.TextPrimary,
                Font = new Font("Consolas", Theme.FontSizeBase),
                Padding = new Padding(16), TextAlign = ContentAlignment.TopLeft,
                AutoEllipsis = true
            };
            UpdatePeritoInfoBox();
            root.Controls.Add(_peritoInfoBox, 0, 4);

            tab.Controls.Add(root);
            return tab;
        }

        private void UpdatePeritoInfoBox()
        {
            if (_peritoInfoBox == null) return;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("--- PERITO ---");
            sb.AppendLine("  Nome     : " + (_perito.Nome     ?? ""));
            sb.AppendLine("  CPF      : " + (_perito.CPF      ?? ""));
            sb.AppendLine("  Registro : " + (_perito.Registro ?? ""));
            sb.AppendLine("  Orgao    : " + (_perito.Orgao    ?? ""));
            sb.AppendLine("  Cargo    : " + (_perito.Cargo    ?? ""));
            sb.AppendLine("  E-mail   : " + (_perito.Email    ?? ""));
            sb.AppendLine();
            sb.AppendLine("--- FINGERPRINT IMUTAVEL DA MAQUINA ---");
            if (_fingerprint != null)
                sb.Append(MachineFingerprint.ToTextBlock(_fingerprint));
            sb.AppendLine();
            sb.AppendLine("> Bot. 'EDITAR PERITO' no topo para alterar.");
            sb.AppendLine("> Auto-log: " + (_autoLogger != null ? _autoLogger.FilePath : "-"));
            sb.AppendLine("> Indice global: " + SessionRegistry.GetIndexPath(_logsDir));
            _peritoInfoBox.Text = sb.ToString();
        }

        // ============= ACOES =============
        private void ShowAboutDialog()
        {
            using (var dlg = new SplashAboutForm())
            {
                dlg.ShowDialog(this);
            }
        }

        /// <summary>
        /// mountvol /R - remove entries de MountedDevices para volumes que
        /// nao estao atualmente montados. Windows "esquece" drives conhecidos
        /// e nao os remonta automaticamente ao reconectar.
        /// </summary>
        private void ClearMountedDevicesCache()
        {
            var confirm = MessageBox.Show(
                "Isso vai executar 'mountvol /R' - o Windows vai ESQUECER todos "
                + "os drives que ja foram vistos mas nao estao montados agora.\r\n\r\n"
                + "Ao reconectar um drive, ele nao sera remontado automaticamente "
                + "(precisa dar letra manual via Gerenciador de Discos).\r\n\r\n"
                + "Prosseguir?",
                "ShieldBlock - Limpar cache de drives",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;
            try
            {
                bool ok = _engine.ClearMountedDevicesCache();
                Notify(ok ? "Cache de drives limpo" : "Falha ao limpar cache");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro: " + ex.Message,
                    "ShieldBlock", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenIcpDialog()
        {
            using (var dlg = new IcpBrasilDialog(_icpConfig))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    _icpConfig = dlg.Result;
                    _icpConfig.Save(_icpConfigPath);
                    _session.Append("ICP_CONFIGURED",
                        "ICP-Brasil " + (_icpConfig.Enabled ? "HABILITADA" : "desabilitada")
                        + " | Cert: " + (_icpConfig.CertDisplayName ?? "-"));
                    AppendLogLine("ICP_CONFIGURED",
                        "Configuracao ICP atualizada");
                    UpdateIcpBadge();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Falha ao salvar config ICP: " + ex.Message,
                        "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void UpdateIcpBadge()
        {
            if (_icpBadge == null) return;
            if (_icpConfig != null && _icpConfig.Enabled
                && !string.IsNullOrEmpty(_icpConfig.CertThumbprint))
            {
                _icpBadge.Text = "* ICP ATIVO";
                _icpBadge.ForeColor = Theme.AccentGreen;
            }
            else
            {
                _icpBadge.Text = "* SEM ICP";
                _icpBadge.ForeColor = Theme.TextMuted;
            }
        }

        private void SignSessionFinal()
        {
            try
            {
                if (_icpConfig == null || !_icpConfig.Enabled
                    || string.IsNullOrEmpty(_icpConfig.CertThumbprint)) return;
                if (!_icpConfig.AutoSignOnSessionEnd) return;

                var cert = IcpBrasilSigner.GetByThumbprint(_icpConfig.CertThumbprint);
                if (cert == null)
                {
                    _session.Append("ICP_SIGN_ERROR",
                        "Cert configurado nao foi encontrado (thumbprint: "
                        + _icpConfig.CertThumbprint + ")");
                    return;
                }

                string finalHash = _session.FinalHash;
                byte[] sig = IcpBrasilSigner.SignHashString(finalHash, cert);
                string sigPath = Path.Combine(_logsDir,
                    "session_" + _session.SessionId + "_"
                    + _session.StartedAt.ToString("yyyyMMdd_HHmmss") + ".p7s");
                File.WriteAllBytes(sigPath, sig);

                _session.Append("ICP_SIGNED",
                    "Sessao assinada com ICP-Brasil ("
                    + cert.DisplayName() + ") -> "
                    + Path.GetFileName(sigPath) + " ("
                    + sig.Length + " bytes)");

                // Timestamp opcional
                if (_icpConfig.UseTimestamp
                    && !string.IsNullOrEmpty(_icpConfig.TimestampUrl))
                {
                    try
                    {
                        var tsResult = IcpTimestampClient.RequestTimestamp(
                            sig, _icpConfig.TimestampUrl);
                        if (tsResult.Success)
                        {
                            string tsPath = sigPath + ".tst";
                            File.WriteAllBytes(tsPath, tsResult.TokenBytes);
                            _session.Append("ICP_TIMESTAMP",
                                "Carimbo de tempo obtido: "
                                + Path.GetFileName(tsPath) + " ("
                                + tsResult.TokenBytes.Length + " bytes, ACT: "
                                + tsResult.TsaName + ")");
                        }
                        else
                        {
                            _session.Append("ICP_TIMESTAMP_ERROR",
                                "Falha no carimbo de tempo: " + tsResult.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        _session.Append("ICP_TIMESTAMP_ERROR", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _session.Append("ICP_SIGN_ERROR", ex.Message);
            }
        }

        private void OpenPeritoEditor()
        {
            using (var dlg = new PeritoEditDialog(_perito))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    _perito = dlg.Result;
                    _perito.Save(_peritoPath);
                    _session.Perito = _perito;
                    _session.Append("PERITO_UPDATED",
                        "Dados do perito atualizados: " + _perito.Nome
                        + " | CPF: " + _perito.CPF
                        + " | Reg: " + _perito.Registro
                        + " | Orgao: " + _perito.Orgao);
                    AppendLogLine("PERITO_UPDATED", "Dados atualizados via UI");
                    RefreshStatus();
                    MessageBox.Show("Dados do perito salvos em:\n" + _peritoPath,
                        "ShieldBlock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Falha ao salvar perito: " + ex.Message,
                        "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OpenLogsFolder()
        {
            try
            {
                Directory.CreateDirectory(_logsDir);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + _logsDir + "\"");
                Notify("Pasta de logs aberta");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Falha ao abrir pasta: " + ex.Message,
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DoExportLogsOnly(string format)
        {
            try
            {
                Directory.CreateDirectory(_logsDir);
                string ts   = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string ext  = format == "json" ? ".json" : ".txt";
                string file = "shieldblock_LOGS_" + _session.SessionId + "_" + ts + ext;
                string full = Path.Combine(_logsDir, file);

                _session.Append("EXPORT_LOGS_START",
                    "Exportando audit trail (" + format.ToUpper() + ") -> " + file);

                string hash;
                if (format == "json")
                    hash = LogOnlyExporter.ExportJson(full, _session);
                else
                    hash = LogOnlyExporter.ExportTxt(full, _session);

                _session.Append("EXPORT_LOGS_OK",
                    "Logs gerados. SHA-256: " + hash.Substring(0, 16) + "...");
                AppendLogLine("EXPORT_LOGS_OK",
                    "Arquivo: " + file + " | SHA-256: " + hash.Substring(0, 16) + "...");

                MessageBox.Show("Logs exportados!\n\nArquivo: " + file
                    + "\n\nSHA-256:\n" + HashUtility.FormatHashReadable(hash),
                    "ShieldBlock - Logs exportados",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                TryOpenExplorerAt(full);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                _session.Append("EXPORT_LOGS_ERROR", ex.Message);
                AppendLogLine("EXPORT_LOGS_ERROR", ex.Message);
                MessageBox.Show("Falha ao exportar logs: " + ex.Message,
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TryOpenExplorerAt(string filePath)
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe",
                    "/select,\"" + filePath + "\"");
            }
            catch
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe",
                        "\"" + Path.GetDirectoryName(filePath) + "\"");
                }
                catch { }
            }
        }

        private void DoExport(ExportManager.Format fmt)
        {
            try
            {
                Directory.CreateDirectory(_logsDir);
                string ts   = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string file = "shieldblock_" + _session.SessionId + "_" + ts
                            + ExportManager.DefaultExtension(fmt);
                string full = Path.Combine(_logsDir, file);

                _session.Append("EXPORT_START", "Exportando " + fmt + " -> " + file);

                var drives = _drives.EnumerateAll();
                var disks  = _parts.EnumerateDisks();
                var parts  = _parts.EnumeratePartitions();

                string hash = _exporter.Export(fmt, full, _session, drives, disks, parts);

                _session.Append("EXPORT_OK",
                    "Arquivo gerado. SHA-256: " + hash.Substring(0, 16) + "...");
                AppendLogLine("EXPORT_OK",
                    "Arquivo: " + file + " | SHA-256: " + hash.Substring(0, 16) + "...");

                MessageBox.Show("Relatorio gerado!\n\nLocal: " + full
                    + "\n\nSHA-256:\n" + HashUtility.FormatHashReadable(hash),
                    "ShieldBlock - Export OK",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                TryOpenExplorerAt(full);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                _session.Append("EXPORT_ERROR", ex.Message);
                AppendLogLine("EXPORT_ERROR", ex.Message);
                MessageBox.Show("Falha ao exportar: " + ex.Message,
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshAll()
        {
            RefreshStatus();
            RefreshDrives();
            RefreshPartitions();
        }

        private void RefreshStatus()
        {
            bool wb = _engine.IsWriteBlocked();
            bool am = _engine.IsAutoMountEnabled();

            _cardWriteBlock.Value  = wb ? "BLOQUEADO" : "LIVRE";
            _cardWriteBlock.Accent = wb ? Theme.StatusActive : Theme.StatusInactive;
            _cardWriteBlock.Icon   = wb ? "[#]" : "[ ]";

            _cardAutoMount.Value  = am ? "HABILITADO" : "BLOQUEADO";
            _cardAutoMount.Accent = am ? Theme.StatusInactive : Theme.StatusActive;
            _cardAutoMount.Icon   = am ? "[*]" : "[#]";

            _cardEvents.Value   = _session.EventCount.ToString();
            _cardSession.Value  = _session.SessionId;

            _statusBar.Text = string.Format(
                "Sessao {0} | {1} eventos | Hash atual: {2}... | Perito: {3} | Tick #{4}",
                _session.SessionId, _session.EventCount,
                _session.FinalHash.Substring(0, 12),
                _perito.Nome, _heartbeat != null ? _heartbeat.TickCount : 0);

            // Banner de chain-of-custody
            if (_chainHashLabel != null)
                _chainHashLabel.Text = "Hash atual: " + _session.FinalHash.Substring(0, 24) + "...";

            if (_integrityBadge != null)
            {
                bool ok = _session.VerifyIntegrity();
                _integrityBadge.Text = ok ? "INTEGRIDADE OK" : "*** INTEGRIDADE VIOLADA ***";
                _integrityBadge.ForeColor = ok ? Color.White : Color.Yellow;
            }

            UpdatePeritoInfoBox();
        }

        private void UpdateSessionTimer()
        {
            if (_sessionTimerLabel == null) return;
            var dt = DateTime.Now - _sessionStart;
            _sessionTimerLabel.Text = string.Format(
                "Tempo de sessao: {0:D2}:{1:D2}:{2:D2}",
                (int)dt.TotalHours, dt.Minutes, dt.Seconds);
        }

        private void RefreshDrives()
        {
            _gridDrives.Rows.Clear();
            foreach (var d in _drives.EnumerateAll())
            {
                _gridDrives.Rows.Add(d.Letter, d.Label, d.FileSystem, d.DriveType,
                    d.TotalReadable, d.FreeReadable, d.IsReady ? "Pronto" : "-");
            }
        }

        private void RefreshPartitions()
        {
            _gridDisks.Rows.Clear();
            foreach (var d in _parts.EnumerateDisks())
            {
                _gridDisks.Rows.Add(d.DeviceID, d.Model, d.InterfaceType, d.MediaType,
                    d.SerialNumber, d.SizeReadable, d.Partitions);
            }
            _gridParts.Rows.Clear();
            foreach (var p in _parts.EnumeratePartitions())
            {
                _gridParts.Rows.Add(p.DiskIndex, p.Index, p.StartingOffset,
                    p.SizeReadable, p.BlockSize, p.NumberOfBlocks,
                    p.LogicalDisk, p.FileSystem, p.VolumeSerial,
                    p.Bootable ? "Sim" : "-");
            }
        }

        private void AppendLogLine(string category, string description)
        {
            if (_logBox == null) return;
            if (_logBox.InvokeRequired)
            {
                try { _logBox.BeginInvoke(new Action(() => AppendLogLine(category, description))); }
                catch { }
                return;
            }

            var cache = new LogLineCache
            {
                Category = category ?? "",
                Description = description ?? "",
                Timestamp = Iso8601.FormatFriendly(DateTime.Now),
                Seq = _session.EventCount
            };
            _logCache.Add(cache);
            if (_logCache.Count > 5000)
                _logCache.RemoveRange(0, _logCache.Count - 5000);

            if (PassesFilter(cache)) WriteCacheLine(cache);
        }

        private bool PassesFilter(LogLineCache c)
        {
            if (_filterBox == null) return true;
            string q = _filterBox.Text;
            if (string.IsNullOrEmpty(q)) return true;
            q = q.ToLowerInvariant();
            return (c.Category != null && c.Category.ToLowerInvariant().Contains(q))
                || (c.Description != null && c.Description.ToLowerInvariant().Contains(q));
        }

        private void RebuildLogBox()
        {
            if (_logBox == null) return;
            _logBox.Clear();
            foreach (var c in _logCache)
                if (PassesFilter(c)) WriteCacheLine(c);
        }

        private void WriteCacheLine(LogLineCache c)
        {
            _logBox.SelectionColor = Theme.TerminalDim;
            _logBox.AppendText(string.Format("[#{0:D4}] [{1}] ", c.Seq, c.Timestamp));
            _logBox.SelectionColor = ColorForCategory(c.Category);
            _logBox.AppendText("[" + c.Category + "] ");
            _logBox.SelectionColor = Theme.TerminalText;
            _logBox.AppendText(c.Description + Environment.NewLine);
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.ScrollToCaret();
        }

        private static Color ColorForCategory(string cat)
        {
            if (string.IsNullOrEmpty(cat)) return Theme.TerminalText;
            switch (cat.ToUpperInvariant())
            {
                case "ERROR":
                case "EXPORT_ERROR":
                case "EXPORT_LOGS_ERROR":
                case "TAMPER_DETECTED":
                case "TAMPER_GUARD_ERROR":
                case "USB_WATCHER_ERROR":
                case "FS_ERROR":
                case "FS_MONITOR_ERROR":
                case "PROC_AUDIT_ERROR":
                case "WRITE_BLOCK":      return Theme.AccentRed;
                case "AUTOMOUNT":        return Theme.AccentBlue;
                case "USB_INSERTED":
                case "USB_REMOVED":
                case "USB_CONFIG_CHANGED":
                case "USB_EVENT":        return Theme.AccentYellow;
                case "FS_CREATED":
                case "FS_CHANGED":
                case "FS_DELETED":
                case "FS_RENAMED":       return Theme.AccentMauve;
                case "FORENSIC_TOOL_STARTED":
                case "FORENSIC_BASELINE":
                case "FORENSIC_TOOL_STOPPED": return Theme.AccentYellow;
                case "FORENSIC_WATCHER":
                case "FORENSIC_WATCHER_ERROR": return Theme.AccentTeal;
                case "EXPORT_OK":
                case "EXPORT_LOGS_OK":
                case "PERITO_UPDATED":
                case "SESSION_START":
                case "SESSION_END":      return Theme.AccentGreen;
                case "EXPORT_START":
                case "EXPORT_LOGS_START":
                case "USB_WATCHER":
                case "FS_MONITOR_START":
                case "HEARTBEAT":        return Theme.AccentTeal;
                case "FINGERPRINT":
                case "PROC_SNAPSHOT":    return Theme.AccentBlue;
                default:                 return Theme.AccentMauve;
            }
        }

        private void Notify(string msg)
        {
            _statusBar.Text = msg + " | " + Iso8601.FormatFriendly(DateTime.Now);
        }

        private static bool IsAdmin()
        {
            try
            {
                using (var id = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private void ApplyDarkTitleBarHint()
        {
            try
            {
                var attr = 20; int val = 1;
                DwmSetWindowAttribute(this.Handle, attr, ref val, sizeof(int));
            }
            catch { }
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    }
}

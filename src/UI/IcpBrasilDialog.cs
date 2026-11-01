using System;
using System.Collections.Generic;
using System.Drawing;
using System.Security;
using System.Text;
using System.Windows.Forms;
using ShieldBlock.Core;
using ShieldBlock.Icp;
using ShieldBlock.Utils;

namespace ShieldBlock.UI
{
    /// <summary>
    /// Dialog modal de configuracao ICP-Brasil.
    /// Lista TODOS os certificados disponiveis (e-CPF/e-CNPJ, A1/A3),
    /// permite testar assinatura (PIN do smartcard) e salvar escolha.
    /// </summary>
    public class IcpBrasilDialog : Form
    {
        public IcpBrasilConfig Result { get; private set; }

        private ListView _list;
        private RichTextBox _details;
        private CheckBox _chkEnabled, _chkAutoSign, _chkTimestamp;
        private TextBox _txtTsaUrl;
        private List<IcpBrasilCertificate> _available;
        private IcpBrasilCertificate _selected;

        public IcpBrasilDialog(IcpBrasilConfig initial)
        {
            Result = initial ?? IcpBrasilConfig.Default();
            BuildUi();
            LoadCertificates();
            PreSelect();
        }

        private void BuildUi()
        {
            Text = "ShieldBlock - Configuracao ICP-Brasil";
            Width = 900;
            Height = 700;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Background;
            ForeColor = Theme.TextPrimary;
            Font = new Font(Theme.FontFamily, Theme.FontSizeBase);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;

            // ===== HEADER =====
            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Theme.HeaderBar };
            var title = UiHelpers.MakeLabel("ASSINATURA DIGITAL ICP-BRASIL",
                Theme.FontSizeMedium, true, Theme.AccentBlue);
            title.Location = new Point(20, 12);
            var sub = UiHelpers.MakeLabel(
                "e-CPF / e-CNPJ  |  A1 (arquivo) / A3 (smartcard/token)  |  PKCS#7 + Timestamp RFC 3161",
                Theme.FontSizeSmall, false, Theme.TextMuted);
            sub.Location = new Point(20, 36);
            header.Controls.Add(title);
            header.Controls.Add(sub);
            Controls.Add(header);

            // ===== BOTOES INFERIORES =====
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Theme.HeaderBar };
            var btnSave = UiHelpers.MakeButton("SALVAR CONFIG", Theme.AccentGreen);
            var btnCancel = UiHelpers.MakeButton("CANCELAR", Theme.AccentRed);
            var btnTest = UiHelpers.MakeButton("TESTAR ASSINATURA", Theme.AccentYellow);
            var btnLoadPfx = UiHelpers.MakeButton("CARREGAR .PFX (A1)", Theme.AccentMauve);
            var btnRefresh = UiHelpers.MakeButton("ATUALIZAR LISTA", Theme.AccentTeal);
            foreach (var b in new[] { btnRefresh, btnLoadPfx, btnTest, btnSave, btnCancel })
            {
                b.Width = 170; b.Height = 36;
            }
            btnRefresh.Location = new Point(16, 12);
            btnLoadPfx.Location = new Point(196, 12);
            btnTest.Location    = new Point(376, 12);
            btnSave.Location    = new Point(Width - 360, 12);
            btnCancel.Location  = new Point(Width - 180, 12);
            btnSave.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            btnRefresh.Click += (s, e) => LoadCertificates();
            btnLoadPfx.Click += (s, e) => LoadPfx();
            btnTest.Click    += (s, e) => TestSignature();
            btnSave.Click    += (s, e) => SaveAndClose();
            btnCancel.Click  += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            bottom.Controls.Add(btnRefresh);
            bottom.Controls.Add(btnLoadPfx);
            bottom.Controls.Add(btnTest);
            bottom.Controls.Add(btnSave);
            bottom.Controls.Add(btnCancel);
            Controls.Add(bottom);

            // ===== AREA CENTRAL =====
            var central = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                BackColor = Theme.Background, Padding = new Padding(12)
            };
            central.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            central.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            central.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));

            // Lista de certificados
            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = false,
                BackColor = Theme.Surface,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.None,
                Font = new Font(Theme.FontFamily, Theme.FontSizeBase)
            };
            _list.Columns.Add("Tipo",       70);
            _list.Columns.Add("Storage",    80);
            _list.Columns.Add("Titular",    260);
            _list.Columns.Add("CPF/CNPJ",   180);
            _list.Columns.Add("Validade",   100);
            _list.Columns.Add("Emissor",    160);
            _list.SelectedIndexChanged += (s, e) => UpdateDetails();
            central.Controls.Add(_list, 0, 0);

            // Detalhes do selecionado
            _details = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.TerminalBg,
                ForeColor = Theme.TerminalText,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Font = new Font(Theme.FontMonoFallback, Theme.FontSizeBase)
            };
            central.Controls.Add(_details, 0, 1);

            // Opcoes
            var opts = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            _chkEnabled = MakeCheck("Assinatura ICP-Brasil HABILITADA", 0, 4);
            _chkAutoSign = MakeCheck("Assinar automaticamente no fim de cada sessao", 0, 30);
            _chkTimestamp = MakeCheck("Solicitar carimbo de tempo RFC 3161 (ACT-ITI)", 0, 56);
            var lblUrl = UiHelpers.MakeLabel("URL da ACT:", Theme.FontSizeBase, false, Theme.TextSecondary);
            lblUrl.Location = new Point(0, 84);
            _txtTsaUrl = new TextBox
            {
                BackColor = Theme.Surface, ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font(Theme.FontFamily, Theme.FontSizeBase),
                Location = new Point(90, 82), Width = 700
            };
            _txtTsaUrl.Text = Result.TimestampUrl;
            _chkEnabled.Checked   = Result.Enabled;
            _chkAutoSign.Checked  = Result.AutoSignOnSessionEnd;
            _chkTimestamp.Checked = Result.UseTimestamp;
            opts.Controls.Add(_chkEnabled);
            opts.Controls.Add(_chkAutoSign);
            opts.Controls.Add(_chkTimestamp);
            opts.Controls.Add(lblUrl);
            opts.Controls.Add(_txtTsaUrl);
            central.Controls.Add(opts, 0, 2);

            Controls.Add(central);
            central.BringToFront();
        }

        private CheckBox MakeCheck(string text, int x, int y)
        {
            return new CheckBox
            {
                Text = text, Location = new Point(x, y),
                Width = 550, ForeColor = Theme.TextPrimary,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, Theme.FontSizeBase),
                FlatStyle = FlatStyle.Flat
            };
        }

        private void LoadCertificates()
        {
            _list.Items.Clear();
            try
            {
                _available = IcpBrasilSigner.ListAvailableCertificates();
                foreach (var c in _available)
                {
                    string typeStr = c.Type == IcpBrasilCertificate.CertificateType.ECPF ? "e-CPF"
                                   : c.Type == IcpBrasilCertificate.CertificateType.ECNPJ ? "e-CNPJ"
                                   : "Cert";
                    string storeStr = c.Storage == IcpBrasilCertificate.CertificateStorage.SmartCardA3 ? "A3 HW"
                                    : c.Storage == IcpBrasilCertificate.CertificateStorage.FileA1 ? "A1 SW"
                                    : "?";
                    string id = !string.IsNullOrEmpty(c.CPF) ? c.CPF
                              : !string.IsNullOrEmpty(c.CNPJ) ? c.CNPJ : "(sem id)";
                    string issuer = ShortIssuer(c.Issuer);

                    var item = new ListViewItem(typeStr);
                    item.SubItems.Add(storeStr);
                    item.SubItems.Add(c.HolderName ?? "(sem nome)");
                    item.SubItems.Add(id);
                    item.SubItems.Add(c.NotAfter.ToString("dd/MM/yyyy"));
                    item.SubItems.Add(issuer);
                    item.Tag = c;
                    if (c.IsExpired()) item.ForeColor = Theme.AccentRed;
                    else if (c.DaysUntilExpiration() < 30) item.ForeColor = Theme.AccentYellow;
                    _list.Items.Add(item);
                }

                if (_list.Items.Count == 0)
                {
                    _details.Text = "Nenhum certificado ICP-Brasil encontrado no Windows Cert Store.\r\n\r\n"
                        + "- Para e-CPF/e-CNPJ A3: conecte o smartcard/token USB.\r\n"
                        + "- Para A1 (.pfx): clique em 'CARREGAR .PFX' abaixo.";
                }
            }
            catch (Exception ex)
            {
                _details.Text = "Erro ao listar certificados: " + ex.Message;
            }
        }

        private static string ShortIssuer(string fullIssuer)
        {
            if (string.IsNullOrEmpty(fullIssuer)) return "";
            int cn = fullIssuer.IndexOf("CN=", StringComparison.OrdinalIgnoreCase);
            if (cn < 0) return fullIssuer;
            int end = fullIssuer.IndexOf(',', cn);
            if (end < 0) end = fullIssuer.Length;
            string s = fullIssuer.Substring(cn + 3, end - cn - 3);
            if (s.Length > 35) s = s.Substring(0, 32) + "...";
            return s;
        }

        private void PreSelect()
        {
            if (_available == null) return;
            if (string.IsNullOrEmpty(Result.CertThumbprint)) return;
            foreach (ListViewItem it in _list.Items)
            {
                var c = it.Tag as IcpBrasilCertificate;
                if (c != null && string.Equals(c.Thumbprint, Result.CertThumbprint,
                                                StringComparison.OrdinalIgnoreCase))
                {
                    it.Selected = true;
                    it.EnsureVisible();
                    UpdateDetails();
                    return;
                }
            }
        }

        private void UpdateDetails()
        {
            if (_list.SelectedItems.Count == 0) { _selected = null; return; }
            _selected = _list.SelectedItems[0].Tag as IcpBrasilCertificate;
            if (_selected == null) return;
            _details.Text = _selected.DetailsBlock();
        }

        private void LoadPfx()
        {
            using (var ofd = new OpenFileDialog
            {
                Filter = "Certificados PKCS#12 (*.pfx;*.p12)|*.pfx;*.p12|Todos|*.*",
                Title = "Selecione o arquivo .pfx do seu e-CPF/e-CNPJ A1"
            })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                using (var passDlg = new PasswordPromptForm("Senha do certificado A1:"))
                {
                    if (passDlg.ShowDialog(this) != DialogResult.OK) return;
                    try
                    {
                        var cert = IcpBrasilSigner.LoadFromPfx(ofd.FileName,
                            passDlg.SecurePassword);
                        _selected = cert;
                        Result.PfxPath = ofd.FileName;
                        Result.CertThumbprint = cert.Thumbprint;
                        Result.CertDisplayName = cert.DisplayName();
                        _details.Text = "[Carregado de arquivo: " + ofd.FileName + "]\r\n\r\n"
                            + cert.DetailsBlock();
                        MessageBox.Show("Certificado A1 carregado com sucesso!\r\n\r\n"
                            + cert.DisplayName(),
                            "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Falha ao carregar .pfx:\r\n\r\n" + ex.Message,
                            "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void TestSignature()
        {
            if (_selected == null)
            {
                MessageBox.Show("Selecione um certificado primeiro.", "Atencao",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                string testHash = HashUtility.Sha256OfText(
                    "ShieldBlock teste assinatura " + DateTime.Now);
                byte[] sig = IcpBrasilSigner.SignHashString(testHash, _selected);
                var verify = IcpBrasilSigner.Verify(
                    System.Text.Encoding.UTF8.GetBytes(testHash), sig);

                var sb = new StringBuilder();
                sb.AppendLine("=== TESTE DE ASSINATURA ===");
                sb.AppendLine("Hash de teste: " + testHash);
                sb.AppendLine("Tamanho da assinatura: " + sig.Length + " bytes");
                sb.AppendLine();
                sb.AppendLine("Verificacao:");
                foreach (var p in verify) sb.AppendLine("  " + p);
                _details.Text = sb.ToString();

                MessageBox.Show("Assinatura de teste concluida com SUCESSO!\r\n\r\n"
                    + "Tamanho: " + sig.Length + " bytes\r\n"
                    + "Verificacao: " + verify[0],
                    "ICP-Brasil OK",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Falha no teste de assinatura:\r\n\r\n" + ex.Message
                    + "\r\n\r\nDicas:\r\n"
                    + "- Smartcard conectado?\r\n"
                    + "- PIN correto?\r\n"
                    + "- Cert valido (nao expirado)?",
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveAndClose()
        {
            if (_chkEnabled.Checked && _selected == null)
            {
                MessageBox.Show(
                    "Para HABILITAR a assinatura, selecione um certificado primeiro.",
                    "Atencao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Result.Enabled = _chkEnabled.Checked;
            Result.AutoSignOnSessionEnd = _chkAutoSign.Checked;
            Result.UseTimestamp = _chkTimestamp.Checked;
            Result.TimestampUrl = _txtTsaUrl.Text.Trim();
            if (_selected != null)
            {
                Result.CertThumbprint = _selected.Thumbprint;
                Result.CertDisplayName = _selected.DisplayName();
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>
    /// Mini dialog de senha (pra arquivos .pfx A1).
    /// </summary>
    public class PasswordPromptForm : Form
    {
        public SecureString SecurePassword { get; private set; }
        private TextBox _tb;

        public PasswordPromptForm(string prompt)
        {
            Text = "ShieldBlock - Senha do certificado";
            Width = 420; Height = 170;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Background;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;

            var lbl = UiHelpers.MakeLabel(prompt, Theme.FontSizeBase, true, Theme.TextPrimary);
            lbl.Location = new Point(20, 16);
            Controls.Add(lbl);

            _tb = new TextBox
            {
                PasswordChar = '*', UseSystemPasswordChar = true,
                Location = new Point(20, 44), Width = 370,
                BackColor = Theme.Surface, ForeColor = Theme.TextPrimary,
                Font = new Font(Theme.FontFamily, Theme.FontSizeMedium),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_tb);

            var ok = UiHelpers.MakeButton("OK", Theme.AccentGreen);
            ok.Width = 100; ok.Location = new Point(180, 84);
            ok.Click += (s, e) =>
            {
                var ss = new SecureString();
                foreach (char c in _tb.Text) ss.AppendChar(c);
                ss.MakeReadOnly();
                SecurePassword = ss;
                DialogResult = DialogResult.OK; Close();
            };
            Controls.Add(ok);

            var cancel = UiHelpers.MakeButton("CANCELAR", Theme.AccentRed);
            cancel.Width = 100; cancel.Location = new Point(290, 84);
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}

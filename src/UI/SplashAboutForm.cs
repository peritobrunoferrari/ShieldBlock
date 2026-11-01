using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShieldBlock.Core;

namespace ShieldBlock.UI
{
    /// <summary>
    /// Tela de boas-vindas exibida ANTES do MainForm.
    /// Mostra logo, descricao, creditos e contato.
    /// Sai com botao ENTRAR, Enter, Esc ou clicando em qualquer lugar.
    /// </summary>
    public class SplashAboutForm : Form
    {
        public SplashAboutForm()
        {
            BuildUi();
        }

        private void Enter()
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BuildUi()
        {
            Text          = "ShieldBlock - Sobre o sistema";
            Width         = 860;
            Height        = 640;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor     = Theme.Background;
            ForeColor     = Theme.TextPrimary;
            Font          = new Font(Theme.FontFamily, Theme.FontSizeBase);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox   = false;
            MinimizeBox   = false;
            ShowInTaskbar = true;
            KeyPreview    = true;
            KeyDown      += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Escape
                    || e.KeyCode == Keys.Space)
                {
                    Enter();
                }
            };

            // ===== BARRA INFERIOR (botao ENTRAR) - colocada PRIMEIRO no Dock =====
            var bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 90,
                BackColor = Theme.AccentGreen
            };

            var btnEnter = new Button
            {
                Text      = ">> ENTRAR NO SHIELDBLOCK <<",
                Dock      = DockStyle.Fill,
                BackColor = Theme.AccentGreen,
                ForeColor = Theme.HeaderBar,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font(Theme.FontFamily, 14f, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btnEnter.FlatAppearance.BorderSize = 0;
            btnEnter.FlatAppearance.MouseOverBackColor =
                System.Drawing.Color.FromArgb(120, 200, 110);
            btnEnter.FlatAppearance.MouseDownBackColor =
                System.Drawing.Color.FromArgb(100, 180, 90);
            btnEnter.Click += (s, e) => Enter();
            bottom.Controls.Add(btnEnter);
            Controls.Add(bottom);
            AcceptButton = btnEnter;
            TryLoadIcon();


            // ===== BARRA SUPERIOR (azul fino) =====
            var topBar = new Panel
            {
                Dock = DockStyle.Top, Height = 8, BackColor = Theme.AccentBlue
            };
            Controls.Add(topBar);

            // ===== AREA CENTRAL =====
            var main = new Panel
            {
                Dock = DockStyle.Fill, BackColor = Theme.Background
            };
            Controls.Add(main);
            main.BringToFront();

            // Logo grande
            var logo = new ShieldLogo
            {
                LogoColor = Theme.AccentBlue,
                ShowText = true,
                ShowSubtitle = true,
                Size = new Size(260, 320),
                Location = new Point(36, 24),
                BackColor = Theme.Background
            };
            main.Controls.Add(logo);

            // Painel info direita
            var infoPanel = new Panel
            {
                Location = new Point(330, 24),
                Size = new Size(490, 440),
                BackColor = Theme.Surface
            };
            infoPanel.Paint += (s, e) =>
            {
                using (var p = new Pen(Theme.AccentBlue, 3))
                    e.Graphics.DrawLine(p, 0, 0, 0, infoPanel.Height);
            };

            int yy = 18;

            // Titulo
            var appTitle = UiHelpers.MakeLabel("SHIELDBLOCK v6.5",
                22f, true, Theme.AccentBlue);
            appTitle.Location = new Point(20, yy);
            infoPanel.Controls.Add(appTitle);
            yy += 36;

            var appSub = UiHelpers.MakeLabel("USB Write Blocker Forense Auditavel",
                Theme.FontSizeMedium, false, Theme.TextSecondary);
            appSub.Location = new Point(20, yy);
            infoPanel.Controls.Add(appSub);
            yy += 28;

            infoPanel.Controls.Add(MakeDivider(yy));
            yy += 12;

            // SOBRE
            var descTitle = UiHelpers.MakeLabel("SOBRE O SISTEMA",
                Theme.FontSizeBase, true, Theme.AccentMauve);
            descTitle.Location = new Point(20, yy);
            infoPanel.Controls.Add(descTitle);
            yy += 22;

            var desc = new Label
            {
                Location = new Point(20, yy),
                Size = new Size(450, 64),
                Text = "Bloqueador de escrita USB com cadeia de custodia "
                     + "criptograficamente verificavel. Audit trail encadeado "
                     + "por SHA-256, fingerprint imutavel da maquina, log eterno em disco.",
                Font = new Font(Theme.FontFamily, Theme.FontSizeBase),
                ForeColor = Theme.TextPrimary,
                BackColor = Color.Transparent
            };
            infoPanel.Controls.Add(desc);
            yy += 76;

            // DEV
            var devTitle = UiHelpers.MakeLabel("DESENVOLVIDO POR",
                Theme.FontSizeBase, true, Theme.AccentMauve);
            devTitle.Location = new Point(20, yy);
            infoPanel.Controls.Add(devTitle);
            yy += 22;

            var devName = UiHelpers.MakeLabel("Bruno Enrico Moro Ferrari",
                Theme.FontSizeMedium, true, Theme.AccentGreen);
            devName.Location = new Point(20, yy);
            infoPanel.Controls.Add(devName);
            yy += 22;

            var devTitle2 = new Label
            {
                Location = new Point(20, yy), Size = new Size(450, 36),
                Text = "Perito Criminal - Policia Cientifica de SP\r\n"
                     + "Especialista em Computacao Forense",
                Font = new Font(Theme.FontFamily, Theme.FontSizeBase),
                ForeColor = Theme.TextPrimary, BackColor = Color.Transparent
            };
            infoPanel.Controls.Add(devTitle2);
            yy += 44;

            // PARCERIA
            var partTitle = UiHelpers.MakeLabel("EM PARCERIA COM",
                Theme.FontSizeBase, true, Theme.AccentMauve);
            partTitle.Location = new Point(20, yy);
            infoPanel.Controls.Add(partTitle);
            yy += 22;

            var partName = UiHelpers.MakeLabel("SHIELD PERICIAS DIGITAIS",
                Theme.FontSizeMedium, true, Theme.AccentBlue);
            partName.Location = new Point(20, yy);
            infoPanel.Controls.Add(partName);
            yy += 22;

            var partDesc = new Label
            {
                Location = new Point(20, yy), Size = new Size(450, 32),
                Text = "Para uso em instituicoes e por profissionais\r\n"
                     + "de pericia digital.",
                Font = new Font(Theme.FontFamily, Theme.FontSizeBase),
                ForeColor = Theme.TextSecondary, BackColor = Color.Transparent
            };
            infoPanel.Controls.Add(partDesc);
            yy += 36;

            infoPanel.Controls.Add(MakeDivider(yy));
            yy += 10;

            // CONTATO
            var contactEmail = UiHelpers.MakeLabel(
                "[@] contato@shieldpericias.com.br",
                Theme.FontSizeMedium, true, Theme.AccentYellow);
            contactEmail.Location = new Point(20, yy);
            infoPanel.Controls.Add(contactEmail);

            main.Controls.Add(infoPanel);

            // Copyright na base do main (acima do botao)
            var copyright = new Label
            {
                Location = new Point(36, 480), Size = new Size(800, 16),
                Text = "(C) 2026 Bruno Enrico Moro Ferrari & Shield Pericias Digitais",
                Font = new Font(Theme.FontFamily, Theme.FontSizeSmall),
                ForeColor = Theme.TextMuted, BackColor = Color.Transparent
            };
            main.Controls.Add(copyright);

            var copyright2 = new Label
            {
                Location = new Point(36, 498), Size = new Size(800, 16),
                Text = "Distribuicao restrita a profissionais e instituicoes de pericia digital",
                Font = new Font(Theme.FontFamily, Theme.FontSizeSmall),
                ForeColor = Theme.TextMuted, BackColor = Color.Transparent
            };
            main.Controls.Add(copyright2);

            var hint = new Label
            {
                Location = new Point(36, 518), Size = new Size(800, 16),
                Text = "Dica: pressione ENTER, ESPACO ou ESC para continuar",
                Font = new Font(Theme.FontFamily, Theme.FontSizeSmall, FontStyle.Italic),
                ForeColor = Theme.AccentGreen, BackColor = Color.Transparent
            };
            main.Controls.Add(hint);
        }


        private void TryLoadIcon()
        {
            try
            {
                string p = System.IO.Path.Combine(
                    System.AppDomain.CurrentDomain.BaseDirectory,
                    "Resources", "shield.ico");
                if (System.IO.File.Exists(p))
                    this.Icon = new System.Drawing.Icon(p);
            }
            catch { }
        }
        private Panel MakeDivider(int y)
        {
            return new Panel
            {
                Location = new Point(20, y),
                Size = new Size(450, 1),
                BackColor = Theme.Border
            };
        }
    }
}

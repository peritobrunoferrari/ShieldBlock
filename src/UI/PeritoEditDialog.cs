using System;
using System.Drawing;
using System.Windows.Forms;
using ShieldBlock.Core;

namespace ShieldBlock.UI
{
    /// <summary>
    /// Dialog modal para editar os dados do perito cadastrado.
    /// Ao confirmar, salva no Config/perito.json e atualiza a referência.
    /// </summary>
    public class PeritoEditDialog : Form
    {
        public PeritoConfig Result { get; private set; }

        private TextBox _txtNome, _txtCpf, _txtRegistro, _txtOrgao,
                        _txtCargo, _txtEmail, _txtTelefone;

        public PeritoEditDialog(PeritoConfig current)
        {
            Result = current ?? PeritoConfig.Default();
            BuildUi();
            LoadFields(Result);
        }

        private void BuildUi()
        {
            Text            = "ShieldBlock — Editar Perito";
            Width           = 580;
            Height          = 520;
            StartPosition   = FormStartPosition.CenterParent;
            BackColor       = Theme.Background;
            ForeColor       = Theme.TextPrimary;
            Font            = new Font(Theme.FontFamily, Theme.FontSizeBase);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            ShowInTaskbar   = false;

            // ===== HEADER =====
            var header = new Panel
            {
                Dock = DockStyle.Top, Height = 60, BackColor = Theme.HeaderBar
            };
            var title = UiHelpers.MakeLabel("EDITAR PERITO RESPONSÁVEL",
                Theme.FontSizeMedium, true, Theme.AccentBlue);
            title.Location = new Point(20, 12);
            var sub = UiHelpers.MakeLabel(
                "Esses dados aparecem em todos os laudos exportados",
                Theme.FontSizeSmall, false, Theme.TextMuted);
            sub.Location = new Point(20, 36);
            header.Controls.Add(title);
            header.Controls.Add(sub);
            Controls.Add(header);

            // ===== BOTÕES (bottom) =====
            var bottomBar = new Panel
            {
                Dock = DockStyle.Bottom, Height = 60, BackColor = Theme.HeaderBar
            };
            var btnSave   = UiHelpers.MakeButton("💾  SALVAR", Theme.AccentGreen);
            var btnCancel = UiHelpers.MakeButton("CANCELAR", Theme.AccentRed);
            btnSave.Width = 140;
            btnCancel.Width = 120;
            btnSave.Location   = new Point(Width - 290, 12);
            btnCancel.Location = new Point(Width - 140, 12);
            btnSave.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSave.Click   += (s, e) => OnSave();
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            bottomBar.Controls.Add(btnSave);
            bottomBar.Controls.Add(btnCancel);
            Controls.Add(bottomBar);

            // ===== CAMPOS =====
            var form = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 7,
                Padding     = new Padding(24, 16, 24, 16),
                BackColor   = Theme.Background
            };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 7; i++)
                form.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            _txtNome     = AddRow(form, 0, "Nome:");
            _txtCpf      = AddRow(form, 1, "CPF:");
            _txtRegistro = AddRow(form, 2, "Registro:");
            _txtOrgao    = AddRow(form, 3, "Órgão:");
            _txtCargo    = AddRow(form, 4, "Cargo:");
            _txtEmail    = AddRow(form, 5, "E-mail:");
            _txtTelefone = AddRow(form, 6, "Telefone:");

            Controls.Add(form);
            form.BringToFront();
        }

        private TextBox AddRow(TableLayoutPanel form, int row, string labelText)
        {
            var lbl = UiHelpers.MakeLabel(labelText, Theme.FontSizeBase, false,
                Theme.TextSecondary);
            lbl.Anchor   = AnchorStyles.Left;
            lbl.AutoSize = true;
            form.Controls.Add(lbl, 0, row);

            var tb = new TextBox
            {
                BackColor   = Theme.Surface,
                ForeColor   = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font        = new Font(Theme.FontFamily, Theme.FontSizeBase),
                Dock        = DockStyle.Fill,
                Margin      = new Padding(0, 6, 0, 6)
            };
            form.Controls.Add(tb, 1, row);
            return tb;
        }

        private void LoadFields(PeritoConfig p)
        {
            _txtNome.Text     = p.Nome     ?? "";
            _txtCpf.Text      = p.CPF      ?? "";
            _txtRegistro.Text = p.Registro ?? "";
            _txtOrgao.Text    = p.Orgao    ?? "";
            _txtCargo.Text    = p.Cargo    ?? "";
            _txtEmail.Text    = p.Email    ?? "";
            _txtTelefone.Text = p.Telefone ?? "";
        }

        private void OnSave()
        {
            if (string.IsNullOrWhiteSpace(_txtNome.Text))
            {
                MessageBox.Show("O campo Nome é obrigatório.",
                    "Validação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Result.Nome     = _txtNome.Text.Trim();
            Result.CPF      = _txtCpf.Text.Trim();
            Result.Registro = _txtRegistro.Text.Trim();
            Result.Orgao    = _txtOrgao.Text.Trim();
            Result.Cargo    = _txtCargo.Text.Trim();
            Result.Email    = _txtEmail.Text.Trim();
            Result.Telefone = _txtTelefone.Text.Trim();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Forms;
using ShieldBlock.Core;
using ShieldBlock.Utils;

namespace ShieldBlock.UI
{
    /// <summary>
    /// Aba HISTORICO - lista logs antigos, permite abrir, verificar e compactar.
    /// v6.5: layout corrigido - botoes em FlowLayoutPanel com WrapContents
    /// e linha de botoes mais alta para nao cortar.
    /// </summary>
    public class HistoryPanel : Panel
    {
        private readonly string _logsDir;
        private ListBox _listLogs;
        private RichTextBox _preview;
        private Label _summary;

        public HistoryPanel(string logsDir)
        {
            _logsDir = logsDir;
            BackColor = Theme.Background;
            Dock = DockStyle.Fill;
            Build();
            RefreshList();
        }

        private void Build()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4,
                Padding = new Padding(Theme.Padding), BackColor = Theme.Background
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // header
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // lista + preview
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));   // botoes (aumentado)
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));   // summary

            // Header
            var hdr = UiHelpers.MakeLabel("LOGS HISTORICOS - Pasta: " + _logsDir,
                Theme.FontSizeMedium, true, Theme.AccentBlue);
            root.Controls.Add(hdr, 0, 0);
            root.SetColumnSpan(hdr, 2);

            // Lista
            _listLogs = new ListBox
            {
                Dock = DockStyle.Fill, BackColor = Theme.Surface,
                ForeColor = Theme.TextPrimary, BorderStyle = BorderStyle.None,
                Font = new Font(Theme.FontMonoFallback, Theme.FontSizeBase),
                IntegralHeight = false
            };
            _listLogs.SelectedIndexChanged += (s, e) => LoadPreview();
            root.Controls.Add(_listLogs, 0, 1);

            // Preview
            _preview = new RichTextBox
            {
                Dock = DockStyle.Fill, BackColor = Theme.TerminalBg,
                ForeColor = Theme.TerminalText, BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Font = new Font(Theme.FontMonoFallback, Theme.FontSizeBase),
                WordWrap = false
            };
            root.Controls.Add(_preview, 1, 1);

            // BOTOES - FlowLayoutPanel com WrapContents=true para nao cortar
            var btnRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = Theme.Background,
                Padding = new Padding(0, 12, 0, 0),
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight
            };
            var btnRefresh     = UiHelpers.MakeButton("ATUALIZAR LISTA",  Theme.AccentMauve);
            var btnOpen        = UiHelpers.MakeButton("ABRIR NO EDITOR",  Theme.AccentBlue);
            var btnVerify      = UiHelpers.MakeButton("VERIFICAR INTEGRIDADE", Theme.AccentGreen);
            var btnVerifyIndex = UiHelpers.MakeButton("VERIFICAR INDICE GLOBAL", Theme.AccentYellow);
            var btnCompress    = UiHelpers.MakeButton("COMPACTAR (.gz)",  Theme.AccentTeal);

            foreach (var b in new[] { btnRefresh, btnOpen, btnVerify, btnVerifyIndex, btnCompress })
            {
                b.Width = 220; b.Height = 36;
                b.Margin = new Padding(0, 0, 8, 8);
            }
            btnRefresh.Click     += (s, e) => RefreshList();
            btnOpen.Click        += (s, e) => OpenSelected();
            btnVerify.Click      += (s, e) => VerifySelected();
            btnVerifyIndex.Click += (s, e) => VerifyGlobalIndex();
            btnCompress.Click    += (s, e) => CompressSelected();

            btnRow.Controls.Add(btnRefresh);
            btnRow.Controls.Add(btnOpen);
            btnRow.Controls.Add(btnVerify);
            btnRow.Controls.Add(btnVerifyIndex);
            btnRow.Controls.Add(btnCompress);

            root.Controls.Add(btnRow, 0, 2);
            root.SetColumnSpan(btnRow, 2);

            _summary = new Label
            {
                Dock = DockStyle.Fill, BackColor = Theme.Background,
                ForeColor = Theme.TextSecondary,
                Font = new Font(Theme.FontFamily, Theme.FontSizeSmall),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };
            root.Controls.Add(_summary, 0, 3);
            root.SetColumnSpan(_summary, 2);

            Controls.Add(root);
        }

        private void RefreshList()
        {
            _listLogs.Items.Clear();
            try
            {
                if (!Directory.Exists(_logsDir))
                {
                    _summary.Text = "Pasta de logs nao existe ainda.";
                    return;
                }
                var files = new List<string>();
                files.AddRange(Directory.GetFiles(_logsDir, "session_*.log"));
                files.AddRange(Directory.GetFiles(_logsDir, "session_*.log.gz"));
                files.Sort(StringComparer.OrdinalIgnoreCase);
                files.Reverse();

                foreach (var f in files)
                {
                    var fi = new FileInfo(f);
                    _listLogs.Items.Add(string.Format("{0}  ({1:N0} bytes)",
                        fi.Name, fi.Length));
                }

                long total = 0;
                foreach (var f in files) total += new FileInfo(f).Length;
                _summary.Text = files.Count + " arquivos de log  |  "
                              + total.ToString("N0") + " bytes totais  |  "
                              + "indice global: " + (File.Exists(SessionRegistry.GetIndexPath(_logsDir))
                                                     ? "presente" : "ausente");
            }
            catch (Exception ex)
            {
                _summary.Text = "Erro: " + ex.Message;
            }
        }

        private string SelectedFile()
        {
            if (_listLogs.SelectedItem == null) return null;
            string item = _listLogs.SelectedItem.ToString();
            int idx = item.IndexOf("  (", StringComparison.Ordinal);
            string name = idx > 0 ? item.Substring(0, idx) : item;
            return Path.Combine(_logsDir, name);
        }

        private void LoadPreview()
        {
            string f = SelectedFile();
            if (f == null) { _preview.Clear(); return; }
            try
            {
                if (f.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                {
                    _preview.Text = "[Compactado - clique em VERIFICAR para descompactar]\n\nPath: " + f;
                    return;
                }
                string content = File.ReadAllText(f, Encoding.UTF8);
                if (content.Length > 200000)
                    content = content.Substring(0, 200000) + "\n\n[... truncado ...]";
                _preview.Text = content;
            }
            catch (Exception ex)
            {
                _preview.Text = "Erro lendo arquivo: " + ex.Message;
            }
        }

        private void OpenSelected()
        {
            string f = SelectedFile();
            if (f == null) return;
            try { System.Diagnostics.Process.Start("notepad.exe", "\"" + f + "\""); }
            catch (Exception ex) { MessageBox.Show("Falha: " + ex.Message); }
        }

        private void VerifySelected()
        {
            string f = SelectedFile();
            if (f == null) return;
            try
            {
                string content;
                if (f.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                {
                    using (var fs = File.OpenRead(f))
                    using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                    using (var sr = new StreamReader(gz, Encoding.UTF8))
                        content = sr.ReadToEnd();
                }
                else
                {
                    content = File.ReadAllText(f, Encoding.UTF8);
                }

                var sb = new StringBuilder();
                sb.AppendLine("=== VERIFICACAO DE INTEGRIDADE ===");
                sb.AppendLine("Arquivo: " + Path.GetFileName(f));
                sb.AppendLine("Tamanho: " + content.Length + " chars");
                sb.AppendLine("SHA-256 do arquivo: " + HashUtility.Sha256OfText(content));
                sb.AppendLine();

                int countEvents = 0;
                int idx = 0;
                while ((idx = content.IndexOf("hash:", idx, StringComparison.Ordinal)) >= 0)
                {
                    countEvents++; idx += 5;
                }
                sb.AppendLine("Linhas 'hash:' encontradas: " + countEvents);

                bool hasStart = content.Contains(">>> INICIO DA SESSAO <<<");
                bool hasEnd   = content.Contains(">>> FIM DA SESSAO <<<");
                sb.AppendLine("Marcador INICIO: " + (hasStart ? "OK" : "AUSENTE"));
                sb.AppendLine("Marcador FIM   : " + (hasEnd ? "OK" : "AUSENTE - sessao pode ter crashado"));

                _preview.Text = sb.ToString() + "\n\n--- CONTEUDO ---\n" + content;
            }
            catch (Exception ex)
            {
                _preview.Text = "Erro: " + ex.Message;
            }
        }

        private void VerifyGlobalIndex()
        {
            try
            {
                var problems = SessionRegistry.VerifyChain(_logsDir);
                var sb = new StringBuilder();
                sb.AppendLine("=== VERIFICACAO DO INDICE GLOBAL ===");
                sb.AppendLine("Arquivo: " + SessionRegistry.GetIndexPath(_logsDir));
                sb.AppendLine();
                foreach (var p in problems) sb.AppendLine("  " + p);
                _preview.Text = sb.ToString();
            }
            catch (Exception ex) { _preview.Text = "Erro: " + ex.Message; }
        }

        private void CompressSelected()
        {
            string f = SelectedFile();
            if (f == null) return;
            if (f.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Ja esta compactado.");
                return;
            }
            try
            {
                string gz = f + ".gz";
                using (var src = File.OpenRead(f))
                using (var dst = File.Create(gz))
                using (var z = new GZipStream(dst, CompressionLevel.Optimal))
                    src.CopyTo(z);

                long orig = new FileInfo(f).Length;
                long comp = new FileInfo(gz).Length;
                MessageBox.Show(string.Format(
                    "Compactado!\n\nOriginal: {0:N0} bytes\nCompactado: {1:N0} bytes\nReducao: {2:P}",
                    orig, comp, 1.0 - ((double)comp / orig)));
                RefreshList();
            }
            catch (Exception ex) { MessageBox.Show("Erro: " + ex.Message); }
        }
    }
}

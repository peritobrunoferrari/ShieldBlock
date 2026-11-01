using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ShieldBlock.Core;
using ShieldBlock.Utils;

namespace ShieldBlock.Export
{
    /// <summary>
    /// Gerador de PDF rico em C# puro (sem dependencia externa).
    /// v6.5: suporte a retangulos, linhas, boxes para secoes, header/footer
    /// em cada pagina, melhor densidade de conteudo.
    /// </summary>
    public class PdfExporter
    {
        private const float PageWidth   = 595f;
        private const float PageHeight  = 842f;
        private const float MarginLeft  = 40f;
        private const float MarginRight = 40f;
        private const float MarginTop   = 90f;     // espaco pro header
        private const float MarginBottom = 60f;    // espaco pro footer
        private const float LineHeight  = 12f;

        private List<PdfPage> _pages;
        private PdfPage _current;
        private float _y;
        private LogSession _sessionRef;

        public string Export(string path,
                             LogSession session,
                             List<DriveRecord> drives,
                             List<DiskRecord> disks,
                             List<PartitionRecord> partitions)
        {
            _pages = new List<PdfPage>();
            _sessionRef = session;
            NewPage();

            // === CAPA / TITULO ===
            DrawColoredBox(MarginLeft, _y - 60, ContentWidth(), 60,
                0.13f, 0.20f, 0.45f); // azul escuro
            DrawTextAt(MarginLeft + 16, _y - 22,
                "SHIELDBLOCK v6.5", 16, true, 1.0f, 1.0f, 1.0f);
            DrawTextAt(MarginLeft + 16, _y - 42,
                "LAUDO TECNICO DE COLETA - USB Write Blocker Forense",
                10, false, 0.85f, 0.85f, 1.0f);
            _y -= 70;

            // === BLOCO: SESSAO ===
            BeginBox("1. METADADOS DA SESSAO", 0.10f, 0.30f, 0.65f);
            DrawKv("Session ID",   session.SessionId);
            DrawKv("Data/hora",    Iso8601.FormatFriendly(session.StartedAt));
            DrawKv("Operador",     session.OperatorUser + " @ " + session.MachineName);
            DrawKv("Eventos",      session.EventCount.ToString());
            DrawKv("Hash final",   session.FinalHash);
            DrawKv("Integridade",  session.VerifyIntegrity() ? "VERIFICADA - OK" : "VIOLADA");
            EndBox();

            // === BLOCO: PERITO ===
            BeginBox("2. PERITO RESPONSAVEL", 0.55f, 0.15f, 0.55f);
            DrawKv("Nome",     session.Perito.Nome);
            DrawKv("CPF",      session.Perito.CPF);
            DrawKv("Registro", session.Perito.Registro);
            DrawKv("Orgao",    session.Perito.Orgao);
            DrawKv("Cargo",    session.Perito.Cargo);
            if (!string.IsNullOrEmpty(session.Perito.Email))
                DrawKv("E-mail",   session.Perito.Email);
            if (!string.IsNullOrEmpty(session.Perito.Telefone))
                DrawKv("Telefone", session.Perito.Telefone);
            EndBox();

            // === BLOCO: DRIVES LOGICOS ===
            BeginBox("3. DRIVES LOGICOS (" + drives.Count + ")", 0.10f, 0.55f, 0.25f);
            DrawTableHeader(new string[] {
                "Letra", "Rotulo", "FS", "Tipo", "Total", "Livre", "Pronto"
            }, new float[] { 40, 130, 50, 70, 80, 80, 50 });
            foreach (var d in drives)
            {
                DrawTableRow(new string[] {
                    d.Letter ?? "", Trunc(d.Label, 22),
                    d.FileSystem ?? "", d.DriveType ?? "",
                    d.TotalReadable, d.FreeReadable,
                    d.IsReady ? "Sim" : "-"
                }, new float[] { 40, 130, 50, 70, 80, 80, 50 });
            }
            EndBox();

            // === BLOCO: DISCOS FISICOS ===
            BeginBox("4. DISCOS FISICOS (" + disks.Count + ")", 0.55f, 0.30f, 0.10f);
            foreach (var d in disks)
            {
                DrawTextLeft(d.DeviceID + "  [" + d.InterfaceType + " | " + d.MediaType + "]",
                    10, true);
                DrawKv("Modelo",  d.Model);
                DrawKv("Serial",  d.SerialNumber);
                DrawKv("Tamanho", d.SizeReadable + "   |   Particoes: " + d.Partitions);
                _y -= 6;
            }
            EndBox();

            // === BLOCO: PARTICOES ===
            BeginBox("5. PARTICOES (" + partitions.Count + ")", 0.65f, 0.50f, 0.10f);
            DrawTableHeader(new string[] {
                "Disk", "Part", "Offset", "Tamanho", "Bloco", "Volume", "FS", "Serial"
            }, new float[] { 35, 35, 80, 80, 60, 60, 50, 100 });
            foreach (var p in partitions)
            {
                DrawTableRow(new string[] {
                    p.DiskIndex.ToString(),
                    p.Index.ToString(),
                    p.StartingOffset.ToString(),
                    Trunc(p.SizeReadable, 12),
                    p.BlockSize.ToString(),
                    Trunc(p.LogicalDisk, 8),
                    Trunc(p.FileSystem, 6),
                    Trunc(p.VolumeSerial, 14)
                }, new float[] { 35, 35, 80, 80, 60, 60, 50, 100 });
            }
            EndBox();

            // === BLOCO: AUDIT TRAIL ===
            BeginBox("6. AUDIT TRAIL (" + session.EventCount + " eventos encadeados)",
                0.40f, 0.10f, 0.50f);
            foreach (var ev in session.Events)
            {
                DrawTextLeft(string.Format("[#{0:D4}] [{1}] {2}",
                    ev.Sequence,
                    Iso8601.FormatFriendly(ev.Timestamp),
                    ev.Category), 9, true);
                DrawTextLeft("  " + ev.Description, 9, false);
                DrawTextLeft("  hash: " + ev.EventHash, 8, false);
                _y -= 4;
            }
            EndBox();

            // === BLOCO: SELO DE INTEGRIDADE ===
            BeginBox("7. SELO DE INTEGRIDADE SHA-256", 0.10f, 0.45f, 0.10f);
            DrawTextLeft("Hash final encadeado da cadeia de eventos:", 10, false);
            _y -= 4;
            DrawTextLeft(HashUtility.FormatHashReadable(session.FinalHash), 9, true);
            _y -= 6;
            DrawTextLeft(
                "Este hash foi computado pela concatenacao encadeada SHA-256 de TODOS",
                9, false);
            DrawTextLeft(
                "os eventos do audit trail. Qualquer alteracao posterior dos logs",
                9, false);
            DrawTextLeft(
                "invalida o hash final e pode ser detectada por VerifyIntegrity().",
                9, false);
            EndBox();

            // === APLICA HEADER/FOOTER EM TODAS AS PAGINAS ===
            ApplyHeadersAndFooters();

            // === ESCREVE ARQUIVO ===
            WritePdf(path, session);
            return HashUtility.Sha256OfFile(path);
        }

        // ============= LAYOUT HELPERS =============
        private void NewPage()
        {
            _current = new PdfPage();
            _pages.Add(_current);
            _y = PageHeight - MarginTop;
        }

        private float ContentWidth()
        {
            return PageWidth - MarginLeft - MarginRight;
        }

        private void EnsureSpace(float needed)
        {
            if (_y - needed < MarginBottom) NewPage();
        }

        private float _boxStartY;
        private string _boxTitle;
        private float _boxR, _boxG, _boxB;
        private int _boxStartPage;

        private void BeginBox(string title, float r, float g, float bb)
        {
            EnsureSpace(30);
            _boxStartY = _y;
            _boxTitle = title;
            _boxR = r; _boxG = g; _boxB = bb;
            _boxStartPage = _pages.Count - 1;

            // Barra de titulo
            DrawColoredBox(MarginLeft, _y - 18, ContentWidth(), 18, r, g, bb);
            DrawTextAt(MarginLeft + 8, _y - 13, title, 10, true, 1.0f, 1.0f, 1.0f);
            _y -= 22;
        }

        private void EndBox()
        {
            // Borda lateral se mesma pagina; senao apenas espaco
            if (_pages.Count - 1 == _boxStartPage)
            {
                float boxBottom = _y - 4;
                _current.AddRect(MarginLeft, boxBottom,
                    ContentWidth(), _boxStartY - boxBottom,
                    _boxR, _boxG, _boxB, false);
            }
            _y -= 14;
        }

        private void DrawColoredBox(float x, float y, float w, float h,
                                    float r, float g, float bb)
        {
            EnsureSpace(h + 4);
            _current.AddRect(x, y, w, h, r, g, bb, true);
        }

        private void DrawKv(string k, string v)
        {
            EnsureSpace(LineHeight);
            _current.AddText(MarginLeft + 12, _y - 9, k + ":", 9.5f, true, 0.2f, 0.2f, 0.2f);
            _current.AddText(MarginLeft + 110, _y - 9, v ?? "", 9.5f, false, 0, 0, 0);
            _y -= LineHeight;
        }

        private void DrawTextLeft(string text, float size, bool bold)
        {
            EnsureSpace(size + 4);
            _current.AddText(MarginLeft + 12, _y - size + 1, text, size, bold, 0, 0, 0);
            _y -= size + 3;
        }

        private void DrawTextAt(float x, float y, string text, float size, bool bold,
                                float r, float g, float bb)
        {
            _current.AddText(x, y, text, size, bold, r, g, bb);
        }

        private void DrawTableHeader(string[] cols, float[] widths)
        {
            EnsureSpace(16);
            float x = MarginLeft + 8;
            // Faixa cinza no header da tabela
            _current.AddRect(MarginLeft + 4, _y - 12, ContentWidth() - 8, 12,
                0.85f, 0.85f, 0.85f, true);
            for (int i = 0; i < cols.Length; i++)
            {
                _current.AddText(x + 2, _y - 9, cols[i], 8.5f, true, 0.1f, 0.1f, 0.1f);
                x += widths[i];
            }
            _y -= 13;
        }

        private void DrawTableRow(string[] cols, float[] widths)
        {
            EnsureSpace(11);
            float x = MarginLeft + 8;
            for (int i = 0; i < cols.Length; i++)
            {
                string txt = cols[i] ?? "";
                _current.AddText(x + 2, _y - 8, txt, 8f, false, 0, 0, 0);
                x += widths[i];
            }
            // Linha separadora
            _current.AddLine(MarginLeft + 4, _y - 10,
                MarginLeft + ContentWidth() - 4, _y - 10,
                0.85f, 0.85f, 0.85f);
            _y -= 11;
        }

        private void ApplyHeadersAndFooters()
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                var p = _pages[i];
                // HEADER em cada pagina
                p.AddRect(0, PageHeight - 60, PageWidth, 60,
                    0.13f, 0.20f, 0.45f, true);
                p.AddText(MarginLeft, PageHeight - 30,
                    "SHIELDBLOCK v6.5 - Relatorio Pericial",
                    11, true, 1.0f, 1.0f, 1.0f);
                p.AddText(MarginLeft, PageHeight - 48,
                    "Sessao: " + _sessionRef.SessionId
                    + "   |   " + Iso8601.FormatFriendly(_sessionRef.StartedAt),
                    8, false, 0.85f, 0.85f, 1.0f);

                // FOOTER em cada pagina
                p.AddRect(0, 0, PageWidth, 30,
                    0.93f, 0.93f, 0.93f, true);
                p.AddLine(MarginLeft, 30, PageWidth - MarginRight, 30,
                    0.7f, 0.7f, 0.7f);
                p.AddText(MarginLeft, 10,
                    "Bruno Ferrari & Shield Pericias Digitais  |  contato@shieldpericias.com.br",
                    8, false, 0.3f, 0.3f, 0.3f);
                p.AddText(PageWidth - MarginRight - 60, 10,
                    "Pagina " + (i + 1) + " de " + _pages.Count,
                    8, true, 0.3f, 0.3f, 0.3f);
            }
        }

        private static string Trunc(string s, int max)
        {
            if (s == null) return "";
            if (s.Length <= max) return s;
            return s.Substring(0, max - 1) + "~";
        }

        // ============= GERACAO BINARIA =============
        private void WritePdf(string path, LogSession session)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                var enc = Encoding.GetEncoding("ISO-8859-1");
                var offsets = new List<long>();

                Action<string> wstr = (s) =>
                {
                    var bytes = enc.GetBytes(s);
                    fs.Write(bytes, 0, bytes.Length);
                };

                wstr("%PDF-1.4\n");
                fs.WriteByte(0x25); fs.WriteByte(0xE2); fs.WriteByte(0xE3);
                fs.WriteByte(0xCF); fs.WriteByte(0xD3); fs.WriteByte(0x0A);

                int pageCount = _pages.Count;
                int totalObjects = 6 + pageCount * 2 + 1;

                int catalogId = 1, pagesId = 2;
                int fontHelv = 3, fontHelvBold = 4, fontCour = 5, fontCourBold = 6;
                int firstPageId = 7;
                int infoId = 6 + pageCount * 2 + 1;

                offsets.Add(fs.Position);
                wstr(catalogId + " 0 obj\n<< /Type /Catalog /Pages " + pagesId + " 0 R >>\nendobj\n");

                offsets.Add(fs.Position);
                var kids = new StringBuilder();
                for (int i = 0; i < pageCount; i++)
                {
                    int pid = firstPageId + i * 2;
                    if (i > 0) kids.Append(' ');
                    kids.Append(pid).Append(" 0 R");
                }
                wstr(pagesId + " 0 obj\n<< /Type /Pages /Count " + pageCount
                    + " /Kids [" + kids + "] >>\nendobj\n");

                offsets.Add(fs.Position);
                wstr(fontHelv + " 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");
                offsets.Add(fs.Position);
                wstr(fontHelvBold + " 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>\nendobj\n");
                offsets.Add(fs.Position);
                wstr(fontCour + " 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>\nendobj\n");
                offsets.Add(fs.Position);
                wstr(fontCourBold + " 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Courier-Bold >>\nendobj\n");

                for (int i = 0; i < pageCount; i++)
                {
                    int pid = firstPageId + i * 2;
                    int cid = pid + 1;

                    offsets.Add(fs.Position);
                    wstr(pid + " 0 obj\n");
                    wstr("<< /Type /Page /Parent " + pagesId + " 0 R ");
                    wstr("/MediaBox [0 0 " + PageWidth.ToString("0.##") + " "
                                           + PageHeight.ToString("0.##") + "] ");
                    wstr("/Contents " + cid + " 0 R ");
                    wstr("/Resources << /Font << "
                        + "/F1 " + fontHelv + " 0 R "
                        + "/F2 " + fontHelvBold + " 0 R "
                        + "/F3 " + fontCour + " 0 R "
                        + "/F4 " + fontCourBold + " 0 R >> >> ");
                    wstr(">>\nendobj\n");

                    string streamContent = BuildContentStream(_pages[i]);
                    var streamBytes = enc.GetBytes(streamContent);
                    offsets.Add(fs.Position);
                    wstr(cid + " 0 obj\n<< /Length " + streamBytes.Length + " >>\nstream\n");
                    fs.Write(streamBytes, 0, streamBytes.Length);
                    wstr("\nendstream\nendobj\n");
                }

                offsets.Add(fs.Position);
                string title = "ShieldBlock Report " + session.SessionId;
                string author = session.Perito.Nome;
                wstr(infoId + " 0 obj\n<< /Title (" + PdfStr(title) + ") "
                    + "/Author (" + PdfStr(author) + ") "
                    + "/Producer (ShieldBlock v6.5) "
                    + "/Creator (ShieldBlock v6.5 - Bruno Ferrari) >>\nendobj\n");

                long xrefPos = fs.Position;
                wstr("xref\n0 " + (totalObjects + 1) + "\n");
                wstr("0000000000 65535 f \n");
                for (int i = 0; i < offsets.Count; i++)
                    wstr(offsets[i].ToString("D10") + " 00000 n \n");

                wstr("trailer\n<< /Size " + (totalObjects + 1)
                    + " /Root " + catalogId + " 0 R"
                    + " /Info " + infoId + " 0 R >>\n");
                wstr("startxref\n" + xrefPos + "\n%%EOF\n");
            }
        }

        private static string BuildContentStream(PdfPage page)
        {
            var sb = new StringBuilder();
            foreach (var op in page.Ops)
            {
                if (op is PdfRect)
                {
                    var r = (PdfRect)op;
                    sb.Append(r.R.ToString("0.###")).Append(' ')
                      .Append(r.G.ToString("0.###")).Append(' ')
                      .Append(r.B.ToString("0.###")).Append(' ');
                    sb.AppendLine(r.Fill ? "rg" : "RG");
                    sb.Append(r.X.ToString("0.##")).Append(' ')
                      .Append(r.Y.ToString("0.##")).Append(' ')
                      .Append(r.W.ToString("0.##")).Append(' ')
                      .Append(r.H.ToString("0.##")).Append(" re ");
                    sb.AppendLine(r.Fill ? "f" : "S");
                }
                else if (op is PdfLine)
                {
                    var l = (PdfLine)op;
                    sb.Append(l.R.ToString("0.###")).Append(' ')
                      .Append(l.G.ToString("0.###")).Append(' ')
                      .Append(l.B.ToString("0.###")).Append(" RG\n");
                    sb.Append("0.5 w\n");
                    sb.Append(l.X1.ToString("0.##")).Append(' ')
                      .Append(l.Y1.ToString("0.##")).Append(" m ");
                    sb.Append(l.X2.ToString("0.##")).Append(' ')
                      .Append(l.Y2.ToString("0.##")).Append(" l S\n");
                }
                else if (op is PdfText)
                {
                    var t = (PdfText)op;
                    string font = t.Bold ? "/F2" : "/F1";
                    if (t.IsMono) font = t.Bold ? "/F4" : "/F3";
                    sb.Append("BT\n");
                    sb.Append(t.R.ToString("0.###")).Append(' ')
                      .Append(t.G.ToString("0.###")).Append(' ')
                      .Append(t.B.ToString("0.###")).Append(" rg\n");
                    sb.Append(font).Append(' ').Append(t.Size.ToString("0.##")).Append(" Tf\n");
                    sb.Append(t.X.ToString("0.##")).Append(' ')
                      .Append(t.Y.ToString("0.##")).Append(" Td\n");
                    sb.Append('(').Append(PdfStr(t.Text)).Append(") Tj\n");
                    sb.Append("ET\n");
                }
            }
            return sb.ToString();
        }

        private static string PdfStr(string text)
        {
            if (text == null) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
                    continue;
                }
                if (char.IsLowSurrogate(c)) continue;

                if (c == '\\') sb.Append("\\\\");
                else if (c == '(') sb.Append("\\(");
                else if (c == ')') sb.Append("\\)");
                else if (c < 32 || c > 126) sb.Append(ApproxAscii(c));
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string ApproxAscii(char c)
        {
            switch ((int)c)
            {
                case 0x00E1: case 0x00E0: case 0x00E3: case 0x00E2: case 0x00E4: return "a";
                case 0x00C1: case 0x00C0: case 0x00C3: case 0x00C2: case 0x00C4: return "A";
                case 0x00E9: case 0x00E8: case 0x00EA: case 0x00EB: return "e";
                case 0x00C9: case 0x00C8: case 0x00CA: case 0x00CB: return "E";
                case 0x00ED: case 0x00EC: case 0x00EE: case 0x00EF: return "i";
                case 0x00CD: case 0x00CC: case 0x00CE: case 0x00CF: return "I";
                case 0x00F3: case 0x00F2: case 0x00F5: case 0x00F4: case 0x00F6: return "o";
                case 0x00D3: case 0x00D2: case 0x00D5: case 0x00D4: case 0x00D6: return "O";
                case 0x00FA: case 0x00F9: case 0x00FB: case 0x00FC: return "u";
                case 0x00DA: case 0x00D9: case 0x00DB: case 0x00DC: return "U";
                case 0x00E7: return "c";
                case 0x00C7: return "C";
                case 0x00F1: return "n";
                case 0x00D1: return "N";
                case 0x2014: case 0x2013: return "-";
                case 0x201C: case 0x201D: return "\"";
                case 0x2018: case 0x2019: return "'";
                case 0x2026: return "...";
                case 0x2022: return "*";
                case 0x2713: return "+";
                case 0x2717: return "x";
                default:
                    return (c < 128) ? c.ToString() : "?";
            }
        }

        // ============= TIPOS INTERNOS =============
        private class PdfPage
        {
            public List<PdfOp> Ops = new List<PdfOp>();

            public void AddText(float x, float y, string text, float size, bool bold,
                                float r, float g, float b)
            {
                Ops.Add(new PdfText
                {
                    X = x, Y = y, Text = text, Size = size, Bold = bold,
                    R = r, G = g, B = b,
                    IsMono = false
                });
            }

            public void AddRect(float x, float y, float w, float h,
                                float r, float g, float b, bool fill)
            {
                Ops.Add(new PdfRect
                {
                    X = x, Y = y, W = w, H = h,
                    R = r, G = g, B = b, Fill = fill
                });
            }

            public void AddLine(float x1, float y1, float x2, float y2,
                                float r, float g, float b)
            {
                Ops.Add(new PdfLine
                {
                    X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                    R = r, G = g, B = b
                });
            }
        }

        private abstract class PdfOp { }

        private class PdfText : PdfOp
        {
            public float X, Y, Size, R, G, B;
            public string Text;
            public bool Bold, IsMono;
        }

        private class PdfRect : PdfOp
        {
            public float X, Y, W, H, R, G, B;
            public bool Fill;
        }

        private class PdfLine : PdfOp
        {
            public float X1, Y1, X2, Y2, R, G, B;
        }
    }
}

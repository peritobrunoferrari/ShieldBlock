using System.Collections.Generic;
using System.IO;
using System.Text;
using ShieldBlock.Core;
using ShieldBlock.Utils;

namespace ShieldBlock.Export
{
    /// <summary>
    /// Exportador HTML interativo (self-contained) — filtro de busca, ordenação,
    /// dark theme matching o app, sem dependências externas.
    /// </summary>
    public class HtmlExporter
    {
        public string Export(string path,
                             LogSession session,
                             List<DriveRecord> drives,
                             List<DiskRecord> disks,
                             List<PartitionRecord> partitions)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"pt-BR\"><head>");
            sb.AppendLine("<meta charset=\"UTF-8\">");
            sb.AppendLine("<title>ShieldBlock — Relatório Pericial " + Esc(session.SessionId) + "</title>");
            sb.AppendLine(GetStyles());
            sb.AppendLine("</head><body>");

            // HEADER
            sb.AppendLine("<header class=\"top\">");
            sb.AppendLine("  <div class=\"brand\">🛡️ ShieldBlock <span class=\"ver\">v6.0</span></div>");
            sb.AppendLine("  <div class=\"sub\">Relatório de Sessão Pericial</div>");
            sb.AppendLine("</header>");

            sb.AppendLine("<main>");

            // METADADOS DA SESSÃO
            sb.AppendLine("<section class=\"card\">");
            sb.AppendLine("  <h2>📋 Metadados da Sessão</h2>");
            sb.AppendLine("  <table class=\"kv\">");
            Kv(sb, "Session ID", session.SessionId);
            Kv(sb, "Iniciada em", Iso8601.FormatFriendly(session.StartedAt));
            Kv(sb, "Operador", session.OperatorUser + " @ " + session.MachineName);
            Kv(sb, "Total de eventos", session.EventCount.ToString());
            Kv(sb, "Hash final (SHA-256)", "<code>" + Esc(session.FinalHash) + "</code>");
            Kv(sb, "Integridade",
                session.VerifyIntegrity()
                    ? "<span class=\"ok\">✓ OK</span>"
                    : "<span class=\"err\">✗ VIOLADA</span>");
            sb.AppendLine("  </table>");
            sb.AppendLine("</section>");

            // PERITO
            sb.AppendLine("<section class=\"card\">");
            sb.AppendLine("  <h2>👤 Perito Responsável</h2>");
            sb.AppendLine("  <table class=\"kv\">");
            Kv(sb, "Nome", session.Perito.Nome);
            Kv(sb, "CPF", session.Perito.CPF);
            Kv(sb, "Registro", session.Perito.Registro);
            Kv(sb, "Órgão", session.Perito.Orgao);
            Kv(sb, "Cargo", session.Perito.Cargo);
            Kv(sb, "E-mail", session.Perito.Email);
            sb.AppendLine("  </table>");
            sb.AppendLine("</section>");

            // FILTRO
            sb.AppendLine("<section class=\"card filter-card\">");
            sb.AppendLine("  <label>🔎 Filtrar tabelas: ");
            sb.AppendLine("    <input type=\"text\" id=\"q\" placeholder=\"digite para filtrar...\" oninput=\"filterAll()\">");
            sb.AppendLine("  </label>");
            sb.AppendLine("</section>");

            // DRIVES
            sb.AppendLine("<section class=\"card\">");
            sb.AppendLine("  <h2>💾 Drives Lógicos (" + drives.Count + ")</h2>");
            sb.AppendLine("  <table class=\"data\" id=\"t-drives\">");
            sb.AppendLine("    <thead><tr><th>Letra</th><th>Rótulo</th><th>FS</th><th>Tipo</th><th>Total</th><th>Livre</th><th>Status</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var d in drives)
            {
                sb.Append("      <tr><td>").Append(Esc(d.Letter))
                    .Append("</td><td>").Append(Esc(d.Label))
                    .Append("</td><td>").Append(Esc(d.FileSystem))
                    .Append("</td><td>").Append(Esc(d.DriveType))
                    .Append("</td><td>").Append(Esc(d.TotalReadable))
                    .Append("</td><td>").Append(Esc(d.FreeReadable))
                    .Append("</td><td>").Append(d.IsReady ? "✓" : "—")
                    .AppendLine("</td></tr>");
            }
            sb.AppendLine("    </tbody></table>");
            sb.AppendLine("</section>");

            // DISCOS FÍSICOS
            sb.AppendLine("<section class=\"card\">");
            sb.AppendLine("  <h2>🗄️ Discos Físicos (" + disks.Count + ")</h2>");
            sb.AppendLine("  <table class=\"data\" id=\"t-disks\">");
            sb.AppendLine("    <thead><tr><th>DeviceID</th><th>Modelo</th><th>Interface</th><th>Mídia</th><th>Serial</th><th>Tamanho</th><th>Parts</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var d in disks)
            {
                sb.Append("      <tr><td><code>").Append(Esc(d.DeviceID))
                    .Append("</code></td><td>").Append(Esc(d.Model))
                    .Append("</td><td>").Append(Esc(d.InterfaceType))
                    .Append("</td><td>").Append(Esc(d.MediaType))
                    .Append("</td><td>").Append(Esc(d.SerialNumber))
                    .Append("</td><td>").Append(Esc(d.SizeReadable))
                    .Append("</td><td>").Append(d.Partitions)
                    .AppendLine("</td></tr>");
            }
            sb.AppendLine("    </tbody></table>");
            sb.AppendLine("</section>");

            // PARTIÇÕES
            sb.AppendLine("<section class=\"card\">");
            sb.AppendLine("  <h2>🧩 Partições (" + partitions.Count + ")</h2>");
            sb.AppendLine("  <table class=\"data\" id=\"t-parts\">");
            sb.AppendLine("    <thead><tr><th>Disk</th><th>Part</th><th>Offset</th><th>Tamanho</th><th>Bloco</th><th>Blocos</th><th>Volume</th><th>FS</th><th>Serial</th><th>Boot</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var p in partitions)
            {
                sb.Append("      <tr><td>").Append(p.DiskIndex)
                    .Append("</td><td>").Append(p.Index)
                    .Append("</td><td>").Append(p.StartingOffset)
                    .Append("</td><td>").Append(Esc(p.SizeReadable))
                    .Append("</td><td>").Append(p.BlockSize)
                    .Append("</td><td>").Append(p.NumberOfBlocks)
                    .Append("</td><td>").Append(Esc(p.LogicalDisk))
                    .Append("</td><td>").Append(Esc(p.FileSystem))
                    .Append("</td><td>").Append(Esc(p.VolumeSerial))
                    .Append("</td><td>").Append(p.Bootable ? "✓" : "—")
                    .AppendLine("</td></tr>");
            }
            sb.AppendLine("    </tbody></table>");
            sb.AppendLine("</section>");

            // AUDIT TRAIL
            sb.AppendLine("<section class=\"card\">");
            sb.AppendLine("  <h2>📜 Audit Trail (" + session.EventCount + " eventos)</h2>");
            sb.AppendLine("  <table class=\"data\" id=\"t-audit\">");
            sb.AppendLine("    <thead><tr><th>#</th><th>Timestamp</th><th>Categoria</th><th>Descrição</th><th>Hash</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var ev in session.Events)
            {
                sb.Append("      <tr><td>").Append(ev.Sequence)
                    .Append("</td><td>").Append(Esc(Iso8601.FormatFriendly(ev.Timestamp)))
                    .Append("</td><td><span class=\"cat\">").Append(Esc(ev.Category))
                    .Append("</span></td><td>").Append(Esc(ev.Description))
                    .Append("</td><td><code title=\"").Append(Esc(ev.EventHash)).Append("\">")
                    .Append(Esc(ev.EventHash.Substring(0, 12))).Append("…")
                    .AppendLine("</code></td></tr>");
            }
            sb.AppendLine("    </tbody></table>");
            sb.AppendLine("</section>");

            sb.AppendLine("</main>");
            sb.AppendLine("<footer>Gerado por ShieldBlock v6.0 em " + Iso8601.FormatFriendly(System.DateTime.Now) + "</footer>");
            sb.AppendLine(GetScript());
            sb.AppendLine("</body></html>");

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return HashUtility.Sha256OfFile(path);
        }

        private static void Kv(StringBuilder sb, string k, string v)
        {
            sb.Append("    <tr><th>").Append(Esc(k)).Append("</th><td>").Append(v ?? "").AppendLine("</td></tr>");
        }

        private static string Esc(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;");
        }

        private static string GetStyles()
        {
            return @"<style>
* { box-sizing: border-box; }
body { margin: 0; font-family: 'Segoe UI', sans-serif; background: #1E1E2E; color: #CDD6F4; font-size: 14px; }
header.top { background: #181825; padding: 20px 32px; border-bottom: 3px solid #89B4FA; }
.brand { font-size: 24px; font-weight: 600; color: #89B4FA; }
.brand .ver { color: #A6E3A1; font-size: 14px; margin-left: 8px; }
.sub { color: #A6ADC8; font-size: 13px; margin-top: 4px; }
main { max-width: 1400px; margin: 24px auto; padding: 0 24px; }
.card { background: #313244; border: 1px solid #45475A; border-radius: 8px; padding: 20px; margin-bottom: 20px; }
.card h2 { margin: 0 0 16px 0; font-size: 16px; color: #89B4FA; border-bottom: 1px solid #45475A; padding-bottom: 8px; }
table.kv { width: 100%; border-collapse: collapse; }
table.kv th { text-align: left; padding: 8px 12px; color: #A6ADC8; font-weight: 500; width: 200px; vertical-align: top; }
table.kv td { padding: 8px 12px; color: #CDD6F4; }
table.data { width: 100%; border-collapse: collapse; font-size: 12px; }
table.data thead th { background: #45475A; color: #CDD6F4; padding: 10px 8px; text-align: left; position: sticky; top: 0; }
table.data tbody td { padding: 8px; border-bottom: 1px solid #45475A; }
table.data tbody tr:hover { background: #45475A; }
.ok { color: #A6E3A1; font-weight: bold; }
.err { color: #F38BA8; font-weight: bold; }
code { background: #11111B; color: #94E2D5; padding: 2px 6px; border-radius: 3px; font-family: 'Cascadia Mono', 'Consolas', monospace; font-size: 11px; }
.cat { background: #45475A; padding: 2px 8px; border-radius: 4px; font-size: 11px; font-family: monospace; }
.filter-card input { width: 320px; padding: 8px 12px; background: #11111B; color: #CDD6F4; border: 1px solid #45475A; border-radius: 4px; font-size: 13px; }
footer { text-align: center; padding: 20px; color: #7F849C; font-size: 12px; border-top: 1px solid #45475A; margin-top: 32px; }
</style>";
        }

        private static string GetScript()
        {
            return @"<script>
function filterAll() {
  var q = document.getElementById('q').value.toLowerCase();
  ['t-drives','t-disks','t-parts','t-audit'].forEach(function(id){
    var tb = document.querySelector('#' + id + ' tbody');
    if (!tb) return;
    Array.prototype.forEach.call(tb.rows, function(r){
      r.style.display = r.innerText.toLowerCase().indexOf(q) >= 0 ? '' : 'none';
    });
  });
}
</script>";
        }
    }
}

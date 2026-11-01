using System.IO;
using System.Text;
using ShieldBlock.Core;
using ShieldBlock.Utils;

namespace ShieldBlock.Export
{
    /// <summary>
    /// Exportadores que geram apenas o audit trail (sem drives / partições).
    /// Úteis para anexar rapidamente em e-mails, processos, etc.
    /// </summary>
    public static class LogOnlyExporter
    {
        /// <summary>
        /// Exporta apenas os logs em TXT humano-legível.
        /// Retorna o SHA-256 do arquivo gerado.
        /// </summary>
        public static string ExportTxt(string path, LogSession session)
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================");
            sb.AppendLine("  SHIELDBLOCK v6.1 — AUDIT TRAIL (somente logs)");
            sb.AppendLine("================================================================");
            sb.AppendLine("Session ID    : " + session.SessionId);
            sb.AppendLine("Iniciada em   : " + Iso8601.FormatFriendly(session.StartedAt));
            sb.AppendLine("Operador      : " + session.OperatorUser + " @ " + session.MachineName);
            sb.AppendLine("Perito        : " + session.Perito.Nome);
            sb.AppendLine("CPF / Registro: " + session.Perito.CPF + " / " + session.Perito.Registro);
            sb.AppendLine("Órgão         : " + session.Perito.Orgao);
            sb.AppendLine("Total eventos : " + session.EventCount);
            sb.AppendLine("Hash final    : " + session.FinalHash);
            sb.AppendLine("Integridade   : " + (session.VerifyIntegrity() ? "OK" : "VIOLADA"));
            sb.AppendLine("================================================================");
            sb.AppendLine();
            sb.AppendLine("AUDIT TRAIL:");
            sb.AppendLine("----------------------------------------------------------------");

            foreach (var ev in session.Events)
            {
                sb.AppendLine(string.Format("[#{0:D4}] [{1}] [{2}]",
                    ev.Sequence,
                    Iso8601.FormatFriendly(ev.Timestamp),
                    ev.Category));
                sb.AppendLine("        " + ev.Description);
                sb.AppendLine("        prev_hash:  " + ev.PrevHash);
                sb.AppendLine("        event_hash: " + ev.EventHash);
                sb.AppendLine();
            }

            sb.AppendLine("----------------------------------------------------------------");
            sb.AppendLine("Gerado por ShieldBlock v6.1 em " + Iso8601.FormatNow());

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return HashUtility.Sha256OfFile(path);
        }

        /// <summary>
        /// Exporta apenas os logs em JSON estruturado.
        /// </summary>
        public static string ExportJson(string path, LogSession session)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"tool\": \"ShieldBlock v6.1\",\n");
            sb.Append("  \"export_type\": \"audit_trail_only\",\n");
            sb.Append("  \"generated_at\": ").Append(J(Iso8601.FormatNow())).Append(",\n");

            sb.Append("  \"session\": {\n");
            sb.Append("    \"id\": ").Append(J(session.SessionId)).Append(",\n");
            sb.Append("    \"started_at\": ").Append(J(Iso8601.Format(session.StartedAt))).Append(",\n");
            sb.Append("    \"operator_user\": ").Append(J(session.OperatorUser)).Append(",\n");
            sb.Append("    \"machine_name\": ").Append(J(session.MachineName)).Append(",\n");
            sb.Append("    \"event_count\": ").Append(session.EventCount).Append(",\n");
            sb.Append("    \"final_hash\": ").Append(J(session.FinalHash)).Append(",\n");
            sb.Append("    \"integrity_ok\": ").Append(session.VerifyIntegrity() ? "true" : "false").Append("\n");
            sb.Append("  },\n");

            sb.Append("  \"perito\": {\n");
            sb.Append("    \"nome\": ").Append(J(session.Perito.Nome)).Append(",\n");
            sb.Append("    \"cpf\": ").Append(J(session.Perito.CPF)).Append(",\n");
            sb.Append("    \"registro\": ").Append(J(session.Perito.Registro)).Append(",\n");
            sb.Append("    \"orgao\": ").Append(J(session.Perito.Orgao)).Append(",\n");
            sb.Append("    \"cargo\": ").Append(J(session.Perito.Cargo)).Append("\n");
            sb.Append("  },\n");

            sb.Append("  \"audit_trail\": [\n");
            for (int i = 0; i < session.Events.Count; i++)
            {
                var ev = session.Events[i];
                sb.Append("    {");
                sb.Append("\"seq\": ").Append(ev.Sequence).Append(", ");
                sb.Append("\"timestamp\": ").Append(J(Iso8601.Format(ev.Timestamp))).Append(", ");
                sb.Append("\"category\": ").Append(J(ev.Category)).Append(", ");
                sb.Append("\"description\": ").Append(J(ev.Description)).Append(", ");
                sb.Append("\"prev_hash\": ").Append(J(ev.PrevHash)).Append(", ");
                sb.Append("\"event_hash\": ").Append(J(ev.EventHash));
                sb.Append("}").Append(i < session.Events.Count - 1 ? "," : "").Append("\n");
            }
            sb.Append("  ]\n");
            sb.Append("}\n");

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return HashUtility.Sha256OfFile(path);
        }

        private static string J(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '\\') sb.Append("\\\\");
                else if (c == '"') sb.Append("\\\"");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < 32) sb.AppendFormat("\\u{0:x4}", (int)c);
                else sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}

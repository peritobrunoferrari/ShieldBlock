using System;
using System.IO;
using System.Text;

namespace ShieldBlock.Icp
{
    /// <summary>
    /// Persiste configuracao ICP-Brasil (qual certificado usar, se assinatura
    /// esta habilitada, URL do servidor de timestamp) em Config/icp.json.
    /// </summary>
    public class IcpBrasilConfig
    {
        public bool Enabled              { get; set; }
        public string CertThumbprint     { get; set; }
        public string CertDisplayName    { get; set; }
        public string PfxPath            { get; set; }
        public bool UseTimestamp         { get; set; }
        public string TimestampUrl       { get; set; }
        public bool AutoSignOnSessionEnd { get; set; }

        public static IcpBrasilConfig Default()
        {
            return new IcpBrasilConfig
            {
                Enabled = false,
                CertThumbprint = "",
                CertDisplayName = "",
                PfxPath = "",
                UseTimestamp = true,
                TimestampUrl = IcpTimestampClient.ITI_ACT_URL,
                AutoSignOnSessionEnd = true
            };
        }

        public static IcpBrasilConfig Load(string path)
        {
            if (!File.Exists(path)) return Default();
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                return new IcpBrasilConfig
                {
                    Enabled              = GetBool(json, "enabled", false),
                    CertThumbprint       = GetStr(json, "cert_thumbprint"),
                    CertDisplayName      = GetStr(json, "cert_display_name"),
                    PfxPath              = GetStr(json, "pfx_path"),
                    UseTimestamp         = GetBool(json, "use_timestamp", true),
                    TimestampUrl         = GetStrOrDefault(json, "timestamp_url",
                                              IcpTimestampClient.ITI_ACT_URL),
                    AutoSignOnSessionEnd = GetBool(json, "auto_sign_on_session_end", true)
                };
            }
            catch { return Default(); }
        }

        public void Save(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"enabled\":                 " + (Enabled ? "true" : "false") + ",");
            sb.AppendLine("  \"cert_thumbprint\":         " + JsonStr(CertThumbprint) + ",");
            sb.AppendLine("  \"cert_display_name\":       " + JsonStr(CertDisplayName) + ",");
            sb.AppendLine("  \"pfx_path\":                " + JsonStr(PfxPath) + ",");
            sb.AppendLine("  \"use_timestamp\":           " + (UseTimestamp ? "true" : "false") + ",");
            sb.AppendLine("  \"timestamp_url\":           " + JsonStr(TimestampUrl) + ",");
            sb.AppendLine("  \"auto_sign_on_session_end\":" + (AutoSignOnSessionEnd ? "true" : "false"));
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static string GetStr(string json, string key)
        {
            int idx = json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return "";
            int colon = json.IndexOf(':', idx);
            if (colon < 0) return "";
            int firstQuote = json.IndexOf('"', colon);
            if (firstQuote < 0) return "";
            int secondQuote = json.IndexOf('"', firstQuote + 1);
            if (secondQuote < 0) return "";
            return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

        private static string GetStrOrDefault(string json, string key, string def)
        {
            string v = GetStr(json, key);
            return string.IsNullOrEmpty(v) ? def : v;
        }

        private static bool GetBool(string json, string key, bool def)
        {
            int idx = json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return def;
            int colon = json.IndexOf(':', idx);
            if (colon < 0) return def;
            string after = json.Substring(colon + 1).TrimStart();
            if (after.StartsWith("true", StringComparison.OrdinalIgnoreCase))  return true;
            if (after.StartsWith("false", StringComparison.OrdinalIgnoreCase)) return false;
            return def;
        }

        private static string JsonStr(string s)
        {
            if (s == null) return "\"\"";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '\\') sb.Append("\\\\");
                else if (c == '"') sb.Append("\\\"");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c < 32) sb.AppendFormat("\\u{0:x4}", (int)c);
                else sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}

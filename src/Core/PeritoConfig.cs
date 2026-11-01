using System;
using System.IO;
using System.Text;

namespace ShieldBlock.Core
{
    /// <summary>
    /// Metadados do perito responsável pela coleta.
    /// Carregado de Config/perito.json. Obrigatório para laudo oficial.
    /// </summary>
    public class PeritoConfig
    {
        public string Nome       { get; set; }
        public string CPF        { get; set; }
        public string Registro   { get; set; }
        public string Orgao      { get; set; }
        public string Cargo      { get; set; }
        public string Email      { get; set; }
        public string Telefone   { get; set; }

        public static PeritoConfig Default()
        {
            return new PeritoConfig
            {
                Nome     = "[Não configurado]",
                CPF      = "[Não configurado]",
                Registro = "[Não configurado]",
                Orgao    = "[Não configurado]",
                Cargo    = "Perito Digital",
                Email    = "",
                Telefone = ""
            };
        }

        public static PeritoConfig Load(string path)
        {
            if (!File.Exists(path)) return Default();
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                return new PeritoConfig
                {
                    Nome     = ExtractString(json, "nome"),
                    CPF      = ExtractString(json, "cpf"),
                    Registro = ExtractString(json, "registro"),
                    Orgao    = ExtractString(json, "orgao"),
                    Cargo    = ExtractString(json, "cargo"),
                    Email    = ExtractString(json, "email"),
                    Telefone = ExtractString(json, "telefone")
                };
            }
            catch
            {
                return Default();
            }
        }

        private static string ExtractString(string json, string key)
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

        public string SummaryLine()
        {
            return string.Format("{0} (CPF: {1} | Reg: {2} | {3})",
                Nome, CPF, Registro, Orgao);
        }

        /// <summary>
        /// Salva a configuração no arquivo JSON. Estrutura compatível com Load().
        /// </summary>
        public void Save(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"nome\":     " + JsonStr(Nome) + ",");
            sb.AppendLine("  \"cpf\":      " + JsonStr(CPF) + ",");
            sb.AppendLine("  \"registro\": " + JsonStr(Registro) + ",");
            sb.AppendLine("  \"orgao\":    " + JsonStr(Orgao) + ",");
            sb.AppendLine("  \"cargo\":    " + JsonStr(Cargo) + ",");
            sb.AppendLine("  \"email\":    " + JsonStr(Email) + ",");
            sb.AppendLine("  \"telefone\": " + JsonStr(Telefone));
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
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
                else if (c == '\t') sb.Append("\\t");
                else if (c < 32) sb.AppendFormat("\\u{0:x4}", (int)c);
                else sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}

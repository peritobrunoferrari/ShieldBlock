using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ShieldBlock.Icp
{
    /// <summary>
    /// Wrapper de X509Certificate2 com extracao especifica de campos
    /// ICP-Brasil. Suporta e-CPF (PF) e e-CNPJ (PJ), ambos em A1 e A3.
    ///
    /// OIDs ICP-Brasil relevantes:
    ///   2.16.76.1.3.1 - dadosTitularPessoaFisica (e-CPF)
    ///   2.16.76.1.3.3 - cnpj (e-CNPJ)
    ///   2.16.76.1.3.4 - dadosResponsavelPessoaFisica (e-CNPJ - resp.)
    ///   2.16.76.1.3.5 - dadosTitularPessoaJuridica (e-CNPJ)
    ///   2.16.76.1.3.6 - nomeEmpresarial (e-CNPJ)
    ///   2.16.76.1.3.7 - dadosTitularPessoaFisicaResp (e-CNPJ)
    ///   2.16.76.1.3.8 - nomeEmpresarial alternativo
    /// </summary>
    public class IcpBrasilCertificate
    {
        public X509Certificate2 X509 { get; private set; }

        public string Subject       { get { return X509.Subject; } }
        public string Issuer        { get { return X509.Issuer; } }
        public DateTime NotBefore   { get { return X509.NotBefore; } }
        public DateTime NotAfter    { get { return X509.NotAfter; } }
        public string Thumbprint    { get { return X509.Thumbprint; } }
        public string SerialNumber  { get { return X509.SerialNumber; } }
        public bool HasPrivateKey   { get { return X509.HasPrivateKey; } }

        public CertificateType Type      { get; private set; }
        public string HolderName         { get; private set; }
        public string CPF                { get; private set; }
        public string CNPJ               { get; private set; }
        public string CompanyName        { get; private set; }
        public string ResponsibleName    { get; private set; }
        public string ResponsibleCPF     { get; private set; }
        public CertificateStorage Storage { get; private set; }

        public enum CertificateType { Unknown, ECPF, ECNPJ }
        public enum CertificateStorage { Unknown, FileA1, SmartCardA3, Other }

        public IcpBrasilCertificate(X509Certificate2 cert)
        {
            X509 = cert;
            ParseSubject();
            ParseOids();
            DetectStorage();
            DetectType();
        }

        // ============= PARSE SUBJECT =============
        private void ParseSubject()
        {
            // Subject formato: CN=NOME:CPF, OU=AC SOLUTI Multipla, O=ICP-Brasil, ...
            string subj = X509.Subject ?? "";
            var parts = SplitSubject(subj);
            string cn = GetValue(parts, "CN");
            if (!string.IsNullOrEmpty(cn))
            {
                int colon = cn.LastIndexOf(':');
                if (colon > 0)
                {
                    HolderName = cn.Substring(0, colon).Trim();
                    // O que vem depois pode ser CPF (11) ou CNPJ (14)
                    string after = cn.Substring(colon + 1).Trim();
                    if (after.Length == 11 && IsDigits(after)) CPF = FormatCPF(after);
                    else if (after.Length == 14 && IsDigits(after)) CNPJ = FormatCNPJ(after);
                }
                else
                {
                    HolderName = cn.Trim();
                }
            }
        }

        private static Dictionary<string, string> SplitSubject(string subject)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var parts = subject.Split(',');
            foreach (var p in parts)
            {
                int eq = p.IndexOf('=');
                if (eq > 0)
                {
                    string k = p.Substring(0, eq).Trim();
                    string v = p.Substring(eq + 1).Trim();
                    if (!dict.ContainsKey(k)) dict[k] = v;
                }
            }
            return dict;
        }

        private static string GetValue(Dictionary<string, string> d, string key)
        {
            string v; return d.TryGetValue(key, out v) ? v : "";
        }

        // ============= PARSE OIDS =============
        private void ParseOids()
        {
            foreach (var ext in X509.Extensions)
            {
                try
                {
                    string oid = ext.Oid != null ? ext.Oid.Value : "";
                    if (oid == "2.5.29.17") // subjectAltName
                    {
                        ParseSubjectAltName(ext.RawData);
                    }
                }
                catch { }
            }
        }

        private void ParseSubjectAltName(byte[] raw)
        {
            // SubjectAltName eh ASN.1 SEQUENCE. Dentro tem otherName com OIDs
            // ICP. Implementacao manual simplificada que procura padroes de OID.
            try
            {
                // OIDs ICP-Brasil codificados em ASN.1:
                // 2.16.76.1.3.1 = 06 08 60 4C 01 03 01  (PF)
                // 2.16.76.1.3.3 = 06 08 60 4C 01 03 03  (CNPJ)
                // 2.16.76.1.3.4 = 06 08 60 4C 01 03 04
                // ...

                byte[] oid_3_1 = HexToBytes("0608604C01030001"); // PF dados titular
                byte[] oid_3_3 = HexToBytes("0608604C01030003"); // CNPJ
                byte[] oid_3_4 = HexToBytes("0608604C01030004"); // Responsavel
                byte[] oid_3_5 = HexToBytes("0608604C01030005"); // PJ titular
                byte[] oid_3_6 = HexToBytes("0608604C01030006"); // Nome empresarial
                byte[] oid_3_7 = HexToBytes("0608604C01030007"); // Resp PF (CNPJ)

                int idx_3_1 = FindBytes(raw, oid_3_1);
                int idx_3_3 = FindBytes(raw, oid_3_3);
                int idx_3_4 = FindBytes(raw, oid_3_4);
                int idx_3_5 = FindBytes(raw, oid_3_5);
                int idx_3_6 = FindBytes(raw, oid_3_6);
                int idx_3_7 = FindBytes(raw, oid_3_7);

                // Extrai string ASN.1 que vem apos cada OID
                if (idx_3_1 >= 0)
                {
                    string data = ExtractIcpString(raw, idx_3_1 + oid_3_1.Length);
                    ParsePfData(data);
                }
                if (idx_3_3 >= 0)
                {
                    string cnpj = ExtractIcpString(raw, idx_3_3 + oid_3_3.Length);
                    if (cnpj.Length >= 14) CNPJ = FormatCNPJ(cnpj.Substring(0, 14));
                }
                if (idx_3_5 >= 0)
                {
                    string data = ExtractIcpString(raw, idx_3_5 + oid_3_5.Length);
                    // Pode conter CNPJ + dados
                    if (data.Length >= 14 && IsDigits(data.Substring(0, 14)))
                        CNPJ = CNPJ ?? FormatCNPJ(data.Substring(0, 14));
                }
                if (idx_3_6 >= 0)
                {
                    CompanyName = ExtractIcpString(raw, idx_3_6 + oid_3_6.Length).Trim();
                }
                if (idx_3_7 >= 0)
                {
                    string data = ExtractIcpString(raw, idx_3_7 + oid_3_7.Length);
                    ParseResponsavelData(data);
                }
                if (idx_3_4 >= 0)
                {
                    string data = ExtractIcpString(raw, idx_3_4 + oid_3_4.Length);
                    ParseResponsavelData(data);
                }
            }
            catch { /* SubjectAltName malformado nao deve quebrar */ }
        }

        private void ParsePfData(string data)
        {
            // Formato tipico: ddmmaaaa + CPF(11) + NIS/PIS(11) + RG/Doc(15) + ...
            // Mas pode variar. Extrair CPF se for 11 digitos consecutivos.
            if (string.IsNullOrEmpty(data)) return;
            // Procura sequencia de 11 digitos
            for (int i = 0; i <= data.Length - 11; i++)
            {
                string chunk = data.Substring(i, 11);
                if (IsDigits(chunk))
                {
                    if (CPF == null) CPF = FormatCPF(chunk);
                    break;
                }
            }
        }

        private void ParseResponsavelData(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            for (int i = 0; i <= data.Length - 11; i++)
            {
                string chunk = data.Substring(i, 11);
                if (IsDigits(chunk))
                {
                    if (ResponsibleCPF == null) ResponsibleCPF = FormatCPF(chunk);
                    // O resto pode ser nome
                    int rest = i + 11;
                    if (rest < data.Length)
                    {
                        string remaining = data.Substring(rest).Trim();
                        // Limpar caracteres nao-imprimiveis
                        var sb = new StringBuilder();
                        foreach (char c in remaining)
                            if (c >= 32 && c < 127) sb.Append(c);
                        if (sb.Length > 3) ResponsibleName = sb.ToString().Trim();
                    }
                    break;
                }
            }
        }

        private static string ExtractIcpString(byte[] raw, int startIdx)
        {
            // Apos o OID, vem geralmente uma SEQUENCE/OCTET STRING.
            // Aqui pegamos os bytes imprimiveis que sao prefixados por tag ASN.1
            if (startIdx >= raw.Length) return "";
            var sb = new StringBuilder();
            // Pula 1-4 bytes de tag/length
            int i = startIdx;
            int maxScan = Math.Min(raw.Length, startIdx + 200);
            for (; i < maxScan; i++)
            {
                byte b = raw[i];
                if (b >= 32 && b < 127) sb.Append((char)b);
                else if (sb.Length > 5 && b == 0) break;
            }
            return sb.ToString();
        }

        private static int FindBytes(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0 || haystack.Length < needle.Length) return -1;
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] arr = new byte[hex.Length / 2];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return arr;
        }

        // ============= TIPO + STORAGE =============
        private void DetectType()
        {
            if (!string.IsNullOrEmpty(CNPJ)) Type = CertificateType.ECNPJ;
            else if (!string.IsNullOrEmpty(CPF)) Type = CertificateType.ECPF;
            else Type = CertificateType.Unknown;
        }

        private void DetectStorage()
        {
            try
            {
                if (!X509.HasPrivateKey) { Storage = CertificateStorage.Unknown; return; }
                var key = X509.PrivateKey;
                if (key == null) { Storage = CertificateStorage.Unknown; return; }
                string keyClass = key.GetType().Name;
                // Smart cards usam CSPs especificos (ePass, SafeNet, etc).
                // Detectamos pela presenca de "smart" ou CSPs conhecidos no nome.
                var rsa = key as System.Security.Cryptography.RSACryptoServiceProvider;
                if (rsa != null && rsa.CspKeyContainerInfo != null)
                {
                    string provider = rsa.CspKeyContainerInfo.ProviderName ?? "";
                    bool hw = rsa.CspKeyContainerInfo.HardwareDevice;
                    if (hw || ContainsSmartCardProvider(provider))
                        Storage = CertificateStorage.SmartCardA3;
                    else
                        Storage = CertificateStorage.FileA1;
                }
                else Storage = CertificateStorage.Other;
            }
            catch { Storage = CertificateStorage.Unknown; }
        }

        private static bool ContainsSmartCardProvider(string p)
        {
            if (string.IsNullOrEmpty(p)) return false;
            string l = p.ToLowerInvariant();
            return l.Contains("smart") || l.Contains("smartcard")
                || l.Contains("epass") || l.Contains("safenet")
                || l.Contains("gemalto") || l.Contains("etoken")
                || l.Contains("scard") || l.Contains("token");
        }

        // ============= UTILS =============
        public bool IsExpired()
        {
            DateTime now = DateTime.Now;
            return now < NotBefore || now > NotAfter;
        }

        public int DaysUntilExpiration()
        {
            return (int)(NotAfter - DateTime.Now).TotalDays;
        }

        public string DisplayName()
        {
            string typeStr = Type == CertificateType.ECPF ? "e-CPF"
                          : Type == CertificateType.ECNPJ ? "e-CNPJ" : "Cert";
            string storageStr = Storage == CertificateStorage.SmartCardA3 ? "A3"
                             : Storage == CertificateStorage.FileA1 ? "A1" : "?";
            string id = !string.IsNullOrEmpty(CPF) ? CPF
                      : !string.IsNullOrEmpty(CNPJ) ? CNPJ : "(sem id)";
            return string.Format("[{0} {1}] {2}  ({3})",
                typeStr, storageStr, HolderName ?? "?", id);
        }

        public string DetailsBlock()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tipo            : "
                + (Type == CertificateType.ECPF ? "e-CPF (Pessoa Fisica)"
                : Type == CertificateType.ECNPJ ? "e-CNPJ (Pessoa Juridica)" : "Desconhecido"));
            sb.AppendLine("Armazenamento   : "
                + (Storage == CertificateStorage.SmartCardA3 ? "A3 - Smartcard/Token (hardware)"
                : Storage == CertificateStorage.FileA1 ? "A1 - Arquivo .pfx (software)" : "Desconhecido"));
            sb.AppendLine("Titular         : " + (HolderName ?? "?"));
            if (!string.IsNullOrEmpty(CPF))
                sb.AppendLine("CPF             : " + CPF);
            if (!string.IsNullOrEmpty(CNPJ))
                sb.AppendLine("CNPJ            : " + CNPJ);
            if (!string.IsNullOrEmpty(CompanyName))
                sb.AppendLine("Razao Social    : " + CompanyName);
            if (!string.IsNullOrEmpty(ResponsibleName))
                sb.AppendLine("Responsavel     : " + ResponsibleName
                    + (string.IsNullOrEmpty(ResponsibleCPF) ? "" : " (CPF " + ResponsibleCPF + ")"));
            sb.AppendLine("Validade        : " + NotBefore.ToString("dd/MM/yyyy")
                + " ate " + NotAfter.ToString("dd/MM/yyyy")
                + " (" + DaysUntilExpiration() + " dias restantes)");
            sb.AppendLine("Emissor         : " + Issuer);
            sb.AppendLine("Serial          : " + SerialNumber);
            sb.AppendLine("Thumbprint      : " + Thumbprint);
            sb.AppendLine("Chave privada   : " + (HasPrivateKey ? "Sim (pode assinar)" : "NAO (so verifica)"));
            return sb.ToString();
        }

        private static bool IsDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) if (c < '0' || c > '9') return false;
            return true;
        }

        private static string FormatCPF(string raw)
        {
            if (raw == null || raw.Length != 11) return raw ?? "";
            return raw.Substring(0, 3) + "." + raw.Substring(3, 3) + "."
                + raw.Substring(6, 3) + "-" + raw.Substring(9, 2);
        }

        private static string FormatCNPJ(string raw)
        {
            if (raw == null || raw.Length != 14) return raw ?? "";
            return raw.Substring(0, 2) + "." + raw.Substring(2, 3) + "."
                + raw.Substring(5, 3) + "/" + raw.Substring(8, 4) + "-"
                + raw.Substring(12, 2);
        }
    }
}

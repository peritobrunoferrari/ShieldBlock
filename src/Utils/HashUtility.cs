using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ShieldBlock.Utils
{
    /// <summary>
    /// Utilitários de hash SHA-256 para cadeia de custódia.
    /// Compatível com .NET Framework 4.7.2 (sem Convert.ToHexString).
    /// </summary>
    public static class HashUtility
    {
        public static string Sha256OfText(string text)
        {
            if (text == null) text = "";
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                return BytesToHex(bytes);
            }
        }

        public static string Sha256OfFile(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
            {
                return BytesToHex(sha.ComputeHash(fs));
            }
        }

        public static string Sha256OfBytes(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                return BytesToHex(sha.ComputeHash(data));
            }
        }

        public static string BytesToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// Formata hash em grupos de 8 caracteres separados por espaço (legível em laudo).
        /// Ex: a1b2c3d4 e5f6a7b8 ...
        /// </summary>
        public static string FormatHashReadable(string hexHash)
        {
            if (string.IsNullOrEmpty(hexHash)) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < hexHash.Length; i += 8)
            {
                if (i > 0) sb.Append(' ');
                int len = Math.Min(8, hexHash.Length - i);
                sb.Append(hexHash.Substring(i, len));
            }
            return sb.ToString();
        }
    }
}

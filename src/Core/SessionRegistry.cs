using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ShieldBlock.Utils;

namespace ShieldBlock.Core
{
    /// <summary>
    /// Indice global append-only de TODAS as sessoes do ShieldBlock.
    /// Arquivo: Logs/SESSIONS_INDEX.log
    ///
    /// Cada sessao registra UMA linha contendo:
    ///   timestamp | sessionId | userSid | machineGuid | exeHash | finalHash | filePath | prevLineHash | thisLineHash
    ///
    /// O thisLineHash inclui o prevLineHash da linha anterior - assim,
    /// alterar qualquer linha do indice quebra a cadeia inteira.
    /// </summary>
    public static class SessionRegistry
    {
        private const string IndexFileName = "SESSIONS_INDEX.log";
        private const string Separator = " | ";
        private static readonly object _lock = new object();

        public static string GetIndexPath(string logsDir)
        {
            return Path.Combine(logsDir, IndexFileName);
        }

        /// <summary>
        /// Registra a abertura de uma sessao no indice global.
        /// Retorna o hash da linha gravada (que apos termino da sessao
        /// sera usado como referencia).
        /// </summary>
        public static string AppendOpening(string logsDir,
                                           LogSession session,
                                           MachineFingerprint.Snapshot fp)
        {
            lock (_lock)
            {
                if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);
                string path = GetIndexPath(logsDir);

                string prevHash = GetLastLineHash(path);

                string line = string.Join(Separator, new[]
                {
                    "OPEN ",
                    Iso8601.FormatNow(),
                    session.SessionId,
                    SafeField(fp.UserName),
                    SafeField(fp.UserSid),
                    SafeField(fp.MachineGuid),
                    SafeField(fp.ExecutableHash).Substring(0, Math.Min(16, fp.ExecutableHash != null ? fp.ExecutableHash.Length : 0)),
                    "OPENING",
                    SafeField(session.AutoLogger != null ? Path.GetFileName(session.AutoLogger.FilePath) : "(no autolog)"),
                    prevHash
                });
                string lineHash = HashUtility.Sha256OfText(line);
                string fullLine = line + Separator + lineHash;

                AppendLine(path, fullLine);
                return lineHash;
            }
        }

        public static string AppendClosing(string logsDir, LogSession session)
        {
            lock (_lock)
            {
                if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);
                string path = GetIndexPath(logsDir);

                string prevHash = GetLastLineHash(path);

                string line = string.Join(Separator, new[]
                {
                    "CLOSE",
                    Iso8601.FormatNow(),
                    session.SessionId,
                    session.OperatorUser,
                    "-",
                    "-",
                    "-",
                    session.EventCount.ToString() + " events",
                    session.FinalHash.Substring(0, 16),
                    prevHash
                });
                string lineHash = HashUtility.Sha256OfText(line);
                string fullLine = line + Separator + lineHash;

                AppendLine(path, fullLine);
                return lineHash;
            }
        }

        private static void AppendLine(string path, string line)
        {
            using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write,
                                            FileShare.Read))
            using (var w = new StreamWriter(fs, new UTF8Encoding(false)))
            {
                if (fs.Length == 0)
                {
                    w.WriteLine("# ShieldBlock SESSIONS_INDEX - append-only chain of sessions");
                    w.WriteLine("# Format: TYPE | timestamp | sessionId | user | sid | machineGuid | exeHash | extra | filename | prevHash | thisHash");
                    w.WriteLine("# Tampering breaks the chain. Never edit this file manually.");
                    w.WriteLine("#");
                }
                w.WriteLine(line);
                w.Flush();
            }
        }

        private static string GetLastLineHash(string path)
        {
            if (!File.Exists(path)) return new string('0', 64);
            try
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    string l = lines[i];
                    if (string.IsNullOrWhiteSpace(l) || l.StartsWith("#")) continue;
                    int idx = l.LastIndexOf(Separator, StringComparison.Ordinal);
                    if (idx < 0) continue;
                    return l.Substring(idx + Separator.Length).Trim();
                }
            }
            catch { }
            return new string('0', 64);
        }

        /// <summary>
        /// Verifica a cadeia inteira do indice. Retorna lista de problemas.
        /// </summary>
        public static List<string> VerifyChain(string logsDir)
        {
            var problems = new List<string>();
            string path = GetIndexPath(logsDir);
            if (!File.Exists(path))
            {
                problems.Add("Arquivo de indice nao existe: " + path);
                return problems;
            }

            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch (Exception ex)
            {
                problems.Add("Falha ao ler indice: " + ex.Message);
                return problems;
            }

            string expectedPrev = new string('0', 64);
            int lineNum = 0;
            foreach (string raw in lines)
            {
                lineNum++;
                if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#")) continue;

                int lastSep = raw.LastIndexOf(Separator, StringComparison.Ordinal);
                if (lastSep < 0)
                {
                    problems.Add("Linha " + lineNum + ": formato invalido");
                    continue;
                }
                string baseLine = raw.Substring(0, lastSep);
                string storedHash = raw.Substring(lastSep + Separator.Length).Trim();
                string computed = HashUtility.Sha256OfText(baseLine);

                if (storedHash != computed)
                {
                    problems.Add("Linha " + lineNum + ": hash invalido (esperado " +
                                 computed.Substring(0, 16) + "..., armazenado " +
                                 storedHash.Substring(0, Math.Min(16, storedHash.Length)) + "...)");
                }

                // Verifica prevHash
                int prevSep = baseLine.LastIndexOf(Separator, StringComparison.Ordinal);
                if (prevSep >= 0)
                {
                    string storedPrev = baseLine.Substring(prevSep + Separator.Length).Trim();
                    if (storedPrev != expectedPrev)
                    {
                        problems.Add("Linha " + lineNum + ": prev_hash quebrado (cadeia rompida)");
                    }
                }

                expectedPrev = storedHash;
            }

            if (problems.Count == 0)
                problems.Add("OK - cadeia integra (" + lineNum + " linhas verificadas)");
            return problems;
        }

        private static string SafeField(string s)
        {
            if (string.IsNullOrEmpty(s)) return "-";
            return s.Replace("|", "/").Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}

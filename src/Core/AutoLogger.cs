using System;
using System.IO;
using System.Text;
using ShieldBlock.Utils;

namespace ShieldBlock.Core
{
    /// <summary>
    /// Persistencia continua dos logs em disco.
    /// Cada evento eh escrito IMEDIATAMENTE no arquivo (FileStream + AutoFlush) -
    /// sobrevive a crash da aplicacao.
    ///
    /// v6.2: cabecalho inclui MachineFingerprint imutavel (hash do .exe,
    /// machine GUID, SID do usuario, BIOS serial).
    /// </summary>
    public class AutoLogger : IDisposable
    {
        private readonly string _path;
        private StreamWriter _writer;
        private bool _disposed;
        private readonly object _lock = new object();
        private MachineFingerprint.Snapshot _fingerprint;

        public string FilePath { get { return _path; } }
        public MachineFingerprint.Snapshot Fingerprint { get { return _fingerprint; } }

        public AutoLogger(string logsDir, LogSession session,
                          MachineFingerprint.Snapshot fp)
        {
            if (!Directory.Exists(logsDir))
                Directory.CreateDirectory(logsDir);

            string ts = session.StartedAt.ToString("yyyyMMdd_HHmmss");
            string fileName = "session_" + session.SessionId + "_" + ts + ".log";
            _path = Path.Combine(logsDir, fileName);
            _fingerprint = fp;

            var fs = new FileStream(_path, FileMode.Create, FileAccess.Write,
                                    FileShare.Read);
            _writer = new StreamWriter(fs, new UTF8Encoding(false));
            _writer.AutoFlush = true;

            WriteHeader(session);
        }

        private void WriteHeader(LogSession session)
        {
            lock (_lock)
            {
                _writer.WriteLine("================================================================");
                _writer.WriteLine("  SHIELDBLOCK v6.2 - AUTO-LOG DE SESSAO PERICIAL");
                _writer.WriteLine("================================================================");
                _writer.WriteLine("Arquivo gerado automaticamente. Cada evento gravado no ato.");
                _writer.WriteLine("Logs sao mantidos eternamente (nao deletados automaticamente).");
                _writer.WriteLine("");
                _writer.WriteLine("--- IDENTIFICACAO DA SESSAO ---");
                _writer.WriteLine("Session ID    : " + session.SessionId);
                _writer.WriteLine("Inicio em     : " + Iso8601.FormatFriendly(session.StartedAt));
                _writer.WriteLine("");
                _writer.WriteLine("--- PERITO ---");
                _writer.WriteLine("Nome          : " + session.Perito.Nome);
                _writer.WriteLine("CPF / Registro: " + session.Perito.CPF + " / " + session.Perito.Registro);
                _writer.WriteLine("Orgao         : " + session.Perito.Orgao);
                _writer.WriteLine("Cargo         : " + session.Perito.Cargo);
                _writer.WriteLine("Email         : " + (session.Perito.Email ?? ""));
                _writer.WriteLine("");
                _writer.WriteLine("--- FINGERPRINT IMUTAVEL DA MAQUINA ---");
                if (_fingerprint != null)
                    _writer.Write(MachineFingerprint.ToTextBlock(_fingerprint));
                else
                    _writer.WriteLine("(fingerprint nao capturada)");
                _writer.WriteLine("");
                _writer.WriteLine("================================================================");
                _writer.WriteLine("");
                _writer.WriteLine(">>> INICIO DA SESSAO <<<");
                _writer.WriteLine("");
            }
        }

        public void WriteEvent(AuditEvent ev)
        {
            if (_disposed || ev == null) return;
            lock (_lock)
            {
                try
                {
                    _writer.WriteLine(string.Format("[#{0:D4}] [{1}] [{2}]",
                        ev.Sequence,
                        Iso8601.FormatFriendly(ev.Timestamp),
                        ev.Category));
                    _writer.WriteLine("        " + ev.Description);
                    _writer.WriteLine("        hash: " + ev.EventHash);
                    _writer.WriteLine("");
                }
                catch { }
            }
        }

        public void WriteFooter(LogSession session)
        {
            if (_disposed) return;
            lock (_lock)
            {
                try
                {
                    _writer.WriteLine("");
                    _writer.WriteLine(">>> FIM DA SESSAO <<<");
                    _writer.WriteLine("");
                    _writer.WriteLine("================================================================");
                    _writer.WriteLine("Encerrada em  : " + Iso8601.FormatNow());
                    _writer.WriteLine("Total eventos : " + session.EventCount);
                    _writer.WriteLine("Hash final    : " + session.FinalHash);
                    _writer.WriteLine("Integridade   : " + (session.VerifyIntegrity() ? "OK" : "VIOLADA"));
                    _writer.WriteLine("================================================================");
                }
                catch { }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_lock)
            {
                try
                {
                    if (_writer != null)
                    {
                        _writer.Flush();
                        _writer.Dispose();
                        _writer = null;
                    }
                }
                catch { }
            }
        }
    }
}

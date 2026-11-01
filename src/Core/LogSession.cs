using System;
using System.Collections.Generic;
using System.Text;
using ShieldBlock.Utils;

namespace ShieldBlock.Core
{
    /// <summary>
    /// Sessao pericial com audit trail e encadeamento de hash entre eventos.
    /// Cada evento contem o hash do anterior (estilo blockchain leve),
    /// permitindo detectar adulteracao posterior do log.
    ///
    /// v6.1: integrada com AutoLogger - cada Append() grava na hora em disco.
    /// </summary>
    public class LogSession
    {
        public string SessionId      { get; private set; }
        public DateTime StartedAt    { get; private set; }
        public string OperatorUser   { get; private set; }
        public string MachineName    { get; private set; }
        public PeritoConfig Perito   { get; set; }
        public List<AuditEvent> Events { get; private set; }

        /// <summary>
        /// Se definido, cada evento eh gravado IMEDIATAMENTE no arquivo.
        /// </summary>
        public AutoLogger AutoLogger { get; set; }

        public LogSession(PeritoConfig perito)
        {
            SessionId    = GenerateId();
            StartedAt    = DateTime.Now;
            OperatorUser = Environment.UserName;
            MachineName  = Environment.MachineName;
            Perito       = perito ?? PeritoConfig.Default();
            Events       = new List<AuditEvent>();

            Append("SESSION_START",
                string.Format("Sessao iniciada por {0}@{1}", OperatorUser, MachineName));
        }

        public AuditEvent Append(string category, string description)
        {
            string prevHash = Events.Count == 0
                ? new string('0', 64)
                : Events[Events.Count - 1].EventHash;

            var ev = new AuditEvent
            {
                Sequence    = Events.Count + 1,
                Timestamp   = DateTime.Now,
                Category    = category ?? "INFO",
                Description = description ?? "",
                PrevHash    = prevHash
            };
            ev.EventHash = ComputeEventHash(ev);
            Events.Add(ev);

            // Auto-save no disco se logger configurado
            if (AutoLogger != null)
                AutoLogger.WriteEvent(ev);

            return ev;
        }

        private string ComputeEventHash(AuditEvent ev)
        {
            string payload = string.Format("{0}|{1}|{2}|{3}|{4}",
                ev.Sequence,
                Iso8601.Format(ev.Timestamp),
                ev.Category,
                ev.Description,
                ev.PrevHash);
            return HashUtility.Sha256OfText(payload);
        }

        public bool VerifyIntegrity()
        {
            string prevHash = new string('0', 64);
            for (int i = 0; i < Events.Count; i++)
            {
                var ev = Events[i];
                if (ev.PrevHash != prevHash) return false;
                if (ev.EventHash != ComputeEventHash(ev)) return false;
                prevHash = ev.EventHash;
            }
            return true;
        }

        public int EventCount { get { return Events.Count; } }

        public string FinalHash
        {
            get
            {
                return Events.Count == 0
                    ? new string('0', 64)
                    : Events[Events.Count - 1].EventHash;
            }
        }

        private static string GenerateId()
        {
            string g = Guid.NewGuid().ToString("N").ToUpperInvariant();
            return g.Substring(0, 16);
        }

        public string ToHumanReadable()
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================");
            sb.AppendLine("  SHIELDBLOCK - SESSAO PERICIAL");
            sb.AppendLine("================================================================");
            sb.AppendLine("Session ID    : " + SessionId);
            sb.AppendLine("Iniciada em   : " + Iso8601.FormatFriendly(StartedAt));
            sb.AppendLine("Operador      : " + OperatorUser + " @ " + MachineName);
            sb.AppendLine("Perito        : " + Perito.Nome);
            sb.AppendLine("CPF / Registro: " + Perito.CPF + " / " + Perito.Registro);
            sb.AppendLine("Orgao         : " + Perito.Orgao);
            sb.AppendLine("Eventos       : " + Events.Count);
            sb.AppendLine("Hash final    : " + FinalHash);
            sb.AppendLine("Integridade   : " + (VerifyIntegrity() ? "OK" : "VIOLADA"));
            sb.AppendLine("================================================================");
            sb.AppendLine();
            sb.AppendLine("AUDIT TRAIL:");
            sb.AppendLine("----------------------------------------------------------------");
            foreach (var ev in Events)
            {
                sb.AppendLine(string.Format("[#{0:D4}] [{1}] [{2}]",
                    ev.Sequence,
                    Iso8601.FormatFriendly(ev.Timestamp),
                    ev.Category));
                sb.AppendLine("        " + ev.Description);
                sb.AppendLine("        hash: " + ev.EventHash.Substring(0, 16) + "...");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }

    public class AuditEvent
    {
        public int      Sequence    { get; set; }
        public DateTime Timestamp   { get; set; }
        public string   Category    { get; set; }
        public string   Description { get; set; }
        public string   PrevHash    { get; set; }
        public string   EventHash   { get; set; }
    }
}

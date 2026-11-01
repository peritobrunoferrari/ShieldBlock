using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ShieldBlock.Core
{
    /// <summary>
    /// Captura snapshot dos processos ativos para o audit trail.
    /// (Detectar handles abertos especificos por drive exige Win32 API
    /// complexa; aqui registramos top processos por memoria como contexto.)
    /// </summary>
    public class ProcessAuditor
    {
        private const int TopN = 10;
        public event Action<string, string> OnEvent;

        public void Snapshot()
        {
            try
            {
                Process[] all;
                try { all = Process.GetProcesses(); }
                catch (Exception ex)
                {
                    Raise("PROC_AUDIT_ERROR", "GetProcesses falhou: " + ex.Message);
                    return;
                }

                var top = all
                    .Where(p => SafeOk(p))
                    .OrderByDescending(p => SafeMem(p))
                    .Take(TopN)
                    .ToList();

                var sb = new StringBuilder();
                sb.Append("Top " + top.Count + " processos: ");
                for (int i = 0; i < top.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(SafeName(top[i]))
                      .Append("(pid=").Append(top[i].Id)
                      .Append(", mem=").Append((SafeMem(top[i]) / (1024 * 1024)))
                      .Append("MB)");
                }
                sb.Append("  [total=" + all.Length + "]");
                Raise("PROC_SNAPSHOT", sb.ToString());

                foreach (var p in all) { try { p.Dispose(); } catch { } }
            }
            catch (Exception ex)
            {
                Raise("PROC_AUDIT_ERROR", ex.Message);
            }
        }

        private static bool SafeOk(Process p)
        {
            try { var _ = p.Id; return true; } catch { return false; }
        }

        private static long SafeMem(Process p)
        {
            try { return p.WorkingSet64; } catch { return 0; }
        }

        private static string SafeName(Process p)
        {
            try { return p.ProcessName; } catch { return "?"; }
        }

        private void Raise(string cat, string desc)
        {
            var h = OnEvent;
            if (h != null) { try { h(cat, desc); } catch { } }
        }
    }
}

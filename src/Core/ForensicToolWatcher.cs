using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace ShieldBlock.Core
{
    /// <summary>
    /// v6.5: Monitora se ferramentas forenses conhecidas (FTK Imager, Autopsy,
    /// X-Ways, EnCase, dd, HxD, etc.) sao iniciadas durante a sessao pericial.
    ///
    /// Usa WMI Win32_ProcessStartTrace para detectar inicio de processos
    /// em tempo real (precisa privilegios admin). Fallback: snapshot periodico
    /// via Process.GetProcesses() chamado pelo heartbeat.
    /// </summary>
    public class ForensicToolWatcher : IDisposable
    {
        private ManagementEventWatcher _startWatcher;
        private ManagementEventWatcher _stopWatcher;
        private bool _disposed;
        private readonly HashSet<int> _knownPids = new HashSet<int>();
        private readonly object _lock = new object();

        public event Action<string, string> OnEvent;

        // Catalogo de processos forenses conhecidos (case-insensitive)
        // Compare com o nome do executavel (sem .exe)
        private static readonly Dictionary<string, string> KnownTools =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // FTK family
            { "ftk imager",        "AccessData FTK Imager (preview/aquisicao)" },
            { "ftk",               "AccessData FTK" },
            { "adimagecmd",        "FTK Imager CLI" },

            // Autopsy / Sleuth Kit
            { "autopsy",           "Autopsy (Sleuth Kit)" },
            { "autopsy64",         "Autopsy x64" },
            { "tsk_recover",       "Sleuth Kit - tsk_recover" },
            { "fls",               "Sleuth Kit - fls" },
            { "icat",              "Sleuth Kit - icat" },

            // X-Ways / WinHex
            { "winhex",            "X-Ways WinHex" },
            { "xways",             "X-Ways Forensics" },
            { "x-ways forensics",  "X-Ways Forensics" },

            // EnCase
            { "encase",            "Guidance EnCase" },
            { "en",                "EnCase" },

            // Magnet Forensics
            { "axiom",             "Magnet AXIOM" },
            { "axiomprocess",      "Magnet AXIOM Process" },
            { "axiomexamine",      "Magnet AXIOM Examine" },
            { "magnetimage",       "Magnet Acquire" },

            // Cellebrite
            { "ufed",              "Cellebrite UFED" },
            { "ufed4pc",           "Cellebrite UFED 4PC" },
            { "physicalanalyzer",  "Cellebrite Physical Analyzer" },

            // OSForensics / Passmark
            { "osforensics",       "PassMark OSForensics" },

            // Belkasoft
            { "belkasoft",         "Belkasoft Evidence Center" },

            // Outras tools
            { "dd",                "GNU dd (raw disk imaging)" },
            { "dcfldd",            "dcfldd (forensic dd)" },
            { "dc3dd",             "dc3dd (DoD forensic dd)" },
            { "hxd",               "HxD Hex Editor" },
            { "winfe",             "Windows Forensic Environment" },
            { "wireshark",         "Wireshark (analise de rede)" },
            { "tshark",            "TShark (CLI)" },
            { "procmon",           "Sysinternals Process Monitor" },
            { "procmon64",         "Sysinternals Process Monitor x64" },
            { "procexp",           "Sysinternals Process Explorer" },
            { "procexp64",         "Sysinternals Process Explorer x64" },
            { "regshot",           "Regshot (snapshot de Registry)" },
            { "volatility",        "Volatility (memory forensics)" },
            { "volatility3",       "Volatility 3" },
            { "vol",               "Volatility (alias)" },
            { "rekall",            "Rekall (memory forensics)" },
            { "memoryze",          "Mandiant Memoryze" },
            { "redline",           "Mandiant Redline" },
            { "kape",              "KAPE (Kroll Artifact Parser)" },
            { "registryexplorer",  "Eric Zimmerman RegistryExplorer" },
            { "timeline explorer", "Eric Zimmerman Timeline Explorer" },
            { "kapegui",           "KAPE GUI" },
            { "imageusb",          "PassMark ImageUSB" },
            { "rufus",             "Rufus (USB imager)" },
            { "win32diskimager",   "Win32 Disk Imager" },
            { "diskgenius",        "DiskGenius" },
            { "r-studio",          "R-Studio (recovery)" },
            { "recuva",            "Piriform Recuva" },
            { "photorec",          "TestDisk PhotoRec" },
            { "testdisk",          "TestDisk" },

            // Anti-forense (importante registrar!)
            { "ccleaner",          "CCleaner (atencao: pode apagar evidencias)" },
            { "bleachbit",         "BleachBit (atencao: pode apagar evidencias)" },
            { "eraser",            "Eraser (atencao: secure deletion)" },
            { "veracrypt",         "VeraCrypt (encrypted volumes)" },
            { "truecrypt",         "TrueCrypt (encrypted volumes)" }
        };

        public void Start()
        {
            try
            {
                // Snapshot inicial - registra ferramentas ja em execucao
                ScanRunning(true);

                // WMI: evento de inicio de processo
                _startWatcher = new ManagementEventWatcher(
                    new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
                _startWatcher.EventArrived += OnProcessStart;
                _startWatcher.Start();

                _stopWatcher = new ManagementEventWatcher(
                    new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
                _stopWatcher.EventArrived += OnProcessStop;
                _stopWatcher.Start();

                Raise("FORENSIC_WATCHER",
                    "Monitor de ferramentas forenses iniciado ("
                    + KnownTools.Count + " ferramentas catalogadas)");
            }
            catch (Exception ex)
            {
                Raise("FORENSIC_WATCHER_ERROR",
                    "WMI Win32_ProcessStartTrace falhou (precisa admin): " + ex.Message
                    + "  -- usando fallback de snapshot");
                _startWatcher = null;
                _stopWatcher = null;
            }
        }

        /// <summary>
        /// Fallback: chamar do heartbeat. Compara snapshot atual com PIDs conhecidos.
        /// </summary>
        public void TickScan()
        {
            ScanRunning(false);
        }

        private void ScanRunning(bool silent)
        {
            try
            {
                var currentPids = new HashSet<int>();
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        string name = p.ProcessName;
                        currentPids.Add(p.Id);
                        if (IsForensicTool(name) && !_knownPids.Contains(p.Id))
                        {
                            _knownPids.Add(p.Id);
                            if (!silent)
                                RaiseStarted(name, p.Id);
                            else
                                Raise("FORENSIC_BASELINE",
                                    "Ferramenta forense ja em execucao: " + name
                                    + " (PID " + p.Id + ") - "
                                    + GetDescription(name));
                        }
                    }
                    catch { }
                    finally { try { p.Dispose(); } catch { } }
                }

                // Limpa PIDs que sumiram
                lock (_lock)
                {
                    var gone = new List<int>();
                    foreach (var pid in _knownPids)
                        if (!currentPids.Contains(pid)) gone.Add(pid);
                    foreach (var pid in gone) _knownPids.Remove(pid);
                }
            }
            catch (Exception ex)
            {
                Raise("FORENSIC_WATCHER_ERROR", "ScanRunning: " + ex.Message);
            }
        }

        private void OnProcessStart(object sender, EventArrivedEventArgs e)
        {
            try
            {
                string name = Convert.ToString(e.NewEvent["ProcessName"]);
                int pid = 0;
                try { pid = Convert.ToInt32(e.NewEvent["ProcessID"]); } catch { }
                if (string.IsNullOrEmpty(name)) return;
                string nameNoExe = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? name.Substring(0, name.Length - 4) : name;

                if (IsForensicTool(nameNoExe))
                {
                    lock (_lock) { _knownPids.Add(pid); }
                    RaiseStarted(nameNoExe, pid);
                }
            }
            catch { }
        }

        private void OnProcessStop(object sender, EventArrivedEventArgs e)
        {
            try
            {
                string name = Convert.ToString(e.NewEvent["ProcessName"]);
                int pid = 0;
                try { pid = Convert.ToInt32(e.NewEvent["ProcessID"]); } catch { }
                if (string.IsNullOrEmpty(name)) return;
                string nameNoExe = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? name.Substring(0, name.Length - 4) : name;

                if (IsForensicTool(nameNoExe))
                {
                    lock (_lock) { _knownPids.Remove(pid); }
                    Raise("FORENSIC_TOOL_STOPPED",
                        "Ferramenta encerrada: " + nameNoExe + " (PID " + pid + ") - "
                        + GetDescription(nameNoExe));
                }
            }
            catch { }
        }

        private void RaiseStarted(string name, int pid)
        {
            Raise("FORENSIC_TOOL_STARTED",
                "*** FERRAMENTA FORENSE INICIADA: " + name + ".exe (PID " + pid + ") -- "
                + GetDescription(name) + " ***");
        }

        private static bool IsForensicTool(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            return KnownTools.ContainsKey(processName);
        }

        private static string GetDescription(string processName)
        {
            string d;
            if (KnownTools.TryGetValue(processName, out d)) return d;
            return "ferramenta forense";
        }

        public IEnumerable<string> ListKnownTools()
        {
            return KnownTools.Select(kv => kv.Key + " -> " + kv.Value);
        }

        private void Raise(string cat, string desc)
        {
            var h = OnEvent;
            if (h != null) { try { h(cat, desc); } catch { } }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { if (_startWatcher != null) { _startWatcher.Stop(); _startWatcher.Dispose(); } } catch { }
            try { if (_stopWatcher  != null) { _stopWatcher.Stop();  _stopWatcher.Dispose();  } } catch { }
            _startWatcher = null;
            _stopWatcher = null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Management;

namespace ShieldBlock.Core
{
    /// <summary>
    /// Detecta insercao/remocao de drives em tempo real.
    /// v6.5: alem do WMI Win32_VolumeChangeEvent, mantem snapshot de drives
    /// para identificar QUAL drive foi inserido/removido com letra, label,
    /// filesystem, tamanho, modelo e serial.
    /// </summary>
    public class UsbWatcher : IDisposable
    {
        private ManagementEventWatcher _watcher;
        private bool _disposed;
        private Dictionary<string, DriveSnapshot> _lastSnapshot
            = new Dictionary<string, DriveSnapshot>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();

        public event Action<string, string> OnEvent;

        private class DriveSnapshot
        {
            public string Letter;
            public string Label;
            public string FileSystem;
            public string DriveType;
            public long   TotalBytes;
            public long   FreeBytes;
        }

        public void Start()
        {
            if (_watcher != null) return;
            try
            {
                // Snapshot inicial - sem disparar evento
                _lastSnapshot = TakeSnapshot();

                var query = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent");
                _watcher = new ManagementEventWatcher(query);
                _watcher.EventArrived += HandleArrived;
                _watcher.Start();
                Raise("USB_WATCHER", "Monitor USB iniciado (snapshot: "
                    + _lastSnapshot.Count + " drives conhecidos)");
            }
            catch (Exception ex)
            {
                Raise("USB_WATCHER_ERROR", "Falha ao iniciar monitor USB: " + ex.Message);
                _watcher = null;
            }
        }

        private void HandleArrived(object sender, EventArrivedEventArgs e)
        {
            try
            {
                ushort type = 0;
                try { type = Convert.ToUInt16(e.NewEvent["EventType"]); } catch { }

                // Diff entre snapshot atual e anterior
                lock (_lock)
                {
                    var current = TakeSnapshot();
                    var added = new List<DriveSnapshot>();
                    var removed = new List<DriveSnapshot>();

                    foreach (var kv in current)
                        if (!_lastSnapshot.ContainsKey(kv.Key)) added.Add(kv.Value);
                    foreach (var kv in _lastSnapshot)
                        if (!current.ContainsKey(kv.Key)) removed.Add(kv.Value);

                    foreach (var d in added)
                    {
                        string model = TryGetPhysicalInfo(d.Letter);
                        Raise("USB_INSERTED",
                            "Drive [" + d.Letter + "] INSERIDO  |  Rotulo: \""
                            + d.Label + "\"  |  FS: " + d.FileSystem
                            + "  |  Tipo: " + d.DriveType
                            + "  |  Tamanho: " + FormatBytes(d.TotalBytes)
                            + "  |  Livre: " + FormatBytes(d.FreeBytes)
                            + (string.IsNullOrEmpty(model) ? "" : "  |  " + model));
                    }
                    foreach (var d in removed)
                    {
                        Raise("USB_REMOVED",
                            "Drive [" + d.Letter + "] REMOVIDO  |  Rotulo: \""
                            + d.Label + "\"  |  Era " + d.DriveType
                            + " " + FormatBytes(d.TotalBytes));
                    }

                    // Se nao houve diff mas WMI disparou, eh um change de config
                    if (added.Count == 0 && removed.Count == 0 && type == 1)
                    {
                        Raise("USB_CONFIG_CHANGED",
                            "Configuracao de volume alterada (sem mudanca de drive)");
                    }

                    _lastSnapshot = current;
                }
            }
            catch (Exception ex)
            {
                Raise("USB_WATCHER_ERROR", "Erro processando evento: " + ex.Message);
            }
        }

        private Dictionary<string, DriveSnapshot> TakeSnapshot()
        {
            var dict = new Dictionary<string, DriveSnapshot>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    var s = new DriveSnapshot
                    {
                        Letter = d.Name, DriveType = d.DriveType.ToString()
                    };
                    if (d.IsReady)
                    {
                        try
                        {
                            s.Label      = d.VolumeLabel ?? "";
                            s.FileSystem = d.DriveFormat ?? "";
                            s.TotalBytes = d.TotalSize;
                            s.FreeBytes  = d.AvailableFreeSpace;
                        }
                        catch { }
                    }
                    dict[d.Name] = s;
                }
            }
            catch { }
            return dict;
        }

        /// <summary>
        /// Tenta descobrir modelo + serial do disco fisico que contem essa letra.
        /// </summary>
        private string TryGetPhysicalInfo(string driveLetter)
        {
            try
            {
                string letter = driveLetter.TrimEnd('\\').TrimEnd(':') + ":";
                using (var s1 = new ManagementObjectSearcher(
                    "ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='" + letter + "'} "
                    + "WHERE AssocClass=Win32_LogicalDiskToPartition"))
                using (var r1 = s1.Get())
                {
                    foreach (ManagementObject part in r1)
                    {
                        using (var s2 = new ManagementObjectSearcher(
                            "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='"
                            + part["DeviceID"] + "'} "
                            + "WHERE AssocClass=Win32_DiskDriveToDiskPartition"))
                        using (var r2 = s2.Get())
                        {
                            foreach (ManagementObject disk in r2)
                            {
                                string model = SafeStr(disk, "Model");
                                string serial = SafeStr(disk, "SerialNumber");
                                string iface = SafeStr(disk, "InterfaceType");
                                return "Modelo: " + model
                                    + (string.IsNullOrEmpty(serial) ? "" : "  |  Serial: " + serial)
                                    + (string.IsNullOrEmpty(iface) ? "" : "  |  " + iface);
                            }
                        }
                    }
                }
            }
            catch { }
            return "";
        }

        private static string SafeStr(ManagementBaseObject o, string key)
        {
            try { var v = o[key]; return v == null ? "" : v.ToString().Trim(); }
            catch { return ""; }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "?";
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
            double size = bytes;
            int idx = 0;
            while (size >= 1024 && idx < units.Length - 1)
            {
                size /= 1024; idx++;
            }
            return string.Format("{0:0.##} {1}", size, units[idx]);
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
            try
            {
                if (_watcher != null)
                {
                    _watcher.Stop();
                    _watcher.Dispose();
                    _watcher = null;
                }
            }
            catch { }
        }
    }
}

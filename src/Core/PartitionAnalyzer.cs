using System;
using System.Collections.Generic;
using System.Management;

namespace ShieldBlock.Core
{
    /// <summary>
    /// Análise de partições via WMI (Win32_DiskDrive / Win32_DiskPartition).
    /// Fornece dados forenses: offset, tamanho de bloco, número de blocos, serial do volume.
    /// </summary>
    public class PartitionAnalyzer
    {
        public List<DiskRecord> EnumerateDisks()
        {
            var disks = new List<DiskRecord>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        disks.Add(new DiskRecord
                        {
                            DeviceID    = SafeStr(obj, "DeviceID"),
                            Model       = SafeStr(obj, "Model"),
                            InterfaceType = SafeStr(obj, "InterfaceType"),
                            MediaType   = SafeStr(obj, "MediaType"),
                            SerialNumber = SafeStr(obj, "SerialNumber"),
                            SizeBytes   = SafeLong(obj, "Size"),
                            Partitions  = SafeInt(obj, "Partitions")
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                disks.Add(new DiskRecord
                {
                    DeviceID = "(erro WMI)",
                    Model = ex.Message
                });
            }
            return disks;
        }

        public List<PartitionRecord> EnumeratePartitions()
        {
            var parts = new List<PartitionRecord>();
            try
            {
                // Mapa: partição → letra lógica
                var partToLogical = new Dictionary<string, string>();
                using (var s = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_LogicalDiskToPartition"))
                using (var rs = s.Get())
                {
                    foreach (ManagementObject o in rs)
                    {
                        string ant = SafeStr(o, "Antecedent");
                        string dep = SafeStr(o, "Dependent");
                        string partName = ExtractDeviceID(ant);
                        string logical  = ExtractDeviceID(dep);
                        if (!string.IsNullOrEmpty(partName))
                            partToLogical[partName] = logical;
                    }
                }

                // Mapa: letra → filesystem & serial
                var logicalInfo = new Dictionary<string, string[]>();
                using (var s = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk"))
                using (var rs = s.Get())
                {
                    foreach (ManagementObject o in rs)
                    {
                        string dev = SafeStr(o, "DeviceID");
                        if (!string.IsNullOrEmpty(dev))
                            logicalInfo[dev] = new string[]
                            {
                                SafeStr(o, "FileSystem"),
                                SafeStr(o, "VolumeSerialNumber")
                            };
                    }
                }

                using (var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_DiskPartition"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        string name = SafeStr(obj, "Name");
                        string logical = partToLogical.ContainsKey(name)
                            ? partToLogical[name] : "—";

                        string fs = "—", serial = "—";
                        if (!string.IsNullOrEmpty(logical) && logicalInfo.ContainsKey(logical))
                        {
                            fs     = logicalInfo[logical][0];
                            serial = logicalInfo[logical][1];
                        }

                        parts.Add(new PartitionRecord
                        {
                            DiskIndex      = SafeInt(obj, "DiskIndex"),
                            Index          = SafeInt(obj, "Index"),
                            StartingOffset = SafeLong(obj, "StartingOffset"),
                            SizeBytes      = SafeLong(obj, "Size"),
                            BlockSize      = SafeLong(obj, "BlockSize"),
                            NumberOfBlocks = SafeLong(obj, "NumberOfBlocks"),
                            LogicalDisk    = string.IsNullOrEmpty(logical) ? "—" : logical,
                            FileSystem     = string.IsNullOrEmpty(fs) ? "—" : fs,
                            VolumeSerial   = string.IsNullOrEmpty(serial) ? "—" : serial,
                            Type           = SafeStr(obj, "Type"),
                            Bootable       = SafeBool(obj, "Bootable")
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                parts.Add(new PartitionRecord
                {
                    LogicalDisk = "(erro WMI)",
                    FileSystem  = ex.Message
                });
            }
            return parts;
        }

        // ============= HELPERS =============
        private static string SafeStr(ManagementBaseObject o, string key)
        {
            try { var v = o[key]; return v == null ? "" : v.ToString().Trim(); }
            catch { return ""; }
        }
        private static long SafeLong(ManagementBaseObject o, string key)
        {
            try { var v = o[key]; return v == null ? 0L : Convert.ToInt64(v); }
            catch { return 0L; }
        }
        private static int SafeInt(ManagementBaseObject o, string key)
        {
            try { var v = o[key]; return v == null ? 0 : Convert.ToInt32(v); }
            catch { return 0; }
        }
        private static bool SafeBool(ManagementBaseObject o, string key)
        {
            try { var v = o[key]; return v != null && Convert.ToBoolean(v); }
            catch { return false; }
        }
        private static string ExtractDeviceID(string wmiPath)
        {
            if (string.IsNullOrEmpty(wmiPath)) return "";
            int idx = wmiPath.IndexOf("DeviceID=\"", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return "";
            int start = idx + "DeviceID=\"".Length;
            int end = wmiPath.IndexOf('"', start);
            if (end < 0) return "";
            return wmiPath.Substring(start, end - start).Replace("\\\\", "\\");
        }
    }

    public class DiskRecord
    {
        public string DeviceID      { get; set; }
        public string Model         { get; set; }
        public string InterfaceType { get; set; }
        public string MediaType     { get; set; }
        public string SerialNumber  { get; set; }
        public long   SizeBytes     { get; set; }
        public int    Partitions    { get; set; }

        public string SizeReadable { get { return DriveDetector.FormatBytes(SizeBytes); } }
    }

    public class PartitionRecord
    {
        public int  DiskIndex      { get; set; }
        public int  Index          { get; set; }
        public long StartingOffset { get; set; }
        public long SizeBytes      { get; set; }
        public long BlockSize      { get; set; }
        public long NumberOfBlocks { get; set; }
        public string LogicalDisk  { get; set; }
        public string FileSystem   { get; set; }
        public string VolumeSerial { get; set; }
        public string Type         { get; set; }
        public bool   Bootable     { get; set; }

        public string SizeReadable { get { return DriveDetector.FormatBytes(SizeBytes); } }
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace ShieldBlock.Core
{
    /// <summary>
    /// Detecção de drives lógicos do sistema.
    /// </summary>
    public class DriveDetector
    {
        public List<DriveRecord> EnumerateAll()
        {
            var list = new List<DriveRecord>();
            DriveInfo[] drives;
            try { drives = DriveInfo.GetDrives(); }
            catch { return list; }

            foreach (DriveInfo d in drives)
            {
                var rec = new DriveRecord
                {
                    Letter     = d.Name,
                    DriveType  = d.DriveType.ToString(),
                    IsReady    = d.IsReady
                };
                if (d.IsReady)
                {
                    try
                    {
                        rec.Label        = d.VolumeLabel ?? "(sem rótulo)";
                        rec.FileSystem   = d.DriveFormat ?? "";
                        rec.TotalBytes   = d.TotalSize;
                        rec.FreeBytes    = d.AvailableFreeSpace;
                    }
                    catch (Exception ex)
                    {
                        rec.Label = "(erro: " + ex.Message + ")";
                    }
                }
                else
                {
                    rec.Label = "(não pronto)";
                }
                list.Add(rec);
            }
            return list;
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "—";
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
            double size = bytes;
            int idx = 0;
            while (size >= 1024 && idx < units.Length - 1)
            {
                size /= 1024;
                idx++;
            }
            return string.Format("{0:0.##} {1}", size, units[idx]);
        }
    }

    public class DriveRecord
    {
        public string Letter      { get; set; }
        public string Label       { get; set; }
        public string FileSystem  { get; set; }
        public string DriveType   { get; set; }
        public bool   IsReady     { get; set; }
        public long   TotalBytes  { get; set; }
        public long   FreeBytes   { get; set; }

        public string TotalReadable { get { return DriveDetector.FormatBytes(TotalBytes); } }
        public string FreeReadable  { get { return DriveDetector.FormatBytes(FreeBytes); } }
    }
}

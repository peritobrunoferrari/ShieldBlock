using System;
using System.IO;
using System.Management;
using System.Security.Principal;
using Microsoft.Win32;

namespace ShieldBlock.Utils
{
    /// <summary>
    /// Captura fingerprint imutavel da maquina e do usuario.
    /// Tudo aqui eh somente-leitura e deveria ser identico em runs subsequentes
    /// no mesmo PC/usuario.
    /// </summary>
    public static class MachineFingerprint
    {
        public class Snapshot
        {
            public string HostName        { get; set; }
            public string UserName        { get; set; }
            public string UserSid         { get; set; }
            public string MachineGuid     { get; set; }
            public string BiosSerial      { get; set; }
            public string OsVersion       { get; set; }
            public string FrameworkVersion { get; set; }
            public string ExecutablePath  { get; set; }
            public string ExecutableHash  { get; set; }
        }

        public static Snapshot Capture()
        {
            var s = new Snapshot();
            s.HostName        = SafeGet(() => Environment.MachineName);
            s.UserName        = SafeGet(() => Environment.UserName);
            s.UserSid         = SafeGet(GetUserSid);
            s.MachineGuid     = SafeGet(GetMachineGuid);
            s.BiosSerial      = SafeGet(GetBiosSerial);
            s.OsVersion       = SafeGet(() => Environment.OSVersion.VersionString);
            s.FrameworkVersion = SafeGet(() => Environment.Version.ToString());
            s.ExecutablePath  = SafeGet(GetExePath);
            s.ExecutableHash  = SafeGet(() => HashUtility.Sha256OfFile(GetExePath()));
            return s;
        }

        private static string GetUserSid()
        {
            using (var id = WindowsIdentity.GetCurrent())
            {
                if (id.User == null) return "(null)";
                return id.User.Value;
            }
        }

        private static string GetMachineGuid()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Cryptography", false))
            {
                if (key == null) return "(unavailable)";
                object v = key.GetValue("MachineGuid");
                return v == null ? "(unavailable)" : v.ToString();
            }
        }

        private static string GetBiosSerial()
        {
            using (var s = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
            using (var r = s.Get())
            {
                foreach (ManagementObject o in r)
                {
                    object v = o["SerialNumber"];
                    if (v != null) return v.ToString().Trim();
                }
            }
            return "(unavailable)";
        }

        private static string GetExePath()
        {
            var asm = System.Reflection.Assembly.GetEntryAssembly()
                   ?? System.Reflection.Assembly.GetExecutingAssembly();
            return asm.Location;
        }

        private static string SafeGet(Func<string> f)
        {
            try { return f() ?? "(null)"; }
            catch (Exception ex) { return "(err: " + ex.Message + ")"; }
        }

        public static string ToTextBlock(Snapshot s)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Hostname        : " + s.HostName);
            sb.AppendLine("Usuario Windows : " + s.UserName);
            sb.AppendLine("Windows SID     : " + s.UserSid);
            sb.AppendLine("Machine GUID    : " + s.MachineGuid);
            sb.AppendLine("BIOS Serial     : " + s.BiosSerial);
            sb.AppendLine("OS Version      : " + s.OsVersion);
            sb.AppendLine("CLR Version     : " + s.FrameworkVersion);
            sb.AppendLine("Executavel      : " + s.ExecutablePath);
            sb.AppendLine("SHA-256 do .exe : " + s.ExecutableHash);
            return sb.ToString();
        }
    }
}

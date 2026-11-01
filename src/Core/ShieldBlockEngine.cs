using System;
using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace ShieldBlock.Core
{
    /// <summary>
    /// Motor de bloqueio de escrita USB e controle de AutoMount.
    /// Requer privilegios de administrador.
    ///
    /// v6.5 fix: AutoMount agora usa DUAS camadas:
    ///  1. Registry (HKLM\SYSTEM\...\mountmgr\NoAutoMount) - persistente
    ///  2. mountvol.exe /N ou /E - efeito IMEDIATO em drives ja conectados
    ///
    /// Motivo: apenas o Registry nao afeta drives ja descobertos - so aplica
    /// a volumes novos apos reboot ou reinicio do servico mountmgr.
    /// O mountvol.exe eh a ferramenta oficial da Microsoft e aplica na hora.
    /// </summary>
    public class ShieldBlockEngine
    {
        private const string WriteProtectKey =
            @"SYSTEM\CurrentControlSet\Control\StorageDevicePolicies";
        private const string MountMgrKey =
            @"SYSTEM\CurrentControlSet\Services\mountmgr";

        public event Action<string, string> OnAction; // (categoria, descricao)

        // ============= WRITE BLOCK =============
        public bool IsWriteBlocked()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(WriteProtectKey, false))
                {
                    if (key == null) return false;
                    object val = key.GetValue("WriteProtect");
                    return val != null && Convert.ToInt32(val) == 1;
                }
            }
            catch { return false; }
        }

        public bool EnableWriteBlock()
        {
            try
            {
                using (var key = Registry.LocalMachine.CreateSubKey(WriteProtectKey))
                {
                    if (key == null) return false;
                    key.SetValue("WriteProtect", 1, RegistryValueKind.DWord);
                }
                Raise("WRITE_BLOCK", "Write Protection HABILITADA via Registry");
                return true;
            }
            catch (Exception ex)
            {
                Raise("ERROR", "Falha ao habilitar Write Block: " + ex.Message);
                return false;
            }
        }

        public bool DisableWriteBlock()
        {
            try
            {
                using (var key = Registry.LocalMachine.CreateSubKey(WriteProtectKey))
                {
                    if (key == null) return false;
                    key.SetValue("WriteProtect", 0, RegistryValueKind.DWord);
                }
                Raise("WRITE_BLOCK", "Write Protection DESABILITADA via Registry");
                return true;
            }
            catch (Exception ex)
            {
                Raise("ERROR", "Falha ao desabilitar Write Block: " + ex.Message);
                return false;
            }
        }

        // ============= AUTOMOUNT =============
        /// <summary>
        /// Le o estado atual do AutoMount. Prioriza o comando "mountvol"
        /// (fonte da verdade em runtime), com fallback pro Registry.
        /// </summary>
        public bool IsAutoMountEnabled()
        {
            // 1. Tenta pelo mountvol (mais confiavel)
            try
            {
                string output = RunProcess("mountvol.exe", "");
                if (!string.IsNullOrEmpty(output))
                {
                    // A saida contem "Automatic mounting of new volumes enabled/disabled."
                    string lower = output.ToLowerInvariant();
                    if (lower.Contains("disabled")) return false;
                    if (lower.Contains("enabled"))  return true;
                }
            }
            catch { }

            // 2. Fallback: Registry
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(MountMgrKey, false))
                {
                    if (key == null) return true; // default Windows: habilitado
                    object val = key.GetValue("NoAutoMount");
                    return val == null || Convert.ToInt32(val) == 0;
                }
            }
            catch { return true; }
        }

        public bool DisableAutoMount()
        {
            bool registryOk = false;
            bool mountvolOk = false;
            string mountvolOutput = "";

            // 1. Registry (persistente apos reboot)
            try
            {
                using (var key = Registry.LocalMachine.CreateSubKey(MountMgrKey))
                {
                    if (key != null)
                    {
                        key.SetValue("NoAutoMount", 1, RegistryValueKind.DWord);
                        registryOk = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Raise("ERROR", "Registry NoAutoMount=1 falhou: " + ex.Message);
            }

            // 2. mountvol.exe /N (efeito IMEDIATO)
            try
            {
                mountvolOutput = RunProcess("mountvol.exe", "/N");
                mountvolOk = true;
            }
            catch (Exception ex)
            {
                Raise("ERROR", "mountvol /N falhou: " + ex.Message);
            }

            if (registryOk || mountvolOk)
            {
                string status = "";
                if (registryOk && mountvolOk) status = "OK (Registry + mountvol /N)";
                else if (registryOk)          status = "PARCIAL (so Registry, mountvol falhou)";
                else                          status = "PARCIAL (so mountvol, Registry falhou)";
                Raise("AUTOMOUNT", "AutoMount DESABILITADO - " + status
                    + (string.IsNullOrEmpty(mountvolOutput) ? ""
                       : " | mountvol: " + mountvolOutput.Trim()));
                return true;
            }
            return false;
        }

        public bool EnableAutoMount()
        {
            bool registryOk = false;
            bool mountvolOk = false;
            string mountvolOutput = "";

            try
            {
                using (var key = Registry.LocalMachine.CreateSubKey(MountMgrKey))
                {
                    if (key != null)
                    {
                        key.SetValue("NoAutoMount", 0, RegistryValueKind.DWord);
                        registryOk = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Raise("ERROR", "Registry NoAutoMount=0 falhou: " + ex.Message);
            }

            try
            {
                mountvolOutput = RunProcess("mountvol.exe", "/E");
                mountvolOk = true;
            }
            catch (Exception ex)
            {
                Raise("ERROR", "mountvol /E falhou: " + ex.Message);
            }

            if (registryOk || mountvolOk)
            {
                string status = "";
                if (registryOk && mountvolOk) status = "OK (Registry + mountvol /E)";
                else if (registryOk)          status = "PARCIAL (so Registry, mountvol falhou)";
                else                          status = "PARCIAL (so mountvol, Registry falhou)";
                Raise("AUTOMOUNT", "AutoMount HABILITADO - " + status
                    + (string.IsNullOrEmpty(mountvolOutput) ? ""
                       : " | mountvol: " + mountvolOutput.Trim()));
                return true;
            }
            return false;
        }


        /// <summary>
        /// Remove ATIVAMENTE a letra de um drive via mountvol X: /D.
        /// Use quando um drive foi remontado apesar do NoAutoMount
        /// (Windows lembra de drives ja vistos em MountedDevices).
        /// </summary>
        public bool UnmountDriveLetter(string letter)
        {
            if (string.IsNullOrEmpty(letter)) return false;
            // Normaliza letra: "E:\", "E:", "E" -> "E:"
            string L = letter.Trim().Trim('\\').Trim('/');
            if (L.EndsWith(":") == false) L += ":";
            if (L.Length != 2) return false;
            try
            {
                string output = RunProcess("mountvol.exe", L + " /D");
                Raise("AUTOMOUNT", "Drive " + L + " DESMONTADO forcadamente (mountvol " + L
                    + " /D)" + (string.IsNullOrEmpty(output.Trim()) ? "" : " | " + output.Trim()));
                return true;
            }
            catch (Exception ex)
            {
                Raise("ERROR", "mountvol " + L + " /D falhou: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// mountvol /R - Remove mount-point folders AND registry cache de
        /// volumes que nao estao atualmente montados. Limpa o "MountedDevices"
        /// que faz o Windows remontar drives ja conhecidos.
        /// </summary>
        public bool ClearMountedDevicesCache()
        {
            try
            {
                string output = RunProcess("mountvol.exe", "/R");
                Raise("AUTOMOUNT", "Cache de drives limpo (mountvol /R) - drives"
                    + " nao serao remontados automaticamente ao reconectar"
                    + (string.IsNullOrEmpty(output.Trim()) ? "" : " | " + output.Trim()));
                return true;
            }
            catch (Exception ex)
            {
                Raise("ERROR", "mountvol /R falhou: " + ex.Message);
                return false;
            }
        }

        // ============= HELPERS =============
        private static string RunProcess(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            var sb = new StringBuilder();
            using (var p = Process.Start(psi))
            {
                if (p == null) throw new Exception("Process.Start retornou null para " + exe);
                if (!p.WaitForExit(8000))
                {
                    try { p.Kill(); } catch { }
                    throw new Exception(exe + " timeout (>8s)");
                }
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                sb.Append(stdout);
                if (!string.IsNullOrEmpty(stderr)) sb.Append(" | ERR: ").Append(stderr);
                if (p.ExitCode != 0)
                    sb.Append(" | exit=").Append(p.ExitCode);
            }
            return sb.ToString();
        }

        private void Raise(string category, string description)
        {
            var handler = OnAction;
            if (handler != null) handler(category, description);
        }
    }
}

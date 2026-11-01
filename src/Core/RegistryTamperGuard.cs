using System;
using Microsoft.Win32;

namespace ShieldBlock.Core
{
    /// <summary>
    /// Re-le periodicamente as chaves de Registry do WriteBlock/AutoMount
    /// e dispara evento TAMPER_DETECTED se o valor mudou sem ter passado
    /// pelo Engine deste app.
    ///
    /// O estado esperado e mantido por SetExpected() - chamado pelo Engine
    /// sempre que faz uma alteracao legitima.
    /// </summary>
    public class RegistryTamperGuard
    {
        private const string WriteProtectKey =
            @"SYSTEM\CurrentControlSet\Control\StorageDevicePolicies";
        private const string MountMgrKey =
            @"SYSTEM\CurrentControlSet\Services\mountmgr";

        private int? _expectedWriteProtect;
        private int? _expectedNoAutoMount;

        public event Action<string, string> OnEvent;

        public void SetExpected(bool? writeBlocked, bool? autoMountBlocked)
        {
            if (writeBlocked.HasValue)
                _expectedWriteProtect = writeBlocked.Value ? 1 : 0;
            if (autoMountBlocked.HasValue)
                _expectedNoAutoMount = autoMountBlocked.Value ? 1 : 0;
        }

        public void Initialize()
        {
            int? wp = ReadValue(WriteProtectKey, "WriteProtect");
            int? na = ReadValue(MountMgrKey, "NoAutoMount");
            _expectedWriteProtect = wp;
            _expectedNoAutoMount  = na;
        }

        /// <summary>
        /// Roda 1 ciclo de verificacao (chamar no heartbeat).
        /// </summary>
        public void Check()
        {
            try
            {
                int? wp = ReadValue(WriteProtectKey, "WriteProtect");
                int? na = ReadValue(MountMgrKey, "NoAutoMount");

                if (_expectedWriteProtect.HasValue && wp.HasValue
                    && wp.Value != _expectedWriteProtect.Value)
                {
                    Raise("TAMPER_DETECTED",
                        "Valor WriteProtect alterado externamente! "
                        + "Esperado=" + _expectedWriteProtect.Value
                        + ", Encontrado=" + wp.Value);
                    _expectedWriteProtect = wp;
                }

                if (_expectedNoAutoMount.HasValue && na.HasValue
                    && na.Value != _expectedNoAutoMount.Value)
                {
                    Raise("TAMPER_DETECTED",
                        "Valor NoAutoMount alterado externamente! "
                        + "Esperado=" + _expectedNoAutoMount.Value
                        + ", Encontrado=" + na.Value);
                    _expectedNoAutoMount = na;
                }
            }
            catch (Exception ex)
            {
                Raise("TAMPER_GUARD_ERROR", "Erro no check: " + ex.Message);
            }
        }

        private static int? ReadValue(string keyPath, string valueName)
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(keyPath, false))
                {
                    if (key == null) return null;
                    object v = key.GetValue(valueName);
                    if (v == null) return null;
                    return Convert.ToInt32(v);
                }
            }
            catch { return null; }
        }

        private void Raise(string cat, string desc)
        {
            var h = OnEvent;
            if (h != null) { try { h(cat, desc); } catch { } }
        }
    }
}

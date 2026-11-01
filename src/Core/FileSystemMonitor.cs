using System;
using System.Collections.Generic;
using System.IO;

namespace ShieldBlock.Core
{
    /// <summary>
    /// Monitora alteracoes no sistema de arquivos dos drives nao-system.
    /// Util durante a coleta para detectar se algo tenta escrever no drive
    /// (mesmo com WriteBlock, alguns programas podem tentar - util saber).
    /// </summary>
    public class FileSystemMonitor : IDisposable
    {
        private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private bool _disposed;

        public event Action<string, string> OnEvent;

        /// <summary>
        /// Inicia watchers para todos os drives Fixed/Removable que nao sejam o do sistema.
        /// </summary>
        public void StartForAllDataDrives()
        {
            string systemDrive = Path.GetPathRoot(Environment.SystemDirectory)
                ?? "C:\\";
            DriveInfo[] drives;
            try { drives = DriveInfo.GetDrives(); }
            catch { return; }

            foreach (var d in drives)
            {
                try
                {
                    if (!d.IsReady) continue;
                    if (string.Equals(d.RootDirectory.FullName, systemDrive,
                                       StringComparison.OrdinalIgnoreCase)) continue;
                    if (d.DriveType != DriveType.Fixed
                        && d.DriveType != DriveType.Removable) continue;
                    StartForPath(d.RootDirectory.FullName);
                }
                catch { /* drive pode estar offline */ }
            }
        }

        public void StartForPath(string path)
        {
            try
            {
                var w = new FileSystemWatcher(path);
                w.IncludeSubdirectories = true;
                w.NotifyFilter = NotifyFilters.FileName
                               | NotifyFilters.DirectoryName
                               | NotifyFilters.LastWrite
                               | NotifyFilters.Size;
                w.InternalBufferSize = 64 * 1024;
                w.Created += (s, e) => Raise("FS_CREATED",  "[" + path + "] Criado: " + e.Name);
                w.Changed += (s, e) => Raise("FS_CHANGED",  "[" + path + "] Alterado: " + e.Name);
                w.Deleted += (s, e) => Raise("FS_DELETED",  "[" + path + "] Deletado: " + e.Name);
                w.Renamed += (s, e) => Raise("FS_RENAMED",
                    "[" + path + "] Renomeado: " + e.OldName + " -> " + e.Name);
                w.Error += (s, e) => Raise("FS_ERROR",
                    "[" + path + "] " + (e.GetException() == null ? "(?)" : e.GetException().Message));
                w.EnableRaisingEvents = true;
                _watchers.Add(w);
                Raise("FS_MONITOR_START", "Monitor ativo em: " + path);
            }
            catch (Exception ex)
            {
                Raise("FS_MONITOR_ERROR",
                    "Nao foi possivel monitorar " + path + ": " + ex.Message);
            }
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
            foreach (var w in _watchers)
            {
                try { w.EnableRaisingEvents = false; w.Dispose(); } catch { }
            }
            _watchers.Clear();
        }
    }
}

using System;
using System.Windows.Forms;

namespace ShieldBlock.Core
{
    /// <summary>
    /// Timer de heartbeat (default 30s). Dispara um Tick periodico que
    /// MainForm usa para: registrar HEARTBEAT, rodar RegistryTamperGuard,
    /// snapshot de processos e refresh de status.
    /// </summary>
    public class HeartbeatService : IDisposable
    {
        private Timer _timer;
        private int _intervalMs;
        private int _tickCount;
        private bool _disposed;

        public event Action OnTick;

        public int IntervalMs { get { return _intervalMs; } }
        public int TickCount  { get { return _tickCount; } }

        public HeartbeatService(int intervalMs)
        {
            _intervalMs = intervalMs > 0 ? intervalMs : 30000;
        }

        public void Start()
        {
            if (_timer != null) return;
            _timer = new Timer();
            _timer.Interval = _intervalMs;
            _timer.Tick += (s, e) =>
            {
                _tickCount++;
                var h = OnTick;
                if (h != null) { try { h(); } catch { } }
            };
            _timer.Start();
        }

        public void Stop()
        {
            if (_timer == null) return;
            try { _timer.Stop(); } catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Dispose();
                    _timer = null;
                }
            }
            catch { }
        }
    }
}

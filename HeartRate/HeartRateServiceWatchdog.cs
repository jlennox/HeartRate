using System;
using System.Diagnostics;
using System.Threading;

namespace HeartRate
{
    internal class HeartRateServiceWatchdog : IDisposable
    {
        private readonly TimeSpan _timeout;
        private readonly IHeartRateService _service;
        private readonly Stopwatch _lastUpdateTimer = Stopwatch.StartNew();
        private readonly object _sync = new object();
        private bool _isDisposed = false;

        public HeartRateServiceWatchdog(
            TimeSpan timeout,
            IHeartRateService service)
        {
            _timeout = timeout;
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _service.HeartRateUpdated += Service_HeartRateUpdated;

            var thread = new Thread(WatchdogThread)
            {
                Name = GetType().Name,
                IsBackground = true
            };

            thread.Start();
        }

        private void Service_HeartRateUpdated(HeartRateReading reading)
        {
            lock (_sync)
            {
                _lastUpdateTimer.Restart();
            }
        }

        private void WatchdogThread()
        {
            while (!_isDisposed && !_service.IsDisposed)
            {
                var needsRefresh = false;
                lock (_sync)
                {
                    if (_isDisposed)
                    {
                        return;
                    }

                    if (_lastUpdateTimer.Elapsed > _timeout)
                    {
                        needsRefresh = true;
                        _lastUpdateTimer.Restart();
                    }
                }

                if (needsRefresh)
                {
                    Debug.WriteLine("Restarting services...");
                    _service.InitiateDefault();
                }

                Thread.Sleep(10000);
            }

            Debug.WriteLine("Watchdog thread exiting.");
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _isDisposed = true;
            }
        }
    }
}

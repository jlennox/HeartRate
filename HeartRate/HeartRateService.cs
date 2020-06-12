using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace HeartRate
{
    enum ContactSensorStatus
    {
        NotSupported,
        NotSupported2,
        NoContact,
        Contact
    }

    internal interface IHeartRateService : IDisposable
    {
        bool IsDisposed { get; }

        event HeartRateService.HeartRateUpdateEventHandler HeartRateUpdated;
        void InitiateDefault();
        void Cleanup();
    }

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
            _service.HeartRateUpdated += _service_HeartRateUpdated;

            var thread = new Thread(WatchdogThread)
            {
                Name = GetType().Name,
                IsBackground = true
            };

            thread.Start();
        }

        private void _service_HeartRateUpdated(
            ContactSensorStatus status, int bpm)
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

    internal class HeartRateService : IHeartRateService
    {
        // https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.heart_rate_measurement.xml
        private const int _heartRateMeasurementCharacteristicId = 0x2A37;

        public bool IsDisposed => _isDisposed;

        private GattDeviceService _service;
        private readonly object _disposeSync = new object();
        private bool _isDisposed;

        public event HeartRateUpdateEventHandler HeartRateUpdated;
        public delegate void HeartRateUpdateEventHandler(ContactSensorStatus status, int bpm);

        public void InitiateDefault()
        {
            var heartrateSelector = GattDeviceService
                .GetDeviceSelectorFromUuid(GattServiceUuids.HeartRate);

            var devices = AsyncResult(DeviceInformation
                .FindAllAsync(heartrateSelector));

            var device = devices.FirstOrDefault();

            if (device == null)
            {
                throw new ArgumentNullException(
                    nameof(device),
                    "Unable to locate heart rate device.");
            }

            GattDeviceService service;

            lock (_disposeSync)
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }

                Cleanup();

                service = AsyncResult(GattDeviceService.FromIdAsync(device.Id));

                _service = service;
            }

            if (service == null)
            {
                throw new ArgumentOutOfRangeException(
                    $"Unable to get service to {device.Name} ({device.Id}). Is the device inuse by another program? The Bluetooth adaptor may need to be turned off and on again.");
            }

            var heartrate = service.GetCharacteristics(
                GattDeviceService.ConvertShortIdToUuid(
                    _heartRateMeasurementCharacteristicId))
                .FirstOrDefault();

            if (heartrate == null)
            {
                throw new ArgumentOutOfRangeException(
                    $"Unable to locate heart rate measurement on device {device.Name} ({device.Id}).");
            }

            var status = AsyncResult(
                heartrate.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify));

            heartrate.ValueChanged += HeartRate_ValueChanged;

            Debug.WriteLine($"Started {status}");
        }

        public void HeartRate_ValueChanged(
            GattCharacteristic sender,
            GattValueChangedEventArgs args)
        {
            var value = args.CharacteristicValue;

            if (value.Length == 0)
            {
                return;
            }

            using (var reader = DataReader.FromBuffer(value))
            {
                var bpm = -1;
                var flags = reader.ReadByte();
                var isshort = (flags & 1) == 1;
                var contactSensor = (ContactSensorStatus)((flags >> 1) & 3);
                var minLength = isshort ? 3 : 2;

                if (value.Length < minLength)
                {
                    Debug.WriteLine($"Buffer was too small. Got {value.Length}, expected {minLength}.");
                    return;
                }

                if (value.Length > 1)
                {
                    bpm = isshort
                        ? reader.ReadUInt16()
                        : reader.ReadByte();
                }

                Debug.WriteLine($"Read {flags:X} {contactSensor} {bpm}");

                HeartRateUpdated?.Invoke(contactSensor, bpm);
            }
        }

        public void Cleanup()
        {
            var service = Interlocked.Exchange(ref _service, null);

            if (service == null)
            {
                return;
            }

            try
            {
                service.Dispose();
            }
            catch { }
        }

        private static T AsyncResult<T>(IAsyncOperation<T> async)
        {
            while (true)
            {
                switch (async.Status)
                {
                    case AsyncStatus.Started:
                        Thread.Sleep(100);
                        continue;
                    case AsyncStatus.Completed:
                        return async.GetResults();
                    case AsyncStatus.Error:
                        throw async.ErrorCode;
                    case AsyncStatus.Canceled:
                        throw new TaskCanceledException();
                }
            }
        }

        public void Dispose()
        {
            lock (_disposeSync)
            {
                _isDisposed = true;

                Cleanup();
            }
        }
    }
}

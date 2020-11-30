using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace HeartRate
{
    internal enum ContactSensorStatus
    {
        NotSupported,
        NotSupported2,
        NoContact,
        Contact
    }

    [Flags]
    internal enum HeartRateFlags
    {
        None = 0,
        IsShort = 1,
        HasEnergyExpended = 1 << 3,
        HasRRInterval = 1 << 4,
    }

    internal struct HeartRateReading
    {
        public HeartRateFlags Flags { get; set; }
        public ContactSensorStatus Status { get; set; }
        public int BeatsPerMinute { get; set; }
        public int? EnergyExpended { get; set; }
        public int[] RRIntervals { get; set; }
    }

    internal interface IHeartRateService : IDisposable
    {
        bool IsDisposed { get; }

        event HeartRateService.HeartRateUpdateEventHandler HeartRateUpdated;
        void InitiateDefault();
        void Cleanup();
    }

    internal class HeartRateService : IHeartRateService
    {
        // https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.heart_rate_measurement.xml
        private const int _heartRateMeasurementCharacteristicId = 0x2A37;
        private static readonly Guid _heartRateMeasurementCharacteristicUuid =
            GattDeviceService.ConvertShortIdToUuid(_heartRateMeasurementCharacteristicId);

        public bool IsDisposed { get; private set; }

        private GattDeviceService _service;
        private byte[] _buffer;
        private readonly object _disposeSync = new object();

        public event HeartRateUpdateEventHandler HeartRateUpdated;
        public delegate void HeartRateUpdateEventHandler(HeartRateReading reading);

        public void InitiateDefault()
        {
            var heartrateSelector = GattDeviceService
                .GetDeviceSelectorFromUuid(GattServiceUuids.HeartRate);

            var devices = DeviceInformation
                .FindAllAsync(heartrateSelector)
                .AsyncResult();

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
                if (IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }

                Cleanup();

                service = GattDeviceService.FromIdAsync(device.Id)
                    .AsyncResult();

                _service = service;
            }

            if (service == null)
            {
                throw new ArgumentOutOfRangeException(
                    $"Unable to get service to {device.Name} ({device.Id}). Is the device inuse by another program? The Bluetooth adaptor may need to be turned off and on again.");
            }

            var heartrate = service
                .GetCharacteristics(_heartRateMeasurementCharacteristicUuid)
                .FirstOrDefault();

            if (heartrate == null)
            {
                throw new ArgumentOutOfRangeException(
                    $"Unable to locate heart rate measurement on device {device.Name} ({device.Id}).");
            }

            var status = heartrate
                .WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify)
                .AsyncResult();

            heartrate.ValueChanged += HeartRate_ValueChanged;

            Debug.WriteLine($"Started {status}");
        }

        public void HeartRate_ValueChanged(
            GattCharacteristic sender,
            GattValueChangedEventArgs args)
        {
            var buffer = args.CharacteristicValue;
            if (buffer.Length == 0) return;

            var byteBuffer = Interlocked.Exchange(ref _buffer, null)
                ?? new byte[buffer.Length];

            if (byteBuffer.Length != buffer.Length)
            {
                byteBuffer = new byte[buffer.Length];
            }

            try
            {
                using var reader = DataReader.FromBuffer(buffer);
                reader.ReadBytes(byteBuffer);

                var readingValue = ReadBuffer(byteBuffer, (int)buffer.Length);

                if (readingValue == null)
                {
                    Debug.WriteLine($"Buffer was too small. Got {buffer.Length}.");
                    return;
                }

                var reading = readingValue.Value;
                Debug.WriteLine($"Read {reading.Flags:X} {reading.Status} {reading.BeatsPerMinute}");

                HeartRateUpdated?.Invoke(reading);
            }
            finally
            {
                Volatile.Write(ref _buffer, byteBuffer);
            }
        }

        internal static HeartRateReading? ReadBuffer(byte[] buffer, int length)
        {
            if (length == 0) return null;

            var ms = new MemoryStream(buffer, 0, length);
            var flags = (HeartRateFlags)ms.ReadByte();
            var isshort = flags.HasFlag(HeartRateFlags.IsShort);
            var contactSensor = (ContactSensorStatus)(((int)flags >> 1) & 3);
            var hasEnergyExpended = flags.HasFlag(HeartRateFlags.HasEnergyExpended);
            var hasRRInterval = flags.HasFlag(HeartRateFlags.HasRRInterval);
            var minLength = isshort ? 3 : 2;

            if (buffer.Length < minLength) return null;

            var reading = new HeartRateReading
            {
                Flags = flags,
                Status = contactSensor,
                BeatsPerMinute = isshort ? ms.ReadUInt16() : ms.ReadByte()
            };

            if (hasEnergyExpended)
            {
                reading.EnergyExpended = ms.ReadUInt16();
            }

            if (hasRRInterval)
            {
                var rrvalueCount = (buffer.Length - ms.Position) / sizeof(ushort);
                var rrvalues = new int[rrvalueCount];
                for (var i = 0; i < rrvalueCount; ++i)
                {
                    rrvalues[i] = ms.ReadUInt16();
                }

                reading.RRIntervals = rrvalues;
            }

            return reading;
        }

        public void Cleanup()
        {
            var service = Interlocked.Exchange(ref _service, null);
            service.TryDispose();
        }

        public void Dispose()
        {
            lock (_disposeSync)
            {
                IsDisposed = true;

                Cleanup();
            }
        }
    }
}

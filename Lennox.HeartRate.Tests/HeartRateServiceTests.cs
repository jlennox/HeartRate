using HeartRate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.HeartRate.Tests
{
    [TestClass]
    public class HeartRateServiceTests
    {
        private static HeartRateReading? GetReading(params byte[] buf)
        {
            return HeartRateService.ReadBuffer(buf, buf.Length);
        }

        private static int GetBpm(params byte[] buf) => GetReading(buf).Value.BeatsPerMinute;
        private static int[] GetRR(params byte[] buf) => GetReading(buf).Value.RRIntervals;

        [TestMethod]
        public void ReturnsNullWhenTooShort()
        {
            Assert.IsNull(GetReading());
            // Says there's a short, but only gives a byte.
            Assert.IsNull(GetReading(0b00001, 0x12));
            // Says there's a byte, but there's nothing.
            Assert.IsNull(GetReading(0b00000));
        }

        [TestMethod]
        public void ReadsHeartRate()
        {
            // Finds 2byte value.
            Assert.AreEqual(0x0201, GetBpm(0b00001, 0x01, 0x02));
            // Single byte value.
            Assert.AreEqual(0x12, GetBpm(0b00000, 0x12));
        }

        [TestMethod]
        public void ReadsRRIntervals()
        {
            CollectionAssert.AreEqual(new[] { 0x0403, 0x0605 }, GetRR(0b10001, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06));
            CollectionAssert.AreEqual(new[] { 0x0403 }, GetRR(0b10000, 0x12, 0x03, 0x04, 0x05));
        }

        [TestMethod]
        public void EverythingWorksTogether()
        {
            var reading = GetReading(0b11001, 0x01, 0x02, 0x22, 0x33, 0x03, 0x04, 0x05, 0x06).Value;

            Assert.AreEqual(0x0201, reading.BeatsPerMinute);
            Assert.AreEqual(0x3322, reading.EnergyExpended);
            CollectionAssert.AreEqual(new[] { 0x0403, 0x0605 }, reading.RRIntervals);
        }
    }
}

using System.IO;
using System.Linq;
using HeartRate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.HeartRate.Tests
{
    [TestClass]
    public class FileWriterTests
    {
        [TestMethod]
        public void IBIFormatsCorrectly()
        {
            using var tmp = new TempFile();
            var ibi = new IBIFile(tmp);

            ibi.Reading(new HeartRateReading
            {
                RRIntervals = new int[] {
                    4 * 1024,
                    4 * 1024 + (1024 / 2 + 4), // Ensure the rounding is working.
                    6 * 1024 + 4
                }
            });

            ibi.Reading(new HeartRateReading
            {
                RRIntervals = new int[] {
                    7 * 1024,
                    8 * 1024,
                    9 * 1024
                }
            });

            var actual = File.ReadAllLines(tmp);
            var expected = Enumerable.Range(4, 6)
                .Select(t => t.ToString()).ToArray();
            CollectionAssert.AreEqual(expected, actual);
        }
    }
}

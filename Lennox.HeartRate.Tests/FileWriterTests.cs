using System;
using System.IO;
using System.Linq;
using HeartRate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.HeartRate.Tests
{
    [TestClass]
    public class FileWriterTests
    {
        private static int MillisecondToRRValue(double val)
        {
            return (int)(val / 1000d * 1024);
        }

        [TestMethod]
        public void IBIFormatsCorrectly()
        {
            using var tmp = new TempFile();
            using (var ibi = new IBIFile(tmp))
            {
                ibi.Reading(new HeartRateReading
                {
                    RRIntervals = new int[] {
                        MillisecondToRRValue(4),
                        MillisecondToRRValue(5),
                        MillisecondToRRValue(6)
                    }
                });

                // No-operations.
                ibi.Reading(new HeartRateReading { RRIntervals = null });
                ibi.Reading(new HeartRateReading { RRIntervals = Array.Empty<int>() });

                ibi.Reading(new HeartRateReading
                {
                    RRIntervals = new int[] {
                        MillisecondToRRValue(7),
                        MillisecondToRRValue(8),
                        MillisecondToRRValue(9)
                    }
                });
            }

            var actual = File.ReadAllLines(tmp);
            var expected = Enumerable.Range(4, 6)
                .Select(t => t.ToString()).ToArray();
            CollectionAssert.AreEqual(expected, actual);
        }
    }
}

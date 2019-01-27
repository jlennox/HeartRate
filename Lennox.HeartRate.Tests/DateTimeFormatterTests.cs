using System;
using HeartRate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.HeartRate.Tests
{
    [TestClass]
    public class DateTimeFormatterTests
    {
        [TestMethod]
        public void TokenParserExcahngesTokens()
        {
            var dt = new DateTime(1990, 12, 25, 1, 2, 20);

            void AssertOutput(string input, string expected)
            {
                var actual = DateTimeFormatter.FormatStringTokens(input, dt);

                Assert.AreEqual(expected, actual);
            }

            AssertOutput("No tokens", "No tokens");

            AssertOutput(
                "Token at end %date%",
                "Token at end 1990-12-25 01-02 AM");

            AssertOutput(
                "Token at end %date:OA%",
                "Token at end 33232.043287037");

            AssertOutput(
                "Token at end %date:MM-dd-yyyy%",
                "Token at end 12-25-1990");
        }
    }
}

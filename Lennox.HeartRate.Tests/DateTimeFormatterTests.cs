using System;
using HeartRate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.HeartRate.Tests
{
    [TestClass]
    public class DateTimeFormatterTests
    {
        private readonly DateTime _dt = new DateTime(1990, 12, 25, 1, 2, 20);

        private void AssertOutput(
            string input, string expected,
            bool forFilepath = false)
        {
            var actual = DateTimeFormatter.FormatStringTokens(
                input, _dt, forFilepath: forFilepath);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TokenParserExcahngesTokens()
        {
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

        [TestMethod]
        public void SanatizesFilenames()
        {
            AssertOutput(
                "Token at end %date:MM:dd:yyyy%",
                "Token at end 12:25:1990", false);

            AssertOutput(
                "Token at end %date:MM:dd:yyyy%",
                "Token at end 12-25-1990", true);
        }
    }
}

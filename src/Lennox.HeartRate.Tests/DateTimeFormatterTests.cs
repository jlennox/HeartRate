using System;
using HeartRate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.HeartRate.Tests;

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
        // If it's not set to be for a filepath, the inclusion of invalid
        // characters is not escaped.
        AssertOutput(
            @"C:\foo\bar\test-%date:MM:dd:yyyy%",
            @"C:\foo\bar\test-12:25:1990", false);

        // But if it's marked as being for a file, then invalid filename
        // characters are scaped to dashes.
        AssertOutput(
            @"C:\foo\bar\test-%date:MM:dd:yyyy%",
            @"C:\foo\bar\test-12-25-1990", true);
    }
}
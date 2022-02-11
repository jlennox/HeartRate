using System.IO;
using HeartRate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.HeartRate.Tests;

[TestClass]
public class DebugLogTests
{
    [TestMethod]
    public void TruncatingWorks()
    {
        var file = Path.Combine(Path.GetTempPath(), "TruncatingWorks.txt");

        DebugLog.Initialize(file);

        for (var i = 0; i < 1024 * 1024 / DebugLog.FormatLine("Testing").Length + 50; ++i)
        {
            DebugLog.WriteLog("Testing");
        }
    }
}
using System.IO;
using HeartRate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.HeartRate.Tests
{
    [TestClass]
    public class SettingsTests
    {
        [TestMethod]
        public void SettingsSaveToFileAsExpected()
        {
            const string expected = @"<?xml version=""1.0""?>
<HeartRateSettingsProtocol xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <Version>1</Version>
  <FontName>Arial</FontName>
  <UIFontName>Arial</UIFontName>
  <UIFontStyle>Regular</UIFontStyle>
  <UIFontUseSize>false</UIFontUseSize>
  <UIFontSize>20</UIFontSize>
  <UIWindowSizeX>350</UIWindowSizeX>
  <UIWindowSizeY>250</UIWindowSizeY>
  <UITextAlignment>MiddleCenter</UITextAlignment>
  <AlertLevel>70</AlertLevel>
  <WarnLevel>65</WarnLevel>
  <AlertTimeout>120000</AlertTimeout>
  <DisconnectedTimeout>10000</DisconnectedTimeout>
  <Color>FFADD8E6</Color>
  <WarnColor>FFFF0000</WarnColor>
  <UIColor>FF00008B</UIColor>
  <UIWarnColor>FFFF0000</UIWarnColor>
  <UIBackgroundColor>00FFFFFF</UIBackgroundColor>
  <UIBackgroundLayout>Stretch</UIBackgroundLayout>
  <Sizable>true</Sizable>
  <LogFormat>csv</LogFormat>
  <LogDateFormat>OA</LogDateFormat>
  <LogFile> </LogFile>
  <IBIFile> </IBIFile>
</HeartRateSettingsProtocol>";

            using (var tempFile = new TempFile())
            {
                var def = HeartRateSettings.CreateDefault(tempFile);
                def.Save();

                var actual = File.ReadAllText(tempFile);
                AssertStringEqualsNormalizeEndings(expected, actual);
            }
        }

        private static void AssertStringEqualsNormalizeEndings(
            string expected, string actual)
        {
            expected = (expected ?? "").Replace("\r", "").Trim();
            actual = (actual ?? "").Replace("\r", "").Trim();

            Assert.AreEqual(expected, actual);
        }
    }
}

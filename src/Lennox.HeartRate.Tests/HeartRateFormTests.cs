using System;
using System.IO;
using System.Threading;
using HeartRate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.HeartRate.Tests;

[TestClass]
public class HeartRateFormTests
{
    [TestMethod]
    public void TestThing()
    {
        using (var settingsFile = new TempFile())
        using (var logFile = new TempFile())
        {
            var settings = new HeartRateSettings(settingsFile);

            settings.Save();

            using (var service = new TestHeartRateService(
                       TimeSpan.FromMilliseconds(100)))
            {
                var formThread = new Thread(_ =>
                {
                    using (var form = new HeartRateForm(
                               service, settingsFile, DateTime.Now))
                    {
                        form.Show();

                        Thread.Sleep(100000);
                    }
                })
                {
                    IsBackground = true
                };

                formThread.Start();
            }

            Thread.Sleep(100000);
        }
    }

    [TestMethod]
    public void Test()
    {
        using (var settingsFile = new TempFile())
        using (var logFile = new TempFile())
        {
            var settings = new HeartRateSettings(settingsFile)
            {
                LogFormat = "csv",
                LogFile = logFile
            };

            settings.Save();

            const int count = 3;

            var left = count;

            using (var mre = new CountdownEvent(count))
            using (var service = new TestHeartRateService(
                       TimeSpan.FromMilliseconds(100)))
            {
                service.HeartRateUpdated += (reading) =>
                {
                    if (Interlocked.Decrement(ref left) >= 0)
                    {
                        mre.Signal();
                    }
                };

                var formThread = new Thread(_ =>
                {
                    using (var form = new HeartRateForm(
                               service, settingsFile, DateTime.Now))
                    {
                        form.Show();
                        mre.Wait();
                        Thread.Sleep(1000);
                    }
                }) {
                    IsBackground = true
                };

                formThread.Start();
                mre.Wait();
            }

            var actual = File.ReadAllText(logFile);
            // TODO: finish test.
        }
    }
}
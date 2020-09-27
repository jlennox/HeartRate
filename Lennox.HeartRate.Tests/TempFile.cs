using System;
using System.IO;
using System.Threading;

namespace Lennox.HeartRate.Tests
{
    internal sealed class TempFile : IDisposable
    {
        public string Filename => Volatile.Read(ref _filename);

        private string _filename = Path.GetTempFileName();

        public static implicit operator string(TempFile f)
        {
            return f.Filename;
        }

        public void Dispose()
        {
            var filename = Interlocked.Exchange(ref _filename, null);

            if (filename != null && File.Exists(filename))
            {
                try
                {
                    File.Delete(filename);
                }
                catch (Exception) { }
            }
        }
    }
}
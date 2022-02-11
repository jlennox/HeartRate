using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace HeartRate
{
    internal static class Extensions
    {
        // Arg, this is a horrible function.
        public static T AsyncResult<T>(this IAsyncOperation<T> async)
        {
            while (true)
            {
                switch (async.Status)
                {
                    case AsyncStatus.Started:
                        Thread.Sleep(100);
                        continue;
                    case AsyncStatus.Completed:
                        return async.GetResults();
                    case AsyncStatus.Error:
                        throw async.ErrorCode;
                    case AsyncStatus.Canceled:
                        throw new TaskCanceledException();
                }
            }
        }

        public static ushort ReadUInt16(this Stream stream)
        {
            return (ushort)(stream.ReadByte() | (stream.ReadByte() << 8));
        }

        public static bool TryDispose<T>(this T disposable)
            where T : IDisposable
        {
            if (disposable == null) return true;

            try
            {
                disposable.Dispose();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string Truncate(this string s, int length)
        {
            if (s == null || s.Length < length) return s;

            return s.Substring(0, length);
        }
    }
}

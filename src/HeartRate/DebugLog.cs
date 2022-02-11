using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace HeartRate;

internal class DebugLog
{
    private readonly string _name;

    public DebugLog(string name)
    {
        _name = name;
    }

    public void Write(string s)
    {
        WriteLog($"{_name}: {s}");
    }

    private static FileStream _fs = null;

    public static void Initialize(string filename)
    {
        _fs = File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    internal static string FormatLine(string s)
    {
        return $"{DateTime.Now}: {s}\n";
    }

    public static void WriteLog(string s)
    {
        Debug.WriteLine(s);

        if (_fs != null)
        {
            var bytes = Encoding.UTF8.GetBytes(FormatLine(s));

            if (_fs.Length > 1024 * 1024)
            {
                _fs.SetLength(0);
            }

            _fs.Write(bytes, 0, bytes.Length);
            _fs.Flush();
        }
    }
}
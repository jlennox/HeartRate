using System.Drawing;
using System.Windows.Forms;

namespace HeartRate;

internal static class Prompt
{
    public static bool TryColor(Color current, out Color color)
    {
        color = default;

        using var dlg = new ColorDialog
        {
            Color = current
        };

        if (dlg.ShowDialog() != DialogResult.OK) return false;

        color = dlg.Color;
        return true;
    }

    public static bool TryFont(
        string currentFont,
        FontStyle currentStyle,
        int currentSize,
        out Font font)
    {
        font = default;

        // Even though it's not really in "points," this prevents it from converting our PX size to Points, and
        // any conversion roundings that would happen.
        using var dlgFont = new Font(currentFont, currentSize, currentStyle, GraphicsUnit.Point);
        using var dlg = new FontDialog
        {
            FontMustExist = true,
            Font = dlgFont
        };

        if (dlg.ShowDialog() != DialogResult.OK) return false;

        font = dlg.Font;
        return true;
    }

    public static bool TryFile(string current, string filter, out string file)
    {
        file = default;

        using var dlg = new OpenFileDialog
        {
            CheckFileExists = true,
            FileName = current,
            Filter = filter
        };

        if (dlg.ShowDialog() != DialogResult.OK) return false;

        file = dlg.FileName;
        return true;
    }

    public static bool TrySaveFile(string current, string filter, out string file)
    {
        file = default;

        using var dlg = new SaveFileDialog
        {
            FileName = current,
            Filter = filter
        };

        if (dlg.ShowDialog() != DialogResult.OK) return false;

        file = dlg.FileName;
        return true;
    }
}
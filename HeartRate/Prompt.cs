using System.Drawing;
using System.Windows.Forms;

namespace HeartRate
{
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
            string currentFond,
            FontStyle currentStyle,
            int currentSize,
            out Font font)
        {
            font = default;

            using var dlgFont = new Font(currentFond, currentSize, currentStyle, GraphicsUnit.Pixel);
            using var dlg = new FontDialog
            {
                FontMustExist = true,
                Font = dlgFont,
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
    }
}

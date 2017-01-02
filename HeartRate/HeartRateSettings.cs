using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeartRate
{
    public class HeartRateSettings
    {
        public string FontName { get; set; }
        public int AlertLevel { get; set; }
        public int WarnLevel { get; set; }
        public TimeSpan AlertTimeout { get; set; }
        public TimeSpan DisconnectedTimeout { get; set; }
        public Brush Color { get; set; }
        public Brush WarnColor { get; set; }

        public static HeartRateSettings CreateDefault()
        {
            return new HeartRateSettings {
                FontName = "Arial",
                WarnLevel = 65,
                AlertLevel = 70,
                AlertTimeout = TimeSpan.FromMinutes(2),
                DisconnectedTimeout = TimeSpan.FromSeconds(10),
                Color = Brushes.LightBlue,
                WarnColor = Brushes.Red
            };
        }
    }
}

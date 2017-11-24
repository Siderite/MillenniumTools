using MillenniumTools.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace MillenniumTools
{
    public class TextToBrushConverter : IValueConverter
    {
        private static ConcurrentDictionary<string, Color> _dict = new ConcurrentDictionary<string, Color>();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var text = value as string;
            return GetBrush(text);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public static Brush GetBrush(string text)
        {
            var color = _dict.GetOrAdd(text, t => GenerateColorFromString(t));
            return new SolidColorBrush(color);
        }

        public static Color GenerateColorFromString(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return Colors.DarkGray;

            var index = _dict.Count;
            var increment = 80;
            var pos = 0.0;
            var list = new List<double>();
            for (var i = 0; i < index; i++)
            {
                list.Add(pos);
                pos += increment;
                if (pos >= 240)
                {
                    pos = 0;
                    increment = increment / 2;
                }
                while (list.Contains(pos))
                {
                    pos += increment;
                }
            }

            Color color = new HSLColor(pos, 240.0, 100.0);
            color.A = 200;
            return color;
            /*
            var bytes = Encoding.UTF8.GetBytes(text);
            var hash = System.Security.Cryptography.SHA1.Create().ComputeHash(bytes);
            var colors = new byte[] { 0, 0, 0 };
            for (var i = 0; i < hash.Length; i++)
            {
                var di = i % colors.Length;
                colors[di] ^= hash[i];
            }
            return Color.FromArgb(192, colors[0], colors[1], colors[2]);*/
        }

    }
}

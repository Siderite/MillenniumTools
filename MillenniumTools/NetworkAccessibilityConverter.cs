using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace MillenniumTools
{
    public class NetworkAccessibilityConverter:IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var accessibility = (double)value;
            return new SolidColorBrush(Color.FromRgb(255, (byte)(accessibility * 100.0 / 100+155), (byte)(accessibility * 255.0 / 100)));
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

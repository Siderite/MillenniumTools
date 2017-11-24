using MillenniumTools.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace MillenniumTools
{
    public class BezierConverter:IMultiValueConverter
    {
        public Size Size { get; set; }

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            int parVal;
            if (values.Length != 4 || values.Any(v => !(v is double)) || !int.TryParse(parameter as string,out parVal)) return DependencyProperty.UnsetValue;
            var width = Size.Width;// (double)values[0];
            var height = Size.Height;// (double)values[1];
            var x1 = ((double)values[0])*width;
            var y1 = ((double)values[1])*height;
            var x2 = ((double)values[2])*width;
            var y2 = ((double)values[3])*height;
            switch(parVal) {
                case 0:
                    return new Point(x1, y1);
                case 1:
                    return new Point(x1+Config.Instance.Smoothness*(x2-x1), y1);
                case 2:
                    return new Point(x1 + (1 - Config.Instance.Smoothness) * (x2 - x1), y2);
                case 3:
                    return new Point(x2, y2);
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }
}

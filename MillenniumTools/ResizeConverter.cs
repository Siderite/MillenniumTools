using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace MillenniumTools
{
    public class ResizeConverter:IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values.Length != 2 || !(values[0] is double) || !(values[1] is double)) return DependencyProperty.UnsetValue;
            var result = (double)values[0] * (double)values[1];
            var parString = parameter as string;
            if (!string.IsNullOrWhiteSpace(parString))
            {
                double offset;
                if (double.TryParse(parString, out offset))
                {
                    result += offset;
                }
            }
            return result;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }

}

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DevSummit2014.Core.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter != null)
            {
                var negationgMark = parameter.ToString();
                if (negationgMark == "!")
                {
                    return (bool)value ? Visibility.Collapsed : Visibility.Visible;
                }
            }

            return (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

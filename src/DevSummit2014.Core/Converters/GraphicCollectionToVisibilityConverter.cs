using Esri.ArcGISRuntime.Layers;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DevSummit2014.Core.Converters
{
    public class GraphicCollectionToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var collection = value as GraphicCollection;
            if (collection == null)
            {
                return Visibility.Collapsed;
            }

            int hideIfUnderGraphics;
            int.TryParse(parameter.ToString(), out hideIfUnderGraphics);

            if (hideIfUnderGraphics > 0)
            {
                if (collection.Count < hideIfUnderGraphics)
                {
                    return Visibility.Collapsed;
                }
                else
                {
                    return Visibility.Visible;
                }
            }

            return collection.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

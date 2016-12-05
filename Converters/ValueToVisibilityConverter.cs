using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace BikeSharing.Clients.CogServicesKiosk.Converters
{
    public class ValueToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool returnValue = false;

            if (value is bool)
                returnValue = (bool)value;
            else if (value is string)
                returnValue = !string.IsNullOrWhiteSpace(value.ToString());
            else if (value != null)
                returnValue = true;

            return returnValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}

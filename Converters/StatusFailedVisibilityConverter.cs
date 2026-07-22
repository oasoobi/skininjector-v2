using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace skininjector_v2.Converters;

public class StatusFailedVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return string.Equals(
            value?.ToString(),
            "Failed",
            StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
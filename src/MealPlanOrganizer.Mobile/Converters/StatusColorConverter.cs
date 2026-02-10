using System.Globalization;

namespace MealPlanOrganizer.Mobile.Converters;

/// <summary>
/// Converter for meal plan status to background color.
/// </summary>
public class StatusColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value as string;
        return status switch
        {
            "Active" => Color.FromArgb("#4CAF50"),
            "Complete" => Color.FromArgb("#2196F3"),
            "Draft" => Color.FromArgb("#9E9E9E"),
            _ => Color.FromArgb("#9E9E9E")
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

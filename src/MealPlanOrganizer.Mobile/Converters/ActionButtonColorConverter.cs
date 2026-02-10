using System.Globalization;

namespace MealPlanOrganizer.Mobile.Converters;

/// <summary>
/// Converter for action button color based on whether recipe is assigned.
/// </summary>
public class ActionButtonColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasRecipe = value is bool b && b;
        return hasRecipe 
            ? Color.FromArgb("#FF9800") // Orange for change
            : Color.FromArgb("#4CAF50"); // Green for add
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

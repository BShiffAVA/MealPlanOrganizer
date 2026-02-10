using System.Globalization;

namespace MealPlanOrganizer.Mobile.Converters;

/// <summary>
/// Converter for day background based on whether recipe is assigned.
/// </summary>
public class HasRecipeBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasRecipe = value is bool b && b;
        return hasRecipe 
            ? Color.FromArgb("#E8F5E9") // Light green
            : Color.FromArgb("#FFF3E0"); // Light orange
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

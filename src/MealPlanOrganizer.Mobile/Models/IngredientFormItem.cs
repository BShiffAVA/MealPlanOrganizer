using CommunityToolkit.Mvvm.ComponentModel;

namespace MealPlanOrganizer.Mobile.Models;

/// <summary>
/// Form item for ingredient input with two-way binding support.
/// </summary>
public partial class IngredientFormItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _quantity = string.Empty;

    public IngredientFormItem()
    {
    }

    public IngredientFormItem(string name, string quantity)
    {
        _name = name;
        _quantity = quantity;
    }
}

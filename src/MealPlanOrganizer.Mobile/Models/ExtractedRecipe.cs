using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MealPlanOrganizer.Mobile.Models;

/// <summary>
/// Extracted recipe data from GenAI processing.
/// </summary>
public class ExtractedRecipe
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CuisineType { get; set; }
    public int? PrepMinutes { get; set; }
    public int? CookMinutes { get; set; }
    public int? Servings { get; set; }
    public ObservableCollection<ExtractedIngredient> Ingredients { get; set; } = new();
    public ObservableCollection<ExtractedStep> Steps { get; set; } = new();
}

/// <summary>
/// An ingredient extracted from a recipe.
/// </summary>
public class ExtractedIngredient : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private decimal? _quantity;
    private string? _unit;
    private string? _quantityWithUnit;

    public string Name 
    { 
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public decimal? Quantity
    {
        get => _quantity;
        set { _quantity = value; OnPropertyChanged(); OnPropertyChanged(nameof(QuantityWithUnit)); }
    }

    public string? Unit
    {
        get => _unit;
        set { _unit = value; OnPropertyChanged(); OnPropertyChanged(nameof(QuantityWithUnit)); }
    }

    /// <summary>
    /// Combined quantity and unit for display/editing.
    /// </summary>
    public string QuantityWithUnit
    {
        get
        {
            if (_quantityWithUnit != null) return _quantityWithUnit;
            if (!Quantity.HasValue) return string.Empty;
            return string.IsNullOrWhiteSpace(Unit) 
                ? Quantity.Value.ToString("G") 
                : $"{Quantity.Value:G} {Unit}";
        }
        set
        {
            _quantityWithUnit = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Display string combining quantity, unit, and name.
    /// </summary>
    public string DisplayText => Quantity.HasValue 
        ? $"{Quantity} {Unit ?? ""} {Name}".Trim()
        : Name;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// A step extracted from a recipe.
/// </summary>
public class ExtractedStep : INotifyPropertyChanged
{
    private int _stepNumber;
    private string _instruction = string.Empty;

    public int StepNumber 
    { 
        get => _stepNumber;
        set { _stepNumber = value; OnPropertyChanged(); }
    }

    public string Instruction 
    { 
        get => _instruction;
        set { _instruction = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

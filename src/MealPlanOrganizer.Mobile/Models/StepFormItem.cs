using CommunityToolkit.Mvvm.ComponentModel;

namespace MealPlanOrganizer.Mobile.Models;

/// <summary>
/// Form item for step/instruction input with two-way binding support.
/// </summary>
public partial class StepFormItem : ObservableObject
{
    [ObservableProperty]
    private int _stepNumber;

    [ObservableProperty]
    private string _instruction = string.Empty;

    public StepFormItem()
    {
    }

    public StepFormItem(int stepNumber, string instruction)
    {
        _stepNumber = stepNumber;
        _instruction = instruction;
    }
}

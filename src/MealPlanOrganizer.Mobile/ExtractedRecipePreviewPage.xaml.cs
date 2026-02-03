using System.Collections.ObjectModel;
using MealPlanOrganizer.Mobile.Models;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile;

[QueryProperty(nameof(ExtractedRecipe), "ExtractedRecipe")]
[QueryProperty(nameof(Confidence), "Confidence")]
public partial class ExtractedRecipePreviewPage : ContentPage
{
    private readonly IRecipeService _recipeService;
    private ExtractedRecipe? _extractedRecipe;
    private double _confidence;

    public ObservableCollection<ExtractedIngredient> Ingredients { get; } = [];
    public ObservableCollection<ExtractedStep> Steps { get; } = [];

    public ExtractedRecipe? ExtractedRecipe
    {
        get => _extractedRecipe;
        set
        {
            _extractedRecipe = value;
            if (value != null)
            {
                PopulateForm(value);
            }
        }
    }

    public double Confidence
    {
        get => _confidence;
        set
        {
            _confidence = value;
            UpdateConfidenceDisplay(value);
        }
    }

    public ExtractedRecipePreviewPage(IRecipeService recipeService)
    {
        InitializeComponent();
        _recipeService = recipeService;
        BindingContext = this;
    }

    private void PopulateForm(ExtractedRecipe recipe)
    {
        NameEntry.Text = recipe.Name ?? string.Empty;
        DescriptionEditor.Text = recipe.Description ?? string.Empty;
        CuisineEntry.Text = recipe.CuisineType ?? string.Empty;
        ServingsEntry.Text = recipe.Servings?.ToString() ?? string.Empty;
        PrepTimeEntry.Text = recipe.PrepMinutes?.ToString() ?? string.Empty;
        CookTimeEntry.Text = recipe.CookMinutes?.ToString() ?? string.Empty;

        Ingredients.Clear();
        if (recipe.Ingredients != null)
        {
            foreach (var ingredient in recipe.Ingredients)
            {
                Ingredients.Add(ingredient);
            }
        }

        Steps.Clear();
        if (recipe.Steps != null)
        {
            foreach (var step in recipe.Steps)
            {
                Steps.Add(step);
            }
        }
    }

    private void UpdateConfidenceDisplay(double confidence)
    {
        string icon;
        string label;
        string description;
        Color backgroundColor;
        Color borderColor;
        Color textColor;

        if (confidence >= 0.8)
        {
            icon = "✅";
            label = "High Confidence";
            description = "The AI is confident about this extraction";
            backgroundColor = Color.FromArgb("#E8F5E9");
            borderColor = Color.FromArgb("#4CAF50");
            textColor = Color.FromArgb("#2E7D32");
        }
        else if (confidence >= 0.6)
        {
            icon = "⚠️";
            label = "Medium Confidence";
            description = "Please review and correct any errors";
            backgroundColor = Color.FromArgb("#FFF3E0");
            borderColor = Color.FromArgb("#FF9800");
            textColor = Color.FromArgb("#E65100");
        }
        else
        {
            icon = "🔍";
            label = "Low Confidence";
            description = "Significant review recommended";
            backgroundColor = Color.FromArgb("#FFEBEE");
            borderColor = Color.FromArgb("#F44336");
            textColor = Color.FromArgb("#C62828");
        }

        ConfidenceFrame.BackgroundColor = backgroundColor;
        ConfidenceFrame.BorderColor = borderColor;
        ConfidenceIcon.Text = icon;
        ConfidenceLabel.Text = $"{label} ({confidence:P0})";
        ConfidenceLabel.TextColor = textColor;
        ConfidenceDescription.Text = description;
        ConfidenceDescription.TextColor = textColor;
    }

    private void OnAddIngredientClicked(object? sender, EventArgs e)
    {
        Ingredients.Add(new ExtractedIngredient
        {
            Name = string.Empty,
            Quantity = null,
            Unit = string.Empty
        });
    }

    private void OnRemoveIngredientClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ExtractedIngredient ingredient)
        {
            Ingredients.Remove(ingredient);
        }
    }

    private void OnAddStepClicked(object? sender, EventArgs e)
    {
        Steps.Add(new ExtractedStep
        {
            StepNumber = Steps.Count + 1,
            Instruction = string.Empty
        });
    }

    private void OnRemoveStepClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ExtractedStep step)
        {
            Steps.Remove(step);
            RenumberSteps();
        }
    }

    private void OnMoveStepUpClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ExtractedStep step)
        {
            var index = Steps.IndexOf(step);
            if (index > 0)
            {
                Steps.Move(index, index - 1);
                RenumberSteps();
            }
        }
    }

    private void OnMoveStepDownClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ExtractedStep step)
        {
            var index = Steps.IndexOf(step);
            if (index < Steps.Count - 1)
            {
                Steps.Move(index, index + 1);
                RenumberSteps();
            }
        }
    }

    private void RenumberSteps()
    {
        for (int i = 0; i < Steps.Count; i++)
        {
            Steps[i].StepNumber = i + 1;
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        var confirmed = await DisplayAlert(
            "Discard Changes?",
            "Are you sure you want to discard this extracted recipe?",
            "Discard",
            "Keep Editing");

        if (confirmed)
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        ValidationErrorFrame.IsVisible = false;

        // Validate required fields
        if (string.IsNullOrWhiteSpace(NameEntry.Text))
        {
            ShowValidationError("Recipe name is required");
            return;
        }

        if (Ingredients.Count == 0 || Ingredients.All(i => string.IsNullOrWhiteSpace(i.Name)))
        {
            ShowValidationError("At least one ingredient is required");
            return;
        }

        if (Steps.Count == 0 || Steps.All(s => string.IsNullOrWhiteSpace(s.Instruction)))
        {
            ShowValidationError("At least one instruction step is required");
            return;
        }

        try
        {
            SetSavingState(true);

            // Build recipe DTO using existing CreateRecipeDto structure
            var recipe = new CreateRecipeDto
            {
                Title = NameEntry.Text.Trim(),
                Description = DescriptionEditor.Text?.Trim(),
                CuisineType = CuisineEntry.Text?.Trim(),
                Servings = ParseInt(ServingsEntry.Text),
                PrepTimeMinutes = ParseInt(PrepTimeEntry.Text),
                CookTimeMinutes = ParseInt(CookTimeEntry.Text),
                Ingredients = Ingredients
                    .Where(i => !string.IsNullOrWhiteSpace(i.Name))
                    .Select(i => new IngredientInput
                    {
                        Name = i.Name?.Trim() ?? string.Empty,
                        Quantity = !string.IsNullOrWhiteSpace(i.QuantityWithUnit) ? i.QuantityWithUnit.Trim() : null
                    })
                    .ToList(),
                Steps = Steps
                    .Where(s => !string.IsNullOrWhiteSpace(s.Instruction))
                    .Select(s => s.Instruction?.Trim() ?? string.Empty)
                    .ToList()
            };

            var savedRecipeId = await _recipeService.CreateRecipeAsync(recipe);

            if (savedRecipeId.HasValue)
            {
                await DisplayAlert("Success", "Recipe saved successfully!", "OK");
                
                // Navigate back to main page
                await Shell.Current.GoToAsync("//MainPage");
            }
            else
            {
                ShowValidationError("Failed to save recipe. Please try again.");
            }
        }
        catch (Exception ex)
        {
            ShowValidationError($"Error saving recipe: {ex.Message}");
        }
        finally
        {
            SetSavingState(false);
        }
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value, out var result) ? result : null;
    }

    private void ShowValidationError(string message)
    {
        ValidationErrorLabel.Text = message;
        ValidationErrorFrame.IsVisible = true;
    }

    private void SetSavingState(bool isSaving)
    {
        SaveButton.IsVisible = !isSaving;
        SavingSection.IsVisible = isSaving;
    }
}

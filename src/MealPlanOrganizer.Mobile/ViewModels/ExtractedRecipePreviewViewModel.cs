using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPlanOrganizer.Mobile.Models;
using MealPlanOrganizer.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for the Extracted Recipe Preview page where users review and edit AI-extracted recipes.
/// </summary>
[QueryProperty(nameof(ExtractedRecipe), "ExtractedRecipe")]
[QueryProperty(nameof(Confidence), "Confidence")]
public partial class ExtractedRecipePreviewViewModel : ObservableObject
{
    private readonly IRecipeService _recipeService;
    private readonly ILogger<ExtractedRecipePreviewViewModel> _logger;

    [ObservableProperty]
    private string? _recipeName;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _cuisineType;

    [ObservableProperty]
    private string? _servingsText;

    [ObservableProperty]
    private string? _prepTimeText;

    [ObservableProperty]
    private string? _cookTimeText;

    [ObservableProperty]
    private ObservableCollection<ExtractedIngredient> _ingredients = new();

    [ObservableProperty]
    private ObservableCollection<ExtractedStep> _steps = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasValidationError;

    [ObservableProperty]
    private string? _validationErrorMessage;

    // Confidence display properties
    [ObservableProperty]
    private string? _confidenceIcon;

    [ObservableProperty]
    private string? _confidenceLabel;

    [ObservableProperty]
    private string? _confidenceDescription;

    [ObservableProperty]
    private string _confidenceBackgroundColor = "#E8F5E9";

    [ObservableProperty]
    private string _confidenceBorderColor = "#4CAF50";

    [ObservableProperty]
    private string _confidenceTextColor = "#2E7D32";

    private ExtractedRecipe? _extractedRecipe;
    private double _confidence;

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
            OnPropertyChanged();
        }
    }

    public double Confidence
    {
        get => _confidence;
        set
        {
            _confidence = value;
            UpdateConfidenceDisplay(value);
            OnPropertyChanged();
        }
    }

    public ExtractedRecipePreviewViewModel(
        IRecipeService recipeService,
        ILogger<ExtractedRecipePreviewViewModel> logger)
    {
        _recipeService = recipeService;
        _logger = logger;
    }

    private void PopulateForm(ExtractedRecipe recipe)
    {
        RecipeName = recipe.Name ?? string.Empty;
        Description = recipe.Description ?? string.Empty;
        CuisineType = recipe.CuisineType ?? string.Empty;
        ServingsText = recipe.Servings?.ToString() ?? string.Empty;
        PrepTimeText = recipe.PrepMinutes?.ToString() ?? string.Empty;
        CookTimeText = recipe.CookMinutes?.ToString() ?? string.Empty;

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

        _logger.LogInformation("Populated form with recipe: {Name}, {IngredientCount} ingredients, {StepCount} steps",
            recipe.Name, Ingredients.Count, Steps.Count);
    }

    private void UpdateConfidenceDisplay(double confidence)
    {
        if (confidence >= 0.8)
        {
            ConfidenceIcon = "✅";
            ConfidenceLabel = $"High Confidence ({confidence:P0})";
            ConfidenceDescription = "The AI is confident about this extraction";
            ConfidenceBackgroundColor = "#E8F5E9";
            ConfidenceBorderColor = "#4CAF50";
            ConfidenceTextColor = "#2E7D32";
        }
        else if (confidence >= 0.6)
        {
            ConfidenceIcon = "⚠️";
            ConfidenceLabel = $"Medium Confidence ({confidence:P0})";
            ConfidenceDescription = "Please review and correct any errors";
            ConfidenceBackgroundColor = "#FFF3E0";
            ConfidenceBorderColor = "#FF9800";
            ConfidenceTextColor = "#E65100";
        }
        else
        {
            ConfidenceIcon = "🔍";
            ConfidenceLabel = $"Low Confidence ({confidence:P0})";
            ConfidenceDescription = "Significant review recommended";
            ConfidenceBackgroundColor = "#FFEBEE";
            ConfidenceBorderColor = "#F44336";
            ConfidenceTextColor = "#C62828";
        }
    }

    [RelayCommand]
    private void AddIngredient()
    {
        Ingredients.Add(new ExtractedIngredient
        {
            Name = string.Empty,
            Quantity = null,
            Unit = string.Empty
        });
    }

    [RelayCommand]
    private void RemoveIngredient(ExtractedIngredient? ingredient)
    {
        if (ingredient != null)
        {
            Ingredients.Remove(ingredient);
        }
    }

    [RelayCommand]
    private void AddStep()
    {
        Steps.Add(new ExtractedStep
        {
            StepNumber = Steps.Count + 1,
            Instruction = string.Empty
        });
    }

    [RelayCommand]
    private void RemoveStep(ExtractedStep? step)
    {
        if (step != null)
        {
            Steps.Remove(step);
            RenumberSteps();
        }
    }

    [RelayCommand]
    private void MoveStepUp(ExtractedStep? step)
    {
        if (step != null)
        {
            var index = Steps.IndexOf(step);
            if (index > 0)
            {
                Steps.Move(index, index - 1);
                RenumberSteps();
            }
        }
    }

    [RelayCommand]
    private void MoveStepDown(ExtractedStep? step)
    {
        if (step != null)
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

    [RelayCommand]
    private async Task CancelAsync()
    {
        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Discard Changes?",
            "Are you sure you want to discard this extracted recipe?",
            "Discard",
            "Keep Editing");

        if (confirmed)
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        HasValidationError = false;

        // Validate required fields
        if (string.IsNullOrWhiteSpace(RecipeName))
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
            IsLoading = true;
            _logger.LogInformation("Saving extracted recipe: {Name}", RecipeName);

            // Build recipe DTO
            var recipe = new CreateRecipeDto
            {
                Title = RecipeName.Trim(),
                Description = Description?.Trim(),
                CuisineType = CuisineType?.Trim(),
                Servings = ParseInt(ServingsText),
                PrepTimeMinutes = ParseInt(PrepTimeText),
                CookTimeMinutes = ParseInt(CookTimeText),
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
                _logger.LogInformation("Recipe saved successfully: {RecipeId}", savedRecipeId);
                await Shell.Current.DisplayAlertAsync("Success", "Recipe saved successfully!", "OK");
                await Shell.Current.GoToAsync("//MainPage");
            }
            else
            {
                ShowValidationError("Failed to save recipe. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving recipe");
            ShowValidationError($"Error saving recipe: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value, out var result) ? result : null;
    }

    private void ShowValidationError(string message)
    {
        ValidationErrorMessage = message;
        HasValidationError = true;
    }
}

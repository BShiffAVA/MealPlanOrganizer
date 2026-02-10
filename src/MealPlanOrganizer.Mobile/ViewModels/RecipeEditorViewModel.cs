using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPlanOrganizer.Mobile.Models;
using MealPlanOrganizer.Mobile.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Media;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for AddRecipePage and EditRecipePage.
/// Uses IsNewRecipe flag to control create vs update behavior.
/// </summary>
public partial class RecipeEditorViewModel : ObservableObject
{
    private readonly IRecipeService _recipeService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<RecipeEditorViewModel> _logger;
    private FileResult? _selectedPhoto;
    private string? _existingImageUrl;

    #region Observable Properties

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _cuisineType = string.Empty;

    [ObservableProperty]
    private string _prepTime = string.Empty;

    [ObservableProperty]
    private string _cookTime = string.Empty;

    [ObservableProperty]
    private string _servings = string.Empty;

    [ObservableProperty]
    private string _creatorName = string.Empty;

    [ObservableProperty]
    private string _imageUrl = string.Empty;

    [ObservableProperty]
    private ImageSource? _photoSource;

    [ObservableProperty]
    private bool _hasPhoto;

    [ObservableProperty]
    private string _photoStatusText = "No photo selected";

    [ObservableProperty]
    private ObservableCollection<IngredientFormItem> _ingredients = new();

    [ObservableProperty]
    private ObservableCollection<StepFormItem> _steps = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isNewRecipe = true;

    [ObservableProperty]
    private Guid? _recipeId;

    #endregion

    public RecipeEditorViewModel(
        IRecipeService recipeService,
        INavigationService navigationService,
        ILogger<RecipeEditorViewModel> logger)
    {
        _recipeService = recipeService;
        _navigationService = navigationService;
        _logger = logger;
    }

    #region Initialization

    /// <summary>
    /// Initializes the ViewModel for creating a new recipe.
    /// </summary>
    public void InitializeForNewRecipe()
    {
        IsNewRecipe = true;
        RecipeId = null;
        ClearForm();
    }

    /// <summary>
    /// Initializes the ViewModel for editing an existing recipe.
    /// </summary>
    public async Task InitializeForEditAsync(Guid recipeId)
    {
        IsNewRecipe = false;
        RecipeId = recipeId;
        await LoadExistingRecipeAsync(recipeId);
    }

    private void ClearForm()
    {
        Title = string.Empty;
        Description = string.Empty;
        CuisineType = string.Empty;
        PrepTime = string.Empty;
        CookTime = string.Empty;
        Servings = string.Empty;
        CreatorName = string.Empty;
        ImageUrl = string.Empty;
        PhotoSource = null;
        HasPhoto = false;
        PhotoStatusText = "No photo selected";
        _selectedPhoto = null;
        _existingImageUrl = null;
        ErrorMessage = null;

        Ingredients.Clear();
        Steps.Clear();
    }

    [RelayCommand]
    private async Task LoadExistingRecipeAsync(Guid recipeId)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var recipe = await _recipeService.GetRecipeByIdAsync(recipeId);

            if (recipe == null)
            {
                ErrorMessage = "Recipe not found";
                return;
            }

            Title = recipe.Title;
            Description = recipe.Description ?? string.Empty;
            CuisineType = recipe.CuisineType ?? string.Empty;
            PrepTime = recipe.PrepTimeMinutes?.ToString() ?? string.Empty;
            CookTime = recipe.CookTimeMinutes?.ToString() ?? string.Empty;
            Servings = recipe.Servings?.ToString() ?? string.Empty;
            CreatorName = recipe.CreatedBy ?? "Unknown";

            if (!string.IsNullOrWhiteSpace(recipe.ImageUrl))
            {
                _existingImageUrl = recipe.ImageUrl;
                ImageUrl = recipe.ImageUrl;
                PhotoSource = ImageSource.FromUri(new Uri(recipe.ImageUrl));
                HasPhoto = true;
                PhotoStatusText = "Using existing photo";
            }

            Ingredients.Clear();
            if (recipe.Ingredients != null)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    Ingredients.Add(new IngredientFormItem(ingredient.Name, ingredient.Quantity ?? string.Empty));
                }
            }

            if (Ingredients.Count == 0)
            {
                Ingredients.Add(new IngredientFormItem());
            }

            Steps.Clear();
            if (recipe.Steps != null)
            {
                var orderedSteps = recipe.Steps.OrderBy(s => s.StepNumber).ToList();
                for (int i = 0; i < orderedSteps.Count; i++)
                {
                    Steps.Add(new StepFormItem(i + 1, orderedSteps[i].Instruction));
                }
            }

            if (Steps.Count == 0)
            {
                Steps.Add(new StepFormItem(1, string.Empty));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load recipe {RecipeId}", recipeId);
            ErrorMessage = $"Failed to load recipe: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Ingredient Commands

    [RelayCommand]
    private void AddIngredient()
    {
        Ingredients.Add(new IngredientFormItem());
    }

    [RelayCommand]
    private void RemoveIngredient(IngredientFormItem? ingredient)
    {
        if (ingredient != null && Ingredients.Count > 1)
        {
            Ingredients.Remove(ingredient);
        }
    }

    #endregion

    #region Step Commands

    [RelayCommand]
    private void AddStep()
    {
        var nextStepNumber = Steps.Count + 1;
        Steps.Add(new StepFormItem(nextStepNumber, string.Empty));
    }

    [RelayCommand]
    private void RemoveStep(StepFormItem? step)
    {
        if (step != null && Steps.Count > 1)
        {
            Steps.Remove(step);
            RenumberSteps();
        }
    }

    [RelayCommand]
    private void MoveStepUp(StepFormItem? step)
    {
        if (step == null) return;

        var index = Steps.IndexOf(step);
        if (index > 0)
        {
            Steps.Move(index, index - 1);
            RenumberSteps();
        }
    }

    [RelayCommand]
    private void MoveStepDown(StepFormItem? step)
    {
        if (step == null) return;

        var index = Steps.IndexOf(step);
        if (index < Steps.Count - 1)
        {
            Steps.Move(index, index + 1);
            RenumberSteps();
        }
    }

    private void RenumberSteps()
    {
        for (int i = 0; i < Steps.Count; i++)
        {
            Steps[i].StepNumber = i + 1;
        }
    }

    #endregion

    #region Photo Commands

    [RelayCommand]
    private async Task TakePhotoAsync()
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            ErrorMessage = "Camera capture is not available on this device.";
            return;
        }

        try
        {
            var photo = await MediaPicker.Default.CapturePhotoAsync();
            await SetPhotoAsync(photo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to take photo");
            ErrorMessage = $"Unable to take photo: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task PickPhotoAsync()
    {
        try
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var photo = await MediaPicker.Default.PickPhotoAsync();
#pragma warning restore CS0618
            await SetPhotoAsync(photo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pick photo");
            ErrorMessage = $"Unable to choose photo: {ex.Message}";
        }
    }

    private async Task SetPhotoAsync(FileResult? photo)
    {
        if (photo == null)
        {
            return;
        }

        _selectedPhoto = photo;
        PhotoSource = ImageSource.FromFile(photo.FullPath);
        HasPhoto = true;
        PhotoStatusText = $"Photo selected: {Path.GetFileName(photo.FullPath)}";
        ImageUrl = "Will be uploaded...";
    }

    [RelayCommand]
    private void ClearPhoto()
    {
        _selectedPhoto = null;
        PhotoSource = null;
        HasPhoto = false;
        PhotoStatusText = "No photo selected";
        ImageUrl = string.Empty;
    }

    #endregion

    #region Save Command

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!ValidateForm())
        {
            ErrorMessage = "Please fill in all required fields (title, at least one ingredient, and at least one step).";
            return;
        }

        try
        {
            IsSaving = true;
            ErrorMessage = null;

            if (IsNewRecipe)
            {
                await CreateRecipeAsync();
            }
            else
            {
                await UpdateRecipeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save recipe");
            ErrorMessage = $"An error occurred: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task CreateRecipeAsync()
    {
        var recipe = new CreateRecipeDto
        {
            Title = Title.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            CuisineType = string.IsNullOrWhiteSpace(CuisineType) ? null : CuisineType.Trim(),
            PrepTimeMinutes = int.TryParse(PrepTime, out var prepTime) ? prepTime : null,
            CookTimeMinutes = int.TryParse(CookTime, out var cookTime) ? cookTime : null,
            Servings = int.TryParse(Servings, out var servings) ? servings : null,
            ImageUrl = null,
            Ingredients = Ingredients
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => new IngredientInput
                {
                    Name = x.Name.Trim(),
                    Quantity = string.IsNullOrWhiteSpace(x.Quantity) ? null : x.Quantity.Trim()
                })
                .ToList(),
            Steps = Steps
                .Where(x => !string.IsNullOrWhiteSpace(x.Instruction))
                .Select(x => x.Instruction.Trim())
                .ToList()
        };

        if (_selectedPhoto != null)
        {
            PhotoStatusText = "Creating recipe...";
            var recipeId = await _recipeService.CreateRecipeAsync(recipe);

            if (!recipeId.HasValue)
            {
                PhotoStatusText = "Recipe creation failed";
                ErrorMessage = "Failed to create recipe. Please try again.";
                return;
            }

            _logger.LogInformation("Recipe created with ID: {RecipeId}, now uploading image", recipeId.Value);
            PhotoStatusText = "Uploading photo...";

            var imageUrl = await _recipeService.UploadRecipeImageAsync(_selectedPhoto, recipeId.Value);

            if (imageUrl != null)
            {
                PhotoStatusText = "Updating recipe with image...";
                var updateDto = new UpdateRecipeDto
                {
                    Title = recipe.Title,
                    Description = recipe.Description,
                    CuisineType = recipe.CuisineType,
                    PrepTimeMinutes = recipe.PrepTimeMinutes,
                    CookTimeMinutes = recipe.CookTimeMinutes,
                    Servings = recipe.Servings,
                    ImageUrl = imageUrl,
                    Ingredients = recipe.Ingredients,
                    Steps = recipe.Steps
                };

                await _recipeService.UpdateRecipeAsync(recipeId.Value, updateDto);
                _logger.LogInformation("Recipe updated with image URL: {RecipeId}", recipeId.Value);
            }
            else
            {
                _logger.LogWarning("Image upload failed for recipe: {RecipeId}", recipeId.Value);
            }

            await _navigationService.GoBackAsync();
        }
        else
        {
            var recipeId = await _recipeService.CreateRecipeAsync(recipe);

            if (recipeId.HasValue)
            {
                await _navigationService.GoBackAsync();
            }
            else
            {
                ErrorMessage = "Failed to create recipe. Please try again.";
            }
        }
    }

    private async Task UpdateRecipeAsync()
    {
        if (!RecipeId.HasValue)
        {
            ErrorMessage = "No recipe ID set for update.";
            return;
        }

        var imageUrl = _existingImageUrl;

        if (_selectedPhoto != null)
        {
            PhotoStatusText = "Uploading photo...";
            var uploadedUrl = await _recipeService.UploadRecipeImageAsync(_selectedPhoto, RecipeId.Value);

            if (string.IsNullOrWhiteSpace(uploadedUrl))
            {
                PhotoStatusText = "Photo upload failed";
                ErrorMessage = "Image upload failed. Please try again.";
                return;
            }

            imageUrl = uploadedUrl;
            PhotoStatusText = "Photo uploaded successfully!";
        }
        else if (!string.IsNullOrWhiteSpace(ImageUrl) && ImageUrl != "Will be uploaded...")
        {
            imageUrl = ImageUrl.Trim();
        }

        var updateDto = new UpdateRecipeDto
        {
            Title = Title.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            CuisineType = string.IsNullOrWhiteSpace(CuisineType) ? null : CuisineType.Trim(),
            PrepTimeMinutes = int.TryParse(PrepTime, out var prepTime) ? prepTime : null,
            CookTimeMinutes = int.TryParse(CookTime, out var cookTime) ? cookTime : null,
            Servings = int.TryParse(Servings, out var servings) ? servings : null,
            ImageUrl = imageUrl,
            Ingredients = Ingredients
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => new IngredientInput
                {
                    Name = x.Name.Trim(),
                    Quantity = string.IsNullOrWhiteSpace(x.Quantity) ? null : x.Quantity.Trim()
                })
                .ToList(),
            Steps = Steps
                .Where(x => !string.IsNullOrWhiteSpace(x.Instruction))
                .Select(x => x.Instruction.Trim())
                .ToList()
        };

        var success = await _recipeService.UpdateRecipeAsync(RecipeId.Value, updateDto);

        if (success)
        {
            _existingImageUrl = imageUrl;
            await _navigationService.GoBackAsync();
        }
        else
        {
            ErrorMessage = "Failed to update recipe. Please try again.";
        }
    }

    private bool ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(Title))
            return false;

        if (Ingredients.Count == 0 || Ingredients.All(x => string.IsNullOrWhiteSpace(x.Name)))
            return false;

        if (Steps.Count == 0 || Steps.All(x => string.IsNullOrWhiteSpace(x.Instruction)))
            return false;

        // For new recipes, creator name is required
        if (IsNewRecipe && string.IsNullOrWhiteSpace(CreatorName))
            return false;

        return true;
    }

    #endregion
}

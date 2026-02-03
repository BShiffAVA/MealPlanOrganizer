using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Media;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile;

public partial class EditRecipePage : ContentPage
{
    private readonly IRecipeService _recipeService;
    private readonly ILogger<EditRecipePage> _logger;
    private readonly Guid _recipeId;
    private readonly List<(Entry nameEntry, Entry quantityEntry)> _ingredientEntries = new();
    private readonly List<Entry> _instructionEntries = new();
    private FileResult? _selectedPhoto;
    private string? _existingImageUrl;
    private bool _isLoaded;

    public EditRecipePage(Guid recipeId)
    {
        InitializeComponent();
        _recipeId = recipeId;

        _recipeService = IPlatformApplication.Current?.Services.GetService<IRecipeService>()
            ?? throw new InvalidOperationException("IRecipeService not registered");

        _logger = IPlatformApplication.Current?.Services.GetService<ILogger<EditRecipePage>>()
            ?? throw new InvalidOperationException("ILogger<EditRecipePage> not registered");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isLoaded)
        {
            await LoadRecipeAsync();
            _isLoaded = true;
        }
    }

    private async Task LoadRecipeAsync()
    {
        try
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;

            var recipe = await _recipeService.GetRecipeByIdAsync(_recipeId);

            if (recipe == null)
            {
                await DisplayAlert("Error", "Recipe not found", "OK");
                await Navigation.PopAsync();
                return;
            }

            TitleEntry.Text = recipe.Title;
            DescriptionEditor.Text = recipe.Description;
            CuisineEntry.Text = recipe.CuisineType;
            PrepTimeEntry.Text = recipe.PrepTimeMinutes?.ToString() ?? string.Empty;
            CookTimeEntry.Text = recipe.CookTimeMinutes?.ToString() ?? string.Empty;
            ServingsEntry.Text = recipe.Servings?.ToString() ?? string.Empty;
            CreatedByLabel.Text = recipe.CreatedBy ?? "Unknown";

            if (!string.IsNullOrWhiteSpace(recipe.ImageUrl))
            {
                _existingImageUrl = recipe.ImageUrl;
                ImageUrlEntry.Text = recipe.ImageUrl;
                RecipeImagePreview.Source = ImageSource.FromUri(new Uri(recipe.ImageUrl));
                RecipeImagePreview.IsVisible = true;
                PhotoStatusLabel.Text = "Using existing photo";
            }

            IngredientsContainer.Children.Clear();
            InstructionsContainer.Children.Clear();
            _ingredientEntries.Clear();
            _instructionEntries.Clear();

            if (recipe.Ingredients != null && recipe.Ingredients.Count > 0)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    AddIngredientEntry(ingredient.Name, ingredient.Quantity);
                }
            }
            else
            {
                AddIngredientEntry();
            }

            if (recipe.Steps != null && recipe.Steps.Count > 0)
            {
                foreach (var step in recipe.Steps.OrderBy(s => s.StepNumber))
                {
                    AddStepEntry(step.Instruction);
                }
            }
            else
            {
                AddStepEntry();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load recipe: {ex.Message}", "OK");
            await Navigation.PopAsync();
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private void OnAddIngredientClicked(object? sender, EventArgs e) => AddIngredientEntry();

    private void AddIngredientEntry(string? name = null, string? quantity = null)
    {
        var nameEntry = new Entry
        {
            Placeholder = "Ingredient name",
            PlaceholderColor = new Color(153, 153, 153),
            HorizontalOptions = LayoutOptions.FillAndExpand,
            Text = name
        };

        var quantityEntry = new Entry
        {
            Placeholder = "Qty",
            PlaceholderColor = new Color(153, 153, 153),
            HorizontalOptions = LayoutOptions.FillAndExpand,
            Text = quantity
        };

        var removeButton = new Button
        {
            Text = "✕",
            BackgroundColor = Colors.Red,
            TextColor = Colors.White,
            Padding = new Thickness(12, 0),
            CornerRadius = 4
        };

        var ingredientFrame = new Frame
        {
            Padding = 8,
            CornerRadius = 8,
            BorderColor = new Color(224, 224, 224),
            HasShadow = false,
            Content = new HorizontalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new VerticalStackLayout
                    {
                        Spacing = 4,
                        HorizontalOptions = LayoutOptions.FillAndExpand,
                        Children =
                        {
                            nameEntry,
                            new HorizontalStackLayout
                            {
                                Spacing = 4,
                                Children =
                                {
                                    quantityEntry,
                                    removeButton
                                }
                            }
                        }
                    }
                }
            }
        };

        removeButton.Clicked += (s, e) =>
        {
            IngredientsContainer.Children.Remove(ingredientFrame);
            _ingredientEntries.RemoveAll(x => x.nameEntry == nameEntry);
        };

        IngredientsContainer.Children.Add(ingredientFrame);
        _ingredientEntries.Add((nameEntry, quantityEntry));
    }

    private void OnAddStepClicked(object? sender, EventArgs e) => AddStepEntry();

    private void AddStepEntry(string? instruction = null)
    {
        var stepNumber = _instructionEntries.Count + 1;
        var instructionEntry = new Entry
        {
            Placeholder = $"Step {stepNumber} instructions",
            PlaceholderColor = new Color(153, 153, 153),
            HorizontalOptions = LayoutOptions.FillAndExpand,
            Text = instruction
        };

        var removeButton = new Button
        {
            Text = "✕",
            BackgroundColor = Colors.Red,
            TextColor = Colors.White,
            Padding = new Thickness(12, 0),
            CornerRadius = 4
        };

        var stepFrame = new Frame
        {
            Padding = 8,
            CornerRadius = 8,
            BorderColor = new Color(224, 224, 224),
            HasShadow = false,
            Content = new HorizontalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = $"Step {stepNumber}:",
                        VerticalOptions = LayoutOptions.Center,
                        FontAttributes = FontAttributes.Bold,
                        MinimumWidthRequest = 60
                    },
                    instructionEntry,
                    removeButton
                }
            }
        };

        removeButton.Clicked += (s, e) =>
        {
            InstructionsContainer.Children.Remove(stepFrame);
            _instructionEntries.Remove(instructionEntry);
        };

        InstructionsContainer.Children.Add(stepFrame);
        _instructionEntries.Add(instructionEntry);
    }

    private async void OnTakePhotoClicked(object? sender, EventArgs e)
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            await DisplayAlert("Not Supported", "Camera capture is not available on this device.", "OK");
            return;
        }

        try
        {
            var photo = await MediaPicker.Default.CapturePhotoAsync();
            await SetPhotoAsync(photo);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Unable to take photo: {ex.Message}", "OK");
        }
    }

    private async void OnPickPhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var photo = await MediaPicker.Default.PickPhotoAsync();
            await SetPhotoAsync(photo);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Unable to choose photo: {ex.Message}", "OK");
        }
    }

    private async Task SetPhotoAsync(FileResult? photo)
    {
        if (photo == null)
        {
            return;
        }

        _selectedPhoto = photo;
        RecipeImagePreview.IsVisible = true;
        RecipeImagePreview.Source = ImageSource.FromFile(photo.FullPath);
        PhotoStatusLabel.Text = $"Photo selected: {Path.GetFileName(photo.FullPath)}";
        ImageUrlEntry.Text = "Will be uploaded...";
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (!ValidateForm())
        {
            await DisplayAlert("Validation Error", "Please fill in all required fields.", "OK");
            return;
        }

        try
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;
            SaveButton.IsEnabled = false;

            var imageUrl = !string.IsNullOrWhiteSpace(ImageUrlEntry.Text)
                ? ImageUrlEntry.Text?.Trim()
                : _existingImageUrl;

            if (_selectedPhoto != null)
            {
                PhotoStatusLabel.Text = "Uploading photo...";
                var uploadedUrl = await _recipeService.UploadRecipeImageAsync(_selectedPhoto, _recipeId);

                if (string.IsNullOrWhiteSpace(uploadedUrl))
                {
                    PhotoStatusLabel.Text = "Photo upload failed";
                    await DisplayAlert("Error", "Image upload failed. Please try again.", "OK");
                    return;
                }

                imageUrl = uploadedUrl;
                PhotoStatusLabel.Text = "Photo uploaded successfully!";
            }

            var updateDto = new UpdateRecipeDto
            {
                Title = TitleEntry.Text?.Trim() ?? string.Empty,
                Description = DescriptionEditor.Text,
                CuisineType = CuisineEntry.Text,
                PrepTimeMinutes = int.TryParse(PrepTimeEntry.Text, out var prepTime) ? prepTime : null,
                CookTimeMinutes = int.TryParse(CookTimeEntry.Text, out var cookTime) ? cookTime : null,
                Servings = int.TryParse(ServingsEntry.Text, out var servings) ? servings : null,
                ImageUrl = imageUrl,
                Ingredients = _ingredientEntries
                    .Where(x => !string.IsNullOrWhiteSpace(x.nameEntry.Text))
                    .Select(x => new IngredientInput
                    {
                        Name = x.nameEntry.Text ?? string.Empty,
                        Quantity = x.quantityEntry.Text
                    })
                    .ToList(),
                Steps = _instructionEntries
                    .Where(x => !string.IsNullOrWhiteSpace(x.Text))
                    .Select(x => x.Text ?? string.Empty)
                    .ToList()
            };

            var success = await _recipeService.UpdateRecipeAsync(_recipeId, updateDto);

            if (success)
            {
                _existingImageUrl = imageUrl;
                await DisplayAlert("Success", "Recipe updated successfully!", "OK");
                await Navigation.PopAsync();
            }
            else
            {
                await DisplayAlert("Error", "Failed to update recipe. Please try again.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
            SaveButton.IsEnabled = true;
        }
    }

    private bool ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(TitleEntry.Text))
            return false;

        if (_ingredientEntries.Count == 0 || _ingredientEntries.All(x => string.IsNullOrWhiteSpace(x.nameEntry.Text)))
            return false;

        if (_instructionEntries.Count == 0 || _instructionEntries.All(x => string.IsNullOrWhiteSpace(x.Text)))
            return false;

        return true;
    }
}

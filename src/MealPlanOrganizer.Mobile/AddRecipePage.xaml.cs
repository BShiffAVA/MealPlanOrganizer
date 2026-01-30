using System.Collections.ObjectModel;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile;

public partial class AddRecipePage : ContentPage
{
    private readonly IRecipeService _recipeService;
    private readonly List<(Entry nameEntry, Entry quantityEntry)> _ingredientEntries = new();
    private readonly List<Entry> _instructionEntries = new();

    public AddRecipePage()
    {
        InitializeComponent();
        
        // Get service from dependency injection
        _recipeService = IPlatformApplication.Current?.Services.GetService<IRecipeService>()
            ?? throw new InvalidOperationException("IRecipeService not registered");
    }

    private void OnAddIngredientClicked(object? sender, EventArgs e)
    {
        var nameEntry = new Entry
        {
            Placeholder = "Ingredient name",
            PlaceholderColor = new Color(153, 153, 153),
            HorizontalOptions = LayoutOptions.FillAndExpand
        };

        var quantityEntry = new Entry
        {
            Placeholder = "Qty",
            PlaceholderColor = new Color(153, 153, 153),
            HorizontalOptions = LayoutOptions.FillAndExpand
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

    private void OnAddStepClicked(object? sender, EventArgs e)
    {
        var stepNumber = _instructionEntries.Count + 1;
        var instructionEntry = new Entry
        {
            Placeholder = $"Step {stepNumber} instructions",
            PlaceholderColor = new Color(153, 153, 153),
            HorizontalOptions = LayoutOptions.FillAndExpand
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

            var recipe = new CreateRecipeDto
            {
                Title = TitleEntry.Text,
                Description = DescriptionEditor.Text,
                CuisineType = CuisineEntry.Text,
                PrepTimeMinutes = int.TryParse(PrepTimeEntry.Text, out var prepTime) ? prepTime : null,
                CookTimeMinutes = int.TryParse(CookTimeEntry.Text, out var cookTime) ? cookTime : null,
                Servings = int.TryParse(ServingsEntry.Text, out var servings) ? servings : null,
                ImageUrl = ImageUrlEntry.Text,
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

            var recipeId = await _recipeService.CreateRecipeAsync(recipe);

            if (recipeId.HasValue)
            {
                await DisplayAlert("Success", "Recipe created successfully!", "OK");
                await Navigation.PopAsync();
            }
            else
            {
                await DisplayAlert("Error", "Failed to create recipe. Please try again.", "OK");
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
        // Title is required
        if (string.IsNullOrWhiteSpace(TitleEntry.Text))
            return false;

        // At least one ingredient is required
        if (_ingredientEntries.Count == 0 || _ingredientEntries.All(x => string.IsNullOrWhiteSpace(x.nameEntry.Text)))
            return false;

        // At least one instruction is required
        if (_instructionEntries.Count == 0 || _instructionEntries.All(x => string.IsNullOrWhiteSpace(x.Text)))
            return false;

        // Creator name is required
        if (string.IsNullOrWhiteSpace(CreatorEntry.Text))
            return false;

        return true;
    }
}

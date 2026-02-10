using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPlanOrganizer.Mobile.Models;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for the MainPage - displays recipe list with filtering and search.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IRecipeService _recipeService;
    private readonly INavigationService _navigationService;
    private readonly List<RecipeCard> _allRecipes = new();

    private static readonly List<string> DefaultPrepTimeOptions = new()
    {
        "All",
        "Quick (<15)",
        "15-30 min",
        "30-60 min",
        "60+ min"
    };

    private static readonly List<string> DefaultRatingOptions = new()
    {
        "All",
        "4-5 stars",
        "3+ stars"
    };

    [ObservableProperty]
    private ObservableCollection<RecipeCard> _recipes = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCuisine = "All";

    [ObservableProperty]
    private string _selectedPrepTime = "All";

    [ObservableProperty]
    private string _selectedRating = "All";

    [ObservableProperty]
    private string _selectedCreator = "All";

    [ObservableProperty]
    private ObservableCollection<string> _cuisineOptions = new() { "All" };

    [ObservableProperty]
    private ObservableCollection<string> _prepTimeOptions = new(DefaultPrepTimeOptions);

    [ObservableProperty]
    private ObservableCollection<string> _ratingOptions = new(DefaultRatingOptions);

    [ObservableProperty]
    private ObservableCollection<string> _creatorOptions = new() { "All" };

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public MainViewModel(IRecipeService recipeService, INavigationService navigationService)
    {
        _recipeService = recipeService;
        _navigationService = navigationService;
    }

    /// <summary>
    /// Loads recipes from the API and initializes filter options.
    /// </summary>
    [RelayCommand]
    private async Task LoadRecipesAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var recipes = await _recipeService.GetRecipesAsync();

            _allRecipes.Clear();
            foreach (var recipe in recipes)
            {
                _allRecipes.Add(new RecipeCard(
                    recipe.Id,
                    recipe.Title,
                    recipe.CuisineType ?? "Unknown",
                    recipe.PrepTimeMinutes ?? 0,
                    recipe.AverageRating,
                    recipe.CreatedBy ?? "Unknown"
                ));
            }

            InitializeFilters();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load recipes: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Navigates to add a new recipe.
    /// </summary>
    [RelayCommand]
    private async Task AddRecipeAsync()
    {
        // Using Navigation.PushAsync pattern for non-Shell pages
        if (Application.Current?.Windows.FirstOrDefault()?.Page is NavigationPage navPage)
        {
            await navPage.Navigation.PushAsync(new AddRecipePage());
        }
    }

    /// <summary>
    /// Navigates to import/extract a recipe.
    /// </summary>
    [RelayCommand]
    private async Task ImportRecipeAsync()
    {
        await _navigationService.GoToAsync(nameof(ExtractRecipePage));
    }

    /// <summary>
    /// Navigates to view recipe details.
    /// </summary>
    [RelayCommand]
    private async Task ViewRecipeAsync(RecipeCard? recipe)
    {
        if (recipe == null) return;

        // Using Navigation.PushAsync pattern for non-Shell pages
        if (Application.Current?.Windows.FirstOrDefault()?.Page is NavigationPage navPage)
        {
            await navPage.Navigation.PushAsync(new RecipeDetailPage(recipe.Id));
        }
    }

    private void InitializeFilters()
    {
        var cuisines = _allRecipes
            .Select(r => r.CuisineType)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
        cuisines.Insert(0, "All");

        var creators = _allRecipes
            .Select(r => r.CreatedBy)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
        creators.Insert(0, "All");

        CuisineOptions = new ObservableCollection<string>(cuisines);
        CreatorOptions = new ObservableCollection<string>(creators);

        // Reset selections to default
        SelectedCuisine = "All";
        SelectedPrepTime = "All";
        SelectedRating = "All";
        SelectedCreator = "All";
    }

    private void ApplyFilters()
    {
        IEnumerable<RecipeCard> filtered = _allRecipes;

        // Search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(r => r.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        // Cuisine filter
        if (SelectedCuisine != "All")
        {
            filtered = filtered.Where(r => r.CuisineType.Equals(SelectedCuisine, StringComparison.OrdinalIgnoreCase));
        }

        // Prep time filter
        filtered = SelectedPrepTime switch
        {
            "Quick (<15)" => filtered.Where(r => r.PrepTimeMinutes < 15),
            "15-30 min" => filtered.Where(r => r.PrepTimeMinutes >= 15 && r.PrepTimeMinutes <= 30),
            "30-60 min" => filtered.Where(r => r.PrepTimeMinutes > 30 && r.PrepTimeMinutes <= 60),
            "60+ min" => filtered.Where(r => r.PrepTimeMinutes > 60),
            _ => filtered
        };

        // Rating filter
        filtered = SelectedRating switch
        {
            "4-5 stars" => filtered.Where(r => r.Rating >= 4),
            "3+ stars" => filtered.Where(r => r.Rating >= 3),
            _ => filtered
        };

        // Creator filter
        if (SelectedCreator != "All")
        {
            filtered = filtered.Where(r => r.CreatedBy.Equals(SelectedCreator, StringComparison.OrdinalIgnoreCase));
        }

        Recipes = new ObservableCollection<RecipeCard>(filtered);
    }

    // Property change handlers for filtering
    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedCuisineChanged(string value) => ApplyFilters();
    partial void OnSelectedPrepTimeChanged(string value) => ApplyFilters();
    partial void OnSelectedRatingChanged(string value) => ApplyFilters();
    partial void OnSelectedCreatorChanged(string value) => ApplyFilters();
}

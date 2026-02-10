# MealPlanOrganizer Mobile Architecture

## Overview

The MealPlanOrganizer mobile application is built with .NET MAUI following the **Model-View-ViewModel (MVVM)** architectural pattern. This document describes the target architecture, component responsibilities, and design decisions.

## Architecture Goals

- **Testability** – ViewModels can be unit tested independently without UI framework dependencies
- **Separation of Concerns** – Clear boundaries between UI, business logic, and data access
- **Maintainability** – Changes to UI don't break logic; changes to logic don't require XAML modifications
- **Scalability** – Pattern supports growing feature set without architectural changes
- **Offline-First** – Local caching with background sync (future enhancement)

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Views (XAML + Code-Behind)              │
│  LoginPage, MainPage, RecipeDetailPage, MealPlanDetailPage...   │
│                                                                 │
│  Responsibilities:                                              │
│  • UI layout and styling                                        │
│  • Data binding to ViewModels                                   │
│  • Platform-specific UI behaviors                               │
└───────────────────────────────┬─────────────────────────────────┘
                                │ DataContext / BindingContext
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                           ViewModels                            │
│  LoginViewModel, MainViewModel, RecipeDetailViewModel...        │
│                                                                 │
│  Responsibilities:                                              │
│  • Presentation logic and state                                 │
│  • Command handling                                             │
│  • Property change notifications                                │
│  • Coordination between Views and Services                      │
└───────────────────────────────┬─────────────────────────────────┘
                                │ Dependency Injection
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                            Services                             │
│  IRecipeService, IAuthService, INavigationService               │
│                                                                 │
│  Responsibilities:                                              │
│  • API communication                                            │
│  • Authentication                                               │
│  • Navigation abstraction                                       │
│  • Business logic                                               │
└───────────────────────────────┬─────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                             Models                              │
│  RecipeCard, RecipeDetailDto, MealPlanDto, IngredientFormItem   │
│                                                                 │
│  Responsibilities:                                              │
│  • Data structures (DTOs)                                       │
│  • Form item models with INotifyPropertyChanged                 │
└─────────────────────────────────────────────────────────────────┘
```

## Project Structure

```
src/MealPlanOrganizer.Mobile/
├── App.xaml                    # Application resources and converters
├── App.xaml.cs
├── AppShell.xaml               # Shell navigation routes
├── AppShell.xaml.cs
├── MauiProgram.cs              # DI container configuration
│
├── Converters/                 # Value converters for XAML bindings
│   ├── StringNotEmptyConverter.cs
│   ├── StatusColorConverter.cs
│   ├── HasRecipeBackgroundConverter.cs
│   └── ActionButtonColorConverter.cs
│
├── Models/                     # Data transfer objects and form models
│   ├── RecipeCard.cs
│   ├── ExtractedRecipe.cs
│   ├── ExtractedIngredient.cs
│   ├── ExtractedStep.cs
│   ├── IngredientFormItem.cs
│   ├── StepFormItem.cs
│   └── ...
│
├── Services/                   # Business logic and external communication
│   ├── IAuthService.cs
│   ├── AuthService.cs
│   ├── IRecipeService.cs
│   ├── RecipeService.cs
│   ├── INavigationService.cs
│   └── NavigationService.cs
│
├── ViewModels/                 # Presentation logic
│   ├── LoginViewModel.cs
│   ├── MainViewModel.cs
│   ├── RecipeEditorViewModel.cs
│   ├── RecipeDetailViewModel.cs
│   ├── MealPlansViewModel.cs
│   ├── CreateMealPlanViewModel.cs
│   ├── MealPlanDetailViewModel.cs
│   ├── MealPlanDayViewModel.cs
│   ├── RecipePickerPageViewModel.cs
│   ├── RecipePickerItemViewModel.cs
│   ├── ExtractRecipeViewModel.cs
│   └── ExtractedRecipePreviewViewModel.cs
│
├── Views/                      # XAML pages (or root folder)
│   ├── LoginPage.xaml / .cs
│   ├── MainPage.xaml / .cs
│   ├── AddRecipePage.xaml / .cs
│   ├── EditRecipePage.xaml / .cs
│   ├── RecipeDetailPage.xaml / .cs
│   ├── MealPlansPage.xaml / .cs
│   ├── CreateMealPlanPage.xaml / .cs
│   ├── MealPlanDetailPage.xaml / .cs
│   ├── RecipePickerPage.xaml / .cs
│   ├── ExtractRecipePage.xaml / .cs
│   └── ExtractedRecipePreviewPage.xaml / .cs
│
├── Platforms/                  # Platform-specific code
├── Properties/
└── Resources/
```

## Component Responsibilities

### Views (Pages)

Views are XAML files with minimal code-behind. They are responsible for:

- **Layout and Styling** – All visual structure defined in XAML
- **Data Binding** – Connect UI elements to ViewModel properties via `{Binding}`
- **BindingContext Setup** – Receive ViewModel via constructor injection

```csharp
public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
```

**Code-behind should NOT contain:**
- Business logic
- API calls
- Data transformation
- Navigation decisions

### ViewModels

ViewModels contain presentation logic and expose data/commands for Views. Built using **CommunityToolkit.Mvvm** for reduced boilerplate.

```csharp
public partial class MainViewModel : ObservableObject
{
    private readonly IRecipeService _recipeService;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private ObservableCollection<RecipeCard> _recipes = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public MainViewModel(IRecipeService recipeService, INavigationService navigation)
    {
        _recipeService = recipeService;
        _navigation = navigation;
    }

    [RelayCommand]
    private async Task LoadRecipesAsync()
    {
        IsLoading = true;
        try
        {
            var recipes = await _recipeService.GetRecipesAsync();
            Recipes = new ObservableCollection<RecipeCard>(recipes.Select(r => new RecipeCard(r)));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ViewRecipeAsync(RecipeCard recipe)
    {
        await _navigation.GoToAsync($"recipe/{recipe.Id}");
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }
}
```

### Services

Services handle business logic and external communication. They are:
- **Interface-based** for testability
- **Injected via DI** into ViewModels
- **Stateless** where possible

| Service | Responsibility |
|---------|----------------|
| `IRecipeService` | Recipe CRUD, meal plans, ratings, extraction |
| `IAuthService` | Authentication via Microsoft Entra External ID |
| `INavigationService` | Abstracted Shell navigation for testability |

### Models

Models are plain data classes. Two categories:

1. **DTOs** – Data transfer objects for API communication (no INotifyPropertyChanged)
2. **Form Items** – Editable models for forms (implement INotifyPropertyChanged)

```csharp
// DTO - immutable data from API
public record RecipeCard(Guid Id, string Title, string CuisineType, int PrepTimeMinutes, double Rating);

// Form Item - editable with property change notifications
public partial class IngredientFormItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _quantity = string.Empty;
}
```

### Converters

Value converters transform data for display. Registered in `App.xaml` as application resources.

```csharp
public class StatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Active" => Colors.Green,
            "Completed" => Colors.Gray,
            _ => Colors.Blue
        };
    }
}
```

## Dependency Injection

All dependencies registered in `MauiProgram.cs`:

```csharp
public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    
    // Services
    builder.Services.AddSingleton<IAuthService, AuthService>();
    builder.Services.AddSingleton<INavigationService, NavigationService>();
    builder.Services.AddHttpClient<IRecipeService, RecipeService>();

    // ViewModels
    builder.Services.AddTransient<LoginViewModel>();
    builder.Services.AddTransient<MainViewModel>();
    builder.Services.AddTransient<RecipeEditorViewModel>();
    // ... other ViewModels

    // Pages
    builder.Services.AddTransient<LoginPage>();
    builder.Services.AddTransient<MainPage>();
    builder.Services.AddTransient<AddRecipePage>();
    // ... other Pages

    return builder.Build();
}
```

## Navigation

Navigation uses Shell with an `INavigationService` abstraction:

```csharp
public interface INavigationService
{
    Task GoToAsync(string route);
    Task GoToAsync(string route, IDictionary<string, object> parameters);
    Task GoBackAsync();
}

public class NavigationService : INavigationService
{
    public Task GoToAsync(string route) => Shell.Current.GoToAsync(route);
    
    public Task GoToAsync(string route, IDictionary<string, object> parameters) 
        => Shell.Current.GoToAsync(route, parameters);
    
    public Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
```

Routes defined in `AppShell.xaml`:

```xml
<Shell>
    <ShellContent Route="login" ContentTemplate="{DataTemplate local:LoginPage}" />
    <ShellContent Route="main" ContentTemplate="{DataTemplate local:MainPage}" />
    <!-- ... -->
</Shell>
```

## Data Binding Patterns

### Property Binding

```xml
<Entry Text="{Binding Title}" />
<Label Text="{Binding Recipe.Description}" />
<ActivityIndicator IsRunning="{Binding IsLoading}" />
```

### Command Binding

```xml
<Button Text="Save" Command="{Binding SaveCommand}" />
<Button Text="Delete" Command="{Binding DeleteCommand}" CommandParameter="{Binding .}" />
```

### Collection Binding

```xml
<CollectionView ItemsSource="{Binding Recipes}">
    <CollectionView.ItemTemplate>
        <DataTemplate x:DataType="models:RecipeCard">
            <Frame>
                <Label Text="{Binding Title}" />
            </Frame>
        </DataTemplate>
    </CollectionView.ItemTemplate>
</CollectionView>
```

### Event-to-Command (for events without Command property)

```xml
<SearchBar Text="{Binding SearchText, Mode=TwoWay}" />
<!-- Uses PropertyChanged partial method in ViewModel -->
```

## ViewModel Catalog

| ViewModel | Page(s) | Key Responsibilities |
|-----------|---------|---------------------|
| `LoginViewModel` | LoginPage | Authentication flow |
| `MainViewModel` | MainPage | Recipe list, filtering, search |
| `RecipeEditorViewModel` | AddRecipePage, EditRecipePage | Recipe create/edit form |
| `RecipeDetailViewModel` | RecipeDetailPage | Recipe display, rating submission |
| `MealPlansViewModel` | MealPlansPage | Meal plan list |
| `CreateMealPlanViewModel` | CreateMealPlanPage | New meal plan form |
| `MealPlanDetailViewModel` | MealPlanDetailPage | Meal plan display, recipe assignment |
| `MealPlanDayViewModel` | (Item VM) | Individual day within meal plan |
| `RecipePickerPageViewModel` | RecipePickerPage | Recipe selection for meal plan |
| `RecipePickerItemViewModel` | (Item VM) | Selectable recipe item |
| `ExtractRecipeViewModel` | ExtractRecipePage | Recipe extraction input |
| `ExtractedRecipePreviewViewModel` | ExtractedRecipePreviewPage | Extracted recipe editing |

## Testing Strategy

### Unit Testing ViewModels

ViewModels are fully testable by mocking services:

```csharp
[Fact]
public async Task LoadRecipes_PopulatesRecipesCollection()
{
    // Arrange
    var mockRecipeService = new Mock<IRecipeService>();
    mockRecipeService.Setup(s => s.GetRecipesAsync())
        .ReturnsAsync(new[] { new RecipeDto { Id = Guid.NewGuid(), Title = "Test" } });
    
    var viewModel = new MainViewModel(mockRecipeService.Object, Mock.Of<INavigationService>());

    // Act
    await viewModel.LoadRecipesCommand.ExecuteAsync(null);

    // Assert
    Assert.Single(viewModel.Recipes);
    Assert.Equal("Test", viewModel.Recipes[0].Title);
}
```

### What Can Be Tested

- ViewModel property changes
- Command execution and side effects
- Filtering and search logic
- Validation rules
- Navigation calls
- Service interactions

### What Requires UI Testing

- Visual layout
- Platform-specific behaviors
- Gesture handling
- Animation

## Design Decisions

### CommunityToolkit.Mvvm

**Decision:** Use CommunityToolkit.Mvvm for MVVM infrastructure.

**Rationale:**
- Source generators reduce boilerplate by 60-70%
- `[ObservableProperty]` generates INotifyPropertyChanged
- `[RelayCommand]` generates ICommand implementations
- Microsoft-supported, widely adopted in .NET MAUI community

### INavigationService Abstraction

**Decision:** Wrap Shell navigation in an interface.

**Rationale:**
- Enables unit testing navigation without Shell dependency
- ViewModels don't directly reference platform types
- Can swap implementations (e.g., for testing or alternative navigation)

### Single RecipeEditorViewModel for Add/Edit

**Decision:** Use one ViewModel for both AddRecipePage and EditRecipePage.

**Rationale:**
- 80% code overlap between pages
- `IsNewRecipe` flag controls behavior differences
- Reduces duplication and maintenance burden

### Form Item ViewModels

**Decision:** Create dedicated `IngredientFormItem` and `StepFormItem` classes.

**Rationale:**
- Forms need two-way binding with property change notifications
- Separates editable form state from API DTOs
- Supports collection operations (add/remove/reorder)

## Future Enhancements

- **Offline Support** – SQLite local database with sync manager
- **Real-time Updates** – SignalR integration for rating changes
- **Validation Framework** – CommunityToolkit.Mvvm validation attributes
- **Localization** – Resource-based string localization

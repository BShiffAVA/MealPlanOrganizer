# MVVM Migration Plan

This document describes the step-by-step plan to migrate the MealPlanOrganizer mobile application from a code-behind pattern to a proper MVVM architecture.

## Current State

The mobile app currently uses a **code-behind pattern** with these characteristics:

- No separate ViewModel classes
- Business logic in `.xaml.cs` code-behind files
- Event handlers (`Clicked`, `TextChanged`) instead of commands
- Pages set `BindingContext = this` (View as its own ViewModel)
- Service locator anti-pattern: `IPlatformApplication.Current?.Services.GetService<>()`
- Some inline ViewModel classes in complex pages

## Target State

- Proper MVVM with separate ViewModel classes
- All business logic in ViewModels
- Command bindings using `ICommand`
- Constructor-injected dependencies
- CommunityToolkit.Mvvm for reduced boilerplate
- INavigationService abstraction for testable navigation

## Technology Choices

| Decision | Choice | Rationale |
|----------|--------|-----------|
| MVVM Framework | CommunityToolkit.Mvvm | Source generators reduce boilerplate; Microsoft-supported |
| Navigation | INavigationService abstraction | Enables unit testing without Shell dependency |
| Add/Edit Pattern | Single RecipeEditorViewModel | 80% code overlap; `IsNewRecipe` flag controls behavior |

---

## Migration Phases

### Phase 1: Infrastructure Setup

#### Step 1.1: Add CommunityToolkit.Mvvm Package

Add the NuGet package to the project file.

**File:** `src/MealPlanOrganizer.Mobile/MealPlanOrganizer.Mobile.csproj`

```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
```

#### Step 1.2: Create Folder Structure

Create the following folders:

```
src/MealPlanOrganizer.Mobile/
├── ViewModels/     (new)
└── Converters/     (new)
```

#### Step 1.3: Create Navigation Service

**File:** `Services/INavigationService.cs`

```csharp
namespace MealPlanOrganizer.Mobile.Services;

public interface INavigationService
{
    Task GoToAsync(string route);
    Task GoToAsync(string route, IDictionary<string, object> parameters);
    Task GoBackAsync();
}
```

**File:** `Services/NavigationService.cs`

```csharp
namespace MealPlanOrganizer.Mobile.Services;

public class NavigationService : INavigationService
{
    public Task GoToAsync(string route) 
        => Shell.Current.GoToAsync(route);

    public Task GoToAsync(string route, IDictionary<string, object> parameters) 
        => Shell.Current.GoToAsync(route, parameters);

    public Task GoBackAsync() 
        => Shell.Current.GoToAsync("..");
}
```

#### Step 1.4: Extract Converters

Move existing inline converter classes to the Converters folder:

| Source Location | Converter Class | Target File |
|-----------------|-----------------|-------------|
| `RecipeDetailPage.xaml.cs` | `StringNotEmptyConverter` | `Converters/StringNotEmptyConverter.cs` |
| `MealPlansPage.xaml.cs` | `StatusColorConverter` | `Converters/StatusColorConverter.cs` |
| `MealPlanDetailPage.xaml.cs` | `HasRecipeBackgroundConverter` | `Converters/HasRecipeBackgroundConverter.cs` |
| `MealPlanDetailPage.xaml.cs` | `ActionButtonColorConverter` | `Converters/ActionButtonColorConverter.cs` |

Register converters in `App.xaml`:

```xml
<Application.Resources>
    <ResourceDictionary>
        <converters:StringNotEmptyConverter x:Key="StringNotEmptyConverter" />
        <converters:StatusColorConverter x:Key="StatusColorConverter" />
        <!-- ... -->
    </ResourceDictionary>
</Application.Resources>
```

#### Step 1.5: Extract RecipeCard Model

Move `RecipeCard` class from `MainPage.xaml.cs` to `Models/RecipeCard.cs`.

#### Step 1.6: Update MauiProgram.cs

Register navigation service and prepare for ViewModel/Page registrations:

```csharp
// Services
builder.Services.AddSingleton<INavigationService, NavigationService>();

// ViewModels (add as created)
// builder.Services.AddTransient<LoginViewModel>();
// builder.Services.AddTransient<MainViewModel>();

// Pages (update as migrated)
// builder.Services.AddTransient<LoginPage>();
```

---

### Phase 2: Simple Pages (LoginPage, MealPlansPage)

#### Step 2.1: Create LoginViewModel

**File:** `ViewModels/LoginViewModel.cs`

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MealPlanOrganizer.Mobile.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isErrorVisible;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public LoginViewModel(IAuthService authService, INavigationService navigation)
    {
        _authService = authService;
        _navigation = navigation;
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        IsLoading = true;
        IsErrorVisible = false;

        try
        {
            var result = await _authService.LoginAsync();
            if (result != null)
            {
                await _navigation.GoToAsync("//main");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            IsErrorVisible = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CheckAuthenticationAsync()
    {
        if (await _authService.IsAuthenticatedAsync())
        {
            await _navigation.GoToAsync("//main");
        }
    }
}
```

#### Step 2.2: Refactor LoginPage Code-Behind

**File:** `LoginPage.xaml.cs`

```csharp
namespace MealPlanOrganizer.Mobile;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is LoginViewModel vm)
        {
            await vm.CheckAuthenticationCommand.ExecuteAsync(null);
        }
    }
}
```

#### Step 2.3: Update LoginPage.xaml

Replace event handlers with command bindings:

```xml
<!-- Before -->
<Button Clicked="OnSignInClicked" ... />

<!-- After -->
<Button Command="{Binding SignInCommand}" ... />
```

Replace `x:Name` UI updates with bindings:

```xml
<!-- Before -->
<Grid x:Name="LoadingGrid" IsVisible="False" ... />

<!-- After -->
<Grid IsVisible="{Binding IsLoading}" ... />
```

#### Step 2.4: Create MealPlansViewModel

**File:** `ViewModels/MealPlansViewModel.cs`

Properties:
- `MealPlans` (ObservableCollection<MealPlanDto>)
- `IsLoading`
- `IsEmpty`

Commands:
- `LoadAsync`
- `CreateMealPlanAsync`
- `ViewMealPlanAsync(MealPlanDto)`

#### Step 2.5: Refactor MealPlansPage

Same pattern as LoginPage.

---

### Phase 3: MainPage (List/Filter Patterns)

#### Step 3.1: Create MainViewModel

**File:** `ViewModels/MainViewModel.cs`

Properties:
- `Recipes` (ObservableCollection<RecipeCard>)
- `SearchText`
- `SelectedCuisine`, `SelectedPrepTime`, `SelectedRating`, `SelectedCreator`
- `CuisineOptions`, `PrepTimeOptions`, `RatingOptions`, `CreatorOptions`
- `IsLoading`

Commands:
- `LoadRecipesAsync`
- `ViewRecipeAsync(RecipeCard)`
- `AddRecipeAsync`
- `ImportRecipeAsync`

Filter logic:
```csharp
partial void OnSearchTextChanged(string value) => ApplyFilters();
partial void OnSelectedCuisineChanged(string value) => ApplyFilters();
// ... other filter properties

private void ApplyFilters()
{
    var filtered = _allRecipes.AsEnumerable();
    
    if (!string.IsNullOrEmpty(SearchText))
        filtered = filtered.Where(r => r.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
    
    // ... other filters
    
    Recipes = new ObservableCollection<RecipeCard>(filtered);
}
```

#### Step 3.2: Refactor MainPage Code-Behind

Constructor receives `MainViewModel`, sets `BindingContext`.

#### Step 3.3: Update MainPage.xaml

Replace Picker event handlers with property bindings:

```xml
<!-- Before -->
<Picker x:Name="CuisinePicker" SelectedIndexChanged="OnCuisineFilterChanged" />

<!-- After -->
<Picker ItemsSource="{Binding CuisineOptions}" 
        SelectedItem="{Binding SelectedCuisine, Mode=TwoWay}" />
```

---

### Phase 4: Recipe Form Pages

#### Step 4.1: Create Form Item Models

**File:** `ViewModels/IngredientFormItem.cs`

```csharp
public partial class IngredientFormItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _quantity = string.Empty;
}
```

**File:** `ViewModels/StepFormItem.cs`

```csharp
public partial class StepFormItem : ObservableObject
{
    [ObservableProperty]
    private int _stepNumber;

    [ObservableProperty]
    private string _instruction = string.Empty;
}
```

#### Step 4.2: Create RecipeEditorViewModel

**File:** `ViewModels/RecipeEditorViewModel.cs`

Properties:
- `Title`, `Description`, `CuisineType`, `PrepTime`, `CookTime`, `Servings`, `CreatorName`
- `PhotoSource`, `PhotoStatusText`
- `Ingredients` (ObservableCollection<IngredientFormItem>)
- `Steps` (ObservableCollection<StepFormItem>)
- `IsLoading`, `IsSaving`
- `IsNewRecipe`, `RecipeId`

Commands:
- `AddIngredient`, `RemoveIngredient(IngredientFormItem)`
- `AddStep`, `RemoveStep(StepFormItem)`, `MoveStepUp(StepFormItem)`, `MoveStepDown(StepFormItem)`
- `TakePhotoAsync`, `PickPhotoAsync`
- `SaveAsync`
- `LoadExistingRecipeAsync(Guid)`

#### Step 4.3: Refactor AddRecipePage

```csharp
public partial class AddRecipePage : ContentPage
{
    public AddRecipePage(RecipeEditorViewModel viewModel)
    {
        InitializeComponent();
        viewModel.IsNewRecipe = true;
        BindingContext = viewModel;
    }
}
```

#### Step 4.4: Update AddRecipePage.xaml

Replace dynamic UI generation with CollectionView:

```xml
<!-- Before: Dynamic ingredient rows created in code-behind -->

<!-- After: Data-bound CollectionView -->
<CollectionView ItemsSource="{Binding Ingredients}">
    <CollectionView.ItemTemplate>
        <DataTemplate x:DataType="vm:IngredientFormItem">
            <Frame Padding="8" CornerRadius="8">
                <Grid ColumnDefinitions="*,Auto,Auto">
                    <Entry Text="{Binding Name}" Placeholder="Ingredient" />
                    <Entry Grid.Column="1" Text="{Binding Quantity}" Placeholder="Qty" />
                    <Button Grid.Column="2" Text="✕" 
                            Command="{Binding RemoveIngredientCommand, Source={RelativeSource AncestorType={x:Type ContentPage}}}"
                            CommandParameter="{Binding .}" />
                </Grid>
            </Frame>
        </DataTemplate>
    </CollectionView.ItemTemplate>
</CollectionView>
```

#### Step 4.5: Refactor EditRecipePage

```csharp
public partial class EditRecipePage : ContentPage, IQueryAttributable
{
    private readonly RecipeEditorViewModel _viewModel;

    public EditRecipePage(RecipeEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        viewModel.IsNewRecipe = false;
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid recipeId)
        {
            _viewModel.RecipeId = recipeId;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadExistingRecipeCommand.ExecuteAsync(null);
    }
}
```

---

### Phase 5: Detail Pages

#### Step 5.1: Create RecipeDetailViewModel

Properties:
- `Recipe` (RecipeDetailDto)
- `IsLoading`
- `SelectedRating` (1-5)
- `Comments`, `SelectedFrequency`
- `IsSubmittingRating`
- `RatingStatusMessage`, `RatingStatusColor`
- Computed: `Star1Color` through `Star5Color`

Commands:
- `LoadAsync(Guid)`
- `EditAsync`
- `SelectRatingAsync(int)`
- `SubmitRatingAsync`

#### Step 5.2: Refactor RecipeDetailPage

Update star rating UI to use commands:

```xml
<HorizontalStackLayout>
    <Button Text="★" BackgroundColor="{Binding Star1Color}"
            Command="{Binding SelectRatingCommand}" CommandParameter="1" />
    <Button Text="★" BackgroundColor="{Binding Star2Color}"
            Command="{Binding SelectRatingCommand}" CommandParameter="2" />
    <!-- ... stars 3-5 -->
</HorizontalStackLayout>
```

#### Step 5.3: Extract MealPlanDayViewModel

Move existing `MealPlanDayViewModel` class from `MealPlanDetailPage.xaml.cs` to `ViewModels/MealPlanDayViewModel.cs`.

#### Step 5.4: Create MealPlanDetailViewModel

Properties:
- `MealPlan`
- `Days` (ObservableCollection<MealPlanDayViewModel>)
- `IsLoading`

Commands:
- `LoadAsync(Guid)`
- `AddRecipeAsync(MealPlanDayViewModel)`
- `RemoveRecipeAsync(MealPlanDayViewModel)`

Note: Drag-drop events may need minimal code-behind wiring.

---

### Phase 6: Picker and Extraction Pages

#### Step 6.1: Rename RecipePickerViewModel

Rename existing `RecipePickerViewModel` to `RecipePickerItemViewModel` (it's an item-level VM).

#### Step 6.2: Create RecipePickerPageViewModel

Properties:
- `Recipes` (ObservableCollection<RecipePickerItemViewModel>)
- `SelectedCount`, `SelectionHint`
- `IsMultiSelectMode`, `CanComplete`, `IsLoading`
- Query properties: `MealPlanId`, `Day`, `StartDate`, `TotalDays`, `Mode`

Commands:
- `LoadAsync`
- `SelectRecipeAsync(RecipePickerItemViewModel)`
- `DoneAsync`

#### Step 6.3: Create CreateMealPlanViewModel

Properties:
- `PlanName`, `StartDate`, `EndDate`
- `SummaryText`, `SummaryColor`
- `IsLoading`, `ErrorMessage`, `HasError`
- `Today` (computed, always DateTime.Today)

Commands:
- `SelectThisWeek`, `SelectNextWeek`, `SelectNext2Weeks`
- `CreateAsync`, `CancelAsync`

#### Step 6.4: Create ExtractRecipeViewModel

Properties:
- `CurrentMode` (enum: Image, Url, Text)
- `ImageModeSelected`, `UrlModeSelected`, `TextModeSelected` (for styling)
- `ImagePreviewSource`, `UrlText`, `PastedText`
- `CharacterCount`, `CanExtract`
- `IsLoading`, `ErrorMessage`, `HasError`

Commands:
- `SelectImageModeAsync`, `SelectUrlModeAsync`, `SelectTextModeAsync`
- `TakePhotoAsync`, `PickPhotoAsync`, `ClearImage`
- `ExtractAsync`

#### Step 6.5: Create ExtractedRecipePreviewViewModel

Properties:
- `ExtractedRecipe`, `Confidence`, `ConfidenceColor`, `ConfidenceText`
- `Ingredients` (ObservableCollection), `Steps` (ObservableCollection)
- `Title`, `Description`, etc.
- `IsLoading`

Commands:
- `AddIngredient`, `RemoveIngredient`
- `AddStep`, `RemoveStep`, `MoveStepUp`, `MoveStepDown`
- `SaveAsync`, `DiscardAsync`

---

### Phase 7: Final Cleanup

#### Step 7.1: Complete DI Registration

Update `MauiProgram.cs` with all ViewModels and Pages:

```csharp
// Services
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<INavigationService, NavigationService>();
builder.Services.AddHttpClient<IRecipeService, RecipeService>();

// ViewModels
builder.Services.AddTransient<LoginViewModel>();
builder.Services.AddTransient<MainViewModel>();
builder.Services.AddTransient<RecipeEditorViewModel>();
builder.Services.AddTransient<RecipeDetailViewModel>();
builder.Services.AddTransient<MealPlansViewModel>();
builder.Services.AddTransient<CreateMealPlanViewModel>();
builder.Services.AddTransient<MealPlanDetailViewModel>();
builder.Services.AddTransient<RecipePickerPageViewModel>();
builder.Services.AddTransient<ExtractRecipeViewModel>();
builder.Services.AddTransient<ExtractedRecipePreviewViewModel>();

// Pages
builder.Services.AddTransient<LoginPage>();
builder.Services.AddTransient<MainPage>();
builder.Services.AddTransient<AddRecipePage>();
builder.Services.AddTransient<EditRecipePage>();
builder.Services.AddTransient<RecipeDetailPage>();
builder.Services.AddTransient<MealPlansPage>();
builder.Services.AddTransient<CreateMealPlanPage>();
builder.Services.AddTransient<MealPlanDetailPage>();
builder.Services.AddTransient<RecipePickerPage>();
builder.Services.AddTransient<ExtractRecipePage>();
builder.Services.AddTransient<ExtractedRecipePreviewPage>();
```

#### Step 7.2: Remove Service Locator Anti-Pattern

Search and replace all instances of:
- `IPlatformApplication.Current?.Services.GetService<>()`
- `Application.Current?.Handler?.MauiContext?.Services.GetService<>()`

With constructor injection.

#### Step 7.3: Register Converters

Ensure all converters are registered in `App.xaml`.

---

## Page Migration Checklist

| Page | ViewModel | Priority | Status |
|------|-----------|----------|--------|
| LoginPage | LoginViewModel | Low | ⬜ |
| MealPlansPage | MealPlansViewModel | Low | ⬜ |
| MainPage | MainViewModel | High | ⬜ |
| CreateMealPlanPage | CreateMealPlanViewModel | Medium | ⬜ |
| AddRecipePage | RecipeEditorViewModel | High | ⬜ |
| EditRecipePage | RecipeEditorViewModel | High | ⬜ |
| RecipeDetailPage | RecipeDetailViewModel | High | ⬜ |
| MealPlanDetailPage | MealPlanDetailViewModel | High | ⬜ |
| RecipePickerPage | RecipePickerPageViewModel | Medium | ⬜ |
| ExtractRecipePage | ExtractRecipeViewModel | Medium | ⬜ |
| ExtractedRecipePreviewPage | ExtractedRecipePreviewViewModel | Medium | ⬜ |

---

## Verification Checklist

After completing migration:

- [ ] `dotnet build src/MealPlanOrganizer.Mobile/MealPlanOrganizer.Mobile.csproj` succeeds
- [ ] App launches without runtime errors
- [ ] LoginPage: Sign-in flow works
- [ ] MainPage: Recipes load, filters work, search works
- [ ] AddRecipePage: Can add ingredients, steps, photos; save works
- [ ] EditRecipePage: Loads existing recipe, updates work
- [ ] RecipeDetailPage: Displays correctly, rating submission works
- [ ] MealPlansPage: Plans load, navigation works
- [ ] CreateMealPlanPage: Date presets work, plan creation works
- [ ] MealPlanDetailPage: Plans display, recipe assignment works
- [ ] RecipePickerPage: Selection works, done button works
- [ ] ExtractRecipePage: All modes work, extraction succeeds
- [ ] ExtractedRecipePreviewPage: Editing works, save works
- [ ] No service locator usage remains (search codebase)
- [ ] Unit tests pass for 2-3 ViewModels

---

## Estimated Effort

| Phase | Pages | Est. Hours |
|-------|-------|------------|
| Phase 1: Infrastructure | - | 4-6 |
| Phase 2: Simple Pages | 2 | 4-6 |
| Phase 3: MainPage | 1 | 4-6 |
| Phase 4: Recipe Forms | 2 | 8-12 |
| Phase 5: Detail Pages | 2 | 8-12 |
| Phase 6: Picker/Extraction | 4 | 8-12 |
| Phase 7: Cleanup | - | 2-4 |
| **Total** | **11** | **38-58 hours** |

---

## Rollback Strategy

If issues arise during migration:

1. Each page is migrated independently
2. Keep old code commented until page is verified
3. Use feature branches for each phase
4. Merge phases only after testing

The migration is designed to be incremental—the app remains functional after each phase.

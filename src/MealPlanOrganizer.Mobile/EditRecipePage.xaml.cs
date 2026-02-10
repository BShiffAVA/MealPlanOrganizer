using MealPlanOrganizer.Mobile.ViewModels;

namespace MealPlanOrganizer.Mobile;

/// <summary>
/// Page for editing an existing recipe. Uses RecipeEditorViewModel in edit mode.
/// Receives recipeId via query parameter when navigating.
/// </summary>
public partial class EditRecipePage : ContentPage, IQueryAttributable
{
    private readonly RecipeEditorViewModel _viewModel;
    private bool _isInitialized;

    public EditRecipePage(RecipeEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    /// <summary>
    /// Receives the recipeId query parameter when navigated to.
    /// </summary>
    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("recipeId", out var recipeIdObj) && 
            recipeIdObj is string recipeIdStr && 
            Guid.TryParse(recipeIdStr, out var recipeId))
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                await _viewModel.InitializeForEditAsync(recipeId);
            }
        }
    }
}


using MealPlanOrganizer.Mobile.ViewModels;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Mobile;

/// <summary>
/// Page for quick rating of recipes from push notification reminders.
/// Shows pending ratings one at a time for streamlined rating submission.
/// </summary>
public partial class QuickRateRecipePage : ContentPage
{
    private readonly QuickRateRecipeViewModel _viewModel;
    private readonly ILogger<QuickRateRecipePage> _logger;
    private bool _isInitialized;

    public QuickRateRecipePage(QuickRateRecipeViewModel viewModel, ILogger<QuickRateRecipePage> logger)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _logger = logger;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isInitialized)
        {
            _isInitialized = true;
            _logger.LogInformation("QuickRateRecipePage appearing - initializing");
            await _viewModel.InitializeCommand.ExecuteAsync(null);
        }
    }
}

using MealPlanOrganizer.Mobile.ViewModels;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Mobile;

public partial class RecipeDetailPage : ContentPage, IQueryAttributable
{
	private readonly RecipeDetailViewModel _viewModel;
	private readonly ILogger<RecipeDetailPage> _logger;
	private bool _isInitialized;

	public RecipeDetailPage(RecipeDetailViewModel viewModel, ILogger<RecipeDetailPage> logger)
	{
		InitializeComponent();
		_viewModel = viewModel;
		_logger = logger;
		BindingContext = viewModel;
	}

	public async void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		try
		{
			if (query.TryGetValue("recipeId", out var recipeIdObj) &&
				recipeIdObj is string recipeIdStr &&
				Guid.TryParse(recipeIdStr, out var recipeId))
			{
				if (!_isInitialized)
				{
					_isInitialized = true;
					await _viewModel.LoadCommand.ExecuteAsync(recipeId);
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to apply query attributes for RecipeDetailPage");
			await DisplayAlertAsync("Error", $"Failed to load recipe: {ex.Message}", "OK");
		}
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		try
		{
			if (_isInitialized && _viewModel.RecipeId != Guid.Empty)
			{
				await _viewModel.LoadRecipeCommand.ExecuteAsync(null);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to refresh recipe on appearing");
		}
	}
}

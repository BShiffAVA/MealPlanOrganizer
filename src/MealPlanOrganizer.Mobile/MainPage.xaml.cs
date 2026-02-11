using MealPlanOrganizer.Mobile.Models;
using MealPlanOrganizer.Mobile.ViewModels;

namespace MealPlanOrganizer.Mobile;

public partial class MainPage : ContentPage
{
	private readonly MainViewModel _viewModel;

	public MainPage(MainViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _viewModel.LoadRecipesCommand.ExecuteAsync(null);
	}

	private async void OnImportRecipeClicked(object? sender, EventArgs e)
	{
		await Shell.Current.GoToAsync(nameof(ExtractRecipePage));
	}

	private async void OnAddRecipeClicked(object? sender, EventArgs e)
	{
		await Shell.Current.GoToAsync(nameof(AddRecipePage));
	}

	private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
	{
		_viewModel.SearchText = e.NewTextValue;
	}

	private void OnCuisineFilterChanged(object? sender, EventArgs e)
	{
		// Filter logic handled by ViewModel
	}

	private void OnPrepTimeFilterChanged(object? sender, EventArgs e)
	{
		// Filter logic handled by ViewModel
	}

	private void OnRatingFilterChanged(object? sender, EventArgs e)
	{
		// Filter logic handled by ViewModel
	}

	private void OnCreatorFilterChanged(object? sender, EventArgs e)
	{
		// Filter logic handled by ViewModel
	}

	private async void OnRecipeTapped(object? sender, TappedEventArgs e)
	{
		if (sender is VisualElement element && element.BindingContext is RecipeCard recipe)
		{
			await Shell.Current.GoToAsync($"{nameof(RecipeDetailPage)}?id={recipe.Id}");
		}
	}
}


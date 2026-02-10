using MealPlanOrganizer.Mobile.ViewModels;

namespace MealPlanOrganizer.Mobile;

public partial class AddRecipePage : ContentPage
{
    public AddRecipePage(RecipeEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.InitializeForNewRecipe();
    }
}

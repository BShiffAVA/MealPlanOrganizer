using MealPlanOrganizer.Mobile.ViewModels;

namespace MealPlanOrganizer.Mobile;

public partial class CreateMealPlanPage : ContentPage
{
    public CreateMealPlanPage(CreateMealPlanViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

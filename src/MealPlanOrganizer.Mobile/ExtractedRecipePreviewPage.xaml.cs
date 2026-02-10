using MealPlanOrganizer.Mobile.ViewModels;

namespace MealPlanOrganizer.Mobile;

public partial class ExtractedRecipePreviewPage : ContentPage
{
    public ExtractedRecipePreviewPage(ExtractedRecipePreviewViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

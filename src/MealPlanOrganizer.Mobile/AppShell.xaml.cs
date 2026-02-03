namespace MealPlanOrganizer.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		
		// Register routes for navigation
		Routing.RegisterRoute(nameof(RecipeDetailPage), typeof(RecipeDetailPage));
		Routing.RegisterRoute(nameof(EditRecipePage), typeof(EditRecipePage));
		Routing.RegisterRoute(nameof(ExtractRecipePage), typeof(ExtractRecipePage));
		Routing.RegisterRoute(nameof(ExtractedRecipePreviewPage), typeof(ExtractedRecipePreviewPage));
	}
}

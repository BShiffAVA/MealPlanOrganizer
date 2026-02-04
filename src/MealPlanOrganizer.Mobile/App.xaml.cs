using Microsoft.Extensions.DependencyInjection;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile;

public partial class App : Application
{
	private readonly IServiceProvider _serviceProvider;

	public App(IServiceProvider serviceProvider)
	{
		InitializeComponent();
		_serviceProvider = serviceProvider;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Start with LoginPage - it will check auth state and navigate accordingly
		var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
		return new Window(new NavigationPage(loginPage));
	}
}
using Android.App;
using Android.Runtime;
using Android.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Platform;

namespace MealPlanOrganizer.Mobile;

[Application]
public class MainApplication : MauiApplication
{
	private static readonly ILogger<MainApplication> _logger = new LoggerFactory().CreateLogger<MainApplication>();

	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
			
	}

	public override void OnCreate()
	{	
		base.OnCreate();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

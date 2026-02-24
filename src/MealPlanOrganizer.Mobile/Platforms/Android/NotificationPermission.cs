namespace MealPlanOrganizer.Mobile.Platforms.Android;

public static class NotificationPermission
{
    public static async Task<bool> EnsureAsync()
    {
        // Only required on Android 13+; on earlier versions, it will typically be Granted.
        var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();

        if (status == PermissionStatus.Granted)
            return true;

        // Optional: show rationale (Android may suggest this after a prior denial)
        if (Permissions.ShouldShowRationale<Permissions.PostNotifications>())
        {
            await Shell.Current.DisplayAlert(
                "Enable notifications",
                "We use notifications to alert you to rate recipes.",
                "OK");
        }

        status = await Permissions.RequestAsync<Permissions.PostNotifications>();
        return status == PermissionStatus.Granted;
    }
}

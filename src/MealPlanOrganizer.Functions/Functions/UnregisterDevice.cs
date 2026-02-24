using System.Net;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Functions;

/// <summary>
/// Unregisters a device from push notifications.
/// </summary>
public class UnregisterDevice
{
    private readonly ILogger<UnregisterDevice> _logger;
    private readonly AppDbContext _context;
    private readonly AuthenticationHelper _authHelper;
    private readonly INotificationService _notificationService;

    public UnregisterDevice(
        ILogger<UnregisterDevice> logger,
        AppDbContext context,
        AuthenticationHelper authHelper,
        INotificationService notificationService)
    {
        _logger = logger;
        _context = context;
        _authHelper = authHelper;
        _notificationService = notificationService;
    }

    [Function("UnregisterDevice")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "devices/{platform}")] HttpRequestData req,
        string platform)
    {
        _logger.LogInformation("Unregistering device from push notifications, platform: {Platform}", platform);

        // Authenticate the request
        var authResult = await _authHelper.AuthenticateAsync(req);
        if (!authResult.IsAuthenticated)
        {
            _logger.LogWarning("Authentication failed: {Error}", authResult.ErrorMessage);
            var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorized.WriteAsJsonAsync(new { message = authResult.ErrorMessage ?? "Unauthorized" });
            return unauthorized;
        }

        var externalId = authResult.UserId;
        if (string.IsNullOrEmpty(externalId))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { message = "Missing user ID in token" });
            return badRequest;
        }

        try
        {
            // Find user by external ID
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.ExternalIdObjectId == externalId);

            if (user == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(new { message = "User not registered" });
                return notFound;
            }

            // Find active registrations for this user and platform
            var registrations = await _context.DeviceRegistrations
                .Where(d => d.UserId == user.Id && d.Platform == platform.ToLowerInvariant() && d.IsActive)
                .ToListAsync();

            if (registrations.Count == 0)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(new { message = "No device registrations found for this platform" });
                return notFound;
            }

            foreach (var registration in registrations)
            {
                // Unregister from Azure Notification Hubs using Installation API
                // Use the device registration Id as the installation ID
                var installationId = registration.NotificationHubRegistrationId ?? registration.Id.ToString();
                await _notificationService.UnregisterDeviceAsync(installationId);

                // Mark as inactive (soft delete)
                registration.IsActive = false;
                registration.UpdatedUtc = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Unregistered {Count} device(s) for user {UserId}, platform {Platform}", 
                registrations.Count, user.Id, platform);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                message = "Device(s) unregistered successfully",
                count = registrations.Count
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unregister device");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteAsJsonAsync(new { message = "Failed to unregister device" });
            return error;
        }
    }
}

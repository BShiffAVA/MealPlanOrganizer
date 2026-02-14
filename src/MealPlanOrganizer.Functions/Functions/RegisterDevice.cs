using System.Net;
using System.Text.Json;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Functions;

/// <summary>
/// Registers a device for push notifications.
/// </summary>
public class RegisterDevice
{
    private readonly ILogger<RegisterDevice> _logger;
    private readonly AppDbContext _context;
    private readonly AuthenticationHelper _authHelper;
    private readonly INotificationService _notificationService;

    public RegisterDevice(
        ILogger<RegisterDevice> logger,
        AppDbContext context,
        AuthenticationHelper authHelper,
        INotificationService notificationService)
    {
        _logger = logger;
        _context = context;
        _authHelper = authHelper;
        _notificationService = notificationService;
    }

    [Function("RegisterDevice")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "devices/register")] HttpRequestData req)
    {
        _logger.LogInformation("Registering device for push notifications");

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

            // Parse request body
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<RegisterDeviceRequest>(
                requestBody ?? "{}",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.Platform) || string.IsNullOrWhiteSpace(request.PushToken))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { message = "Platform and PushToken are required" });
                return badRequest;
            }

            // Validate platform
            var validPlatforms = new[] { "ios", "android", "windows" };
            if (!validPlatforms.Contains(request.Platform.ToLowerInvariant()))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { message = "Platform must be 'ios', 'android', or 'windows'" });
                return badRequest;
            }

            // Register with Azure Notification Hubs
            var hubRegistrationId = await _notificationService.RegisterDeviceAsync(
                user.Id, request.Platform.ToLowerInvariant(), request.PushToken);

            // Check if this device is already registered
            var existingRegistration = await _context.DeviceRegistrations
                .FirstOrDefaultAsync(d => 
                    d.UserId == user.Id && 
                    d.Platform == request.Platform.ToLowerInvariant() && 
                    d.PushToken == request.PushToken);

            if (existingRegistration != null)
            {
                // Update existing registration
                existingRegistration.NotificationHubRegistrationId = hubRegistrationId;
                existingRegistration.UpdatedUtc = DateTime.UtcNow;
                existingRegistration.IsActive = true;
            }
            else
            {
                // Create new registration
                var deviceRegistration = new DeviceRegistration
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Platform = request.Platform.ToLowerInvariant(),
                    PushToken = request.PushToken,
                    NotificationHubRegistrationId = hubRegistrationId,
                    CreatedUtc = DateTime.UtcNow,
                    IsActive = true
                };

                _context.DeviceRegistrations.Add(deviceRegistration);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Device registered successfully for user {UserId}, platform {Platform}", 
                user.Id, request.Platform);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                message = "Device registered successfully",
                platform = request.Platform.ToLowerInvariant(),
                registrationId = hubRegistrationId
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register device");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteAsJsonAsync(new { message = "Failed to register device" });
            return error;
        }
    }
}

/// <summary>
/// Request model for device registration.
/// </summary>
public class RegisterDeviceRequest
{
    /// <summary>
    /// Platform: "ios", "android", or "windows".
    /// </summary>
    public string? Platform { get; set; }

    /// <summary>
    /// Device push token (APNs token, FCM token, or WNS channel URI).
    /// </summary>
    public string? PushToken { get; set; }
}

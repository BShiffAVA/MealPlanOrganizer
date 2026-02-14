using System.Net;
using System.Text.Json;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Functions;

/// <summary>
/// Updates the status of a pending rating (Complete or Dismiss).
/// </summary>
public class UpdatePendingRating
{
    private readonly ILogger<UpdatePendingRating> _logger;
    private readonly AppDbContext _context;
    private readonly AuthenticationHelper _authHelper;

    public UpdatePendingRating(ILogger<UpdatePendingRating> logger, AppDbContext context, AuthenticationHelper authHelper)
    {
        _logger = logger;
        _context = context;
        _authHelper = authHelper;
    }

    [Function("UpdatePendingRating")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "ratings/pending/{id:guid}")] HttpRequestData req,
        Guid id)
    {
        _logger.LogInformation("Updating pending rating {Id}", id);

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

            // Find the pending rating
            var pendingRating = await _context.PendingRatings
                .FirstOrDefaultAsync(pr => pr.Id == id && pr.UserId == user.Id);

            if (pendingRating == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(new { message = "Pending rating not found or does not belong to you" });
                return notFound;
            }

            // Parse request body
            var requestBody = await req.ReadAsStringAsync();
            var updateRequest = JsonSerializer.Deserialize<UpdatePendingRatingRequest>(
                requestBody ?? "{}", 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (updateRequest == null || string.IsNullOrEmpty(updateRequest.Status))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { message = "Status is required (Completed or Dismissed)" });
                return badRequest;
            }

            // Validate status
            var validStatuses = new[] { "Completed", "Dismissed" };
            if (!validStatuses.Contains(updateRequest.Status, StringComparer.OrdinalIgnoreCase))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { message = "Status must be 'Completed' or 'Dismissed'" });
                return badRequest;
            }

            // Update the pending rating
            pendingRating.Status = updateRequest.Status;
            pendingRating.CompletedUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated pending rating {Id} to status {Status}", id, updateRequest.Status);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { 
                id = pendingRating.Id, 
                status = pendingRating.Status,
                completedUtc = pendingRating.CompletedUtc
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update pending rating");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteAsJsonAsync(new { message = "Failed to update pending rating" });
            return error;
        }
    }
}

/// <summary>
/// Request model for updating a pending rating status.
/// </summary>
public class UpdatePendingRatingRequest
{
    /// <summary>
    /// New status: "Completed" or "Dismissed".
    /// </summary>
    public string? Status { get; set; }
}

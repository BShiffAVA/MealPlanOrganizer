using System.Net;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Functions;

/// <summary>
/// Marks a pending rating as dismissed when the user chooses to skip rating a recipe.
/// </summary>
public class DismissPendingRating
{
    private readonly ILogger<DismissPendingRating> _logger;
    private readonly AppDbContext _context;
    private readonly AuthenticationHelper _authHelper;

    public DismissPendingRating(ILogger<DismissPendingRating> logger, AppDbContext context, AuthenticationHelper authHelper)
    {
        _logger = logger;
        _context = context;
        _authHelper = authHelper;
    }

    [Function("DismissPendingRating")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "pending-ratings/{id:guid}/dismiss")] HttpRequestData req,
        Guid id)
    {
        _logger.LogInformation("Dismissing pending rating {Id}", id);

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
                _logger.LogWarning("Pending rating {Id} not found for user {UserId}", id, user.Id);
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(new { message = "Pending rating not found or does not belong to you" });
                return notFound;
            }

            // Check if already dismissed or completed
            if (pendingRating.Status == "Dismissed")
            {
                _logger.LogInformation("Pending rating {Id} is already dismissed", id);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    id = pendingRating.Id,
                    status = pendingRating.Status,
                    completedUtc = pendingRating.CompletedUtc,
                    message = "Already dismissed"
                });
                return response;
            }

            if (pendingRating.Status == "Completed")
            {
                _logger.LogInformation("Pending rating {Id} is already completed, cannot dismiss", id);
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteAsJsonAsync(new
                {
                    id = pendingRating.Id,
                    status = pendingRating.Status,
                    message = "Cannot dismiss a completed rating"
                });
                return conflict;
            }

            // Update the pending rating to dismissed
            pendingRating.Status = "Dismissed";
            pendingRating.CompletedUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully dismissed pending rating {Id}", id);

            var successResponse = req.CreateResponse(HttpStatusCode.OK);
            await successResponse.WriteAsJsonAsync(new
            {
                id = pendingRating.Id,
                status = pendingRating.Status,
                completedUtc = pendingRating.CompletedUtc
            });
            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dismiss pending rating {Id}", id);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteAsJsonAsync(new { message = "Failed to dismiss pending rating" });
            return error;
        }
    }
}

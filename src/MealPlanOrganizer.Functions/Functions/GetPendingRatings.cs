using System.Net;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Models;
using MealPlanOrganizer.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Functions;

/// <summary>
/// Gets pending ratings for the current authenticated user.
/// Returns recipes that were served and need to be rated.
/// </summary>
public class GetPendingRatings
{
    private readonly ILogger<GetPendingRatings> _logger;
    private readonly AppDbContext _context;
    private readonly AuthenticationHelper _authHelper;

    public GetPendingRatings(ILogger<GetPendingRatings> logger, AppDbContext context, AuthenticationHelper authHelper)
    {
        _logger = logger;
        _context = context;
        _authHelper = authHelper;
    }

    [Function("GetPendingRatings")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ratings/pending")] HttpRequestData req)
    {
        _logger.LogInformation("Getting pending ratings for current user");

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

            // Get pending ratings for this user
            var pendingRatings = await _context.PendingRatings
                .Include(pr => pr.Recipe)
                .Where(pr => pr.UserId == user.Id && pr.Status == "Pending")
                .OrderByDescending(pr => pr.ServedDate)
                .Select(pr => new PendingRatingResponse
                {
                    Id = pr.Id,
                    RecipeId = pr.RecipeId,
                    RecipeTitle = pr.Recipe != null ? pr.Recipe.Title : "Unknown Recipe",
                    RecipeImageUrl = pr.Recipe != null ? pr.Recipe.ImageUrl : null,
                    CuisineType = pr.Recipe != null ? pr.Recipe.CuisineType : null,
                    MealPlanId = pr.MealPlanId,
                    MealPlanRecipeId = pr.MealPlanRecipeId,
                    ServedDate = pr.ServedDate,
                    CreatedUtc = pr.CreatedUtc
                })
                .ToListAsync();

            _logger.LogInformation("Found {Count} pending ratings for user {UserId}", pendingRatings.Count, user.Id);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(pendingRatings);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending ratings");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteAsJsonAsync(new { message = "Failed to get pending ratings" });
            return error;
        }
    }
}

using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Services;

namespace MealPlanOrganizer.Functions.Functions;

public class GetUserRatingHistory
{
    private readonly ILogger<GetUserRatingHistory> _logger;
    private readonly AppDbContext _context;
    private readonly AuthenticationHelper _authHelper;

    public GetUserRatingHistory(ILogger<GetUserRatingHistory> logger, AppDbContext context, AuthenticationHelper authHelper)
    {
        _logger = logger;
        _context = context;
        _authHelper = authHelper;
    }

    [Function("GetUserRatingHistory")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ratings/me")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Getting rating history for current user");

        // Authenticate the request
        var authResult = await _authHelper.AuthenticateAsync(req);
        if (!authResult.IsAuthenticated)
        {
            _logger.LogWarning("Unauthorized rating history request: {Error}", authResult.ErrorMessage);
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteAsJsonAsync(new { message = "Authentication required" });
            return unauthorizedResponse;
        }

        var userId = authResult.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Authenticated but no user ID in claims");
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteAsJsonAsync(new { message = "Authentication required" });
            return unauthorizedResponse;
        }

        // Get all ratings for the user
        var ratings = await _context.RecipeRatings
            .Where(r => r.UserId == userId)
            .Include(r => r.Recipe)
            .OrderByDescending(r => r.RatedUtc)
            .ToListAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            userId = userId,
            totalRatings = ratings.Count,
            ratings = ratings.Select(r => new
            {
                ratingId = r.Id,
                recipeId = r.RecipeId,
                recipeName = r.Recipe?.Title,
                rating = r.Rating,
                comments = r.Comments,
                frequencyPreference = r.FrequencyPreference,
                ratedUtc = r.RatedUtc
            }).ToList()
        });

        return response;
    }
}

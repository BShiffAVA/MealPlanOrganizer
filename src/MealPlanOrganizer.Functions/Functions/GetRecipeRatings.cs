using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Services;

namespace MealPlanOrganizer.Functions.Functions;

public class GetRecipeRatings
{
    private readonly ILogger<GetRecipeRatings> _logger;
    private readonly AppDbContext _context;
    private readonly AuthenticationHelper _authHelper;

    public GetRecipeRatings(ILogger<GetRecipeRatings> logger, AppDbContext context, AuthenticationHelper authHelper)
    {
        _logger = logger;
        _context = context;
        _authHelper = authHelper;
    }

    [Function("GetRecipeRatings")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/{id:guid}/ratings")] HttpRequestData req,
        Guid id,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Getting ratings for recipe {RecipeId}", id);

        // Try to authenticate (optional for this endpoint)
        var authResult = await _authHelper.AuthenticateAsync(req);
        var currentUserId = authResult.IsAuthenticated ? authResult.UserId : null;

        // Verify recipe exists
        var recipe = await _context.Recipes.FindAsync(id);
        if (recipe == null)
        {
            _logger.LogWarning("Recipe with ID {RecipeId} not found", id);
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new { message = $"Recipe with ID {id} not found" });
            return notFoundResponse;
        }

        // Get all ratings for the recipe
        var ratings = await _context.RecipeRatings
            .Where(r => r.RecipeId == id)
            .OrderByDescending(r => r.RatedUtc)
            .ToListAsync();

        var averageRating = ratings.Count > 0
            ? Math.Round(ratings.Average(r => r.Rating), 1)
            : 0;

        // Calculate star count breakdown
        var starBreakdown = new Dictionary<int, int>
        {
            { 1, ratings.Count(r => r.Rating == 1) },
            { 2, ratings.Count(r => r.Rating == 2) },
            { 3, ratings.Count(r => r.Rating == 3) },
            { 4, ratings.Count(r => r.Rating == 4) },
            { 5, ratings.Count(r => r.Rating == 5) }
        };

        // Get current user's most recent personal rating (if authenticated)
        object? userPersonalRating = null;
        if (!string.IsNullOrEmpty(currentUserId))
        {
            var userRating = ratings.FirstOrDefault(r => r.UserId == currentUserId);
            if (userRating != null)
            {
                userPersonalRating = new
                {
                    ratingId = userRating.Id,
                    rating = userRating.Rating,
                    comments = userRating.Comments,
                    frequencyPreference = userRating.FrequencyPreference,
                    ratedUtc = userRating.RatedUtc
                };
            }
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            recipeId = id,
            totalRatings = ratings.Count,
            averageRating = averageRating,
            starBreakdown = starBreakdown,
            userPersonalRating = userPersonalRating,
            ratings = ratings.Select(r => new
            {
                ratingId = r.Id,
                userId = r.UserId,
                rating = r.Rating,
                comments = r.Comments,
                frequencyPreference = r.FrequencyPreference,
                ratedUtc = r.RatedUtc
            }).ToList()
        });

        return response;
    }
}

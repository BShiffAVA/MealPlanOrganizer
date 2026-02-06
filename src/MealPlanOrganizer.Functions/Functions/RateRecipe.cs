using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Models;
using MealPlanOrganizer.Functions.Services;

namespace MealPlanOrganizer.Functions.Functions;

public class RateRecipe
{
    private readonly ILogger<RateRecipe> _logger;
    private readonly AppDbContext _context;
    private readonly AuthenticationHelper _authHelper;

    public RateRecipe(ILogger<RateRecipe> logger, AppDbContext context, AuthenticationHelper authHelper)
    {
        _logger = logger;
        _context = context;
        _authHelper = authHelper;
    }

    [Function("RateRecipe")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "recipes/{id:guid}/ratings")] HttpRequestData req,
        Guid id,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Submitting rating for recipe {RecipeId}", id);

        // Authenticate the request
        var authResult = await _authHelper.AuthenticateAsync(req);
        if (!authResult.IsAuthenticated)
        {
            _logger.LogWarning("Unauthorized rating attempt: {Error}", authResult.ErrorMessage);
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteAsJsonAsync(new { message = "Authentication required to rate recipes" });
            return unauthorizedResponse;
        }

        // Use display name if available, or email, or userId as fallback
        var userId = authResult.UserDisplayName 
                  ?? authResult.UserEmail 
                  ?? authResult.UserId 
                  ?? "anonymous";
        
        _logger.LogInformation("Authenticated user: {UserId} (from DisplayName: {DisplayName}, Email: {Email}, UserId: {RawUserId})", 
            userId, authResult.UserDisplayName, authResult.UserEmail, authResult.UserId);

        // Verify recipe exists
        var recipe = await _context.Recipes.FindAsync(id);
        if (recipe == null)
        {
            _logger.LogWarning("Recipe with ID {RecipeId} not found", id);
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new { message = $"Recipe with ID {id} not found" });
            return notFoundResponse;
        }

        // Parse request body
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        RateRecipeRequest? rateRequest = null;

        try
        {
            var requestBody = await req.ReadAsStringAsync();
            rateRequest = System.Text.Json.JsonSerializer.Deserialize<RateRecipeRequest>(requestBody, options);
        }
        catch
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { message = "Invalid request body" });
            return badResponse;
        }

        if (rateRequest == null || rateRequest.Rating < 1 || rateRequest.Rating > 5)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { message = "Rating must be between 1 and 5" });
            return badResponse;
        }

        if (rateRequest.Comments != null && rateRequest.Comments.Length > 500)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { message = "Comments must be 500 characters or less" });
            return badResponse;
        }

        // Validate frequency preference if provided
        if (!string.IsNullOrEmpty(rateRequest.FrequencyPreference) &&
            !RateRecipeRequest.ValidFrequencies.Contains(rateRequest.FrequencyPreference))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new 
            { 
                message = $"Invalid frequency preference. Valid values: {string.Join(", ", RateRecipeRequest.ValidFrequencies)}" 
            });
            return badResponse;
        }

        // Check if user has already rated this recipe today (one rating per day limit)
        var todayUtc = DateTime.UtcNow.Date;
        var existingRatingToday = await _context.RecipeRatings
            .FirstOrDefaultAsync(r => r.RecipeId == id && 
                                       r.UserId == userId && 
                                       r.RatedUtc.Date == todayUtc);

        if (existingRatingToday != null)
        {
            _logger.LogWarning("User {UserId} has already rated recipe {RecipeId} today", userId, id);
            var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
            await conflictResponse.WriteAsJsonAsync(new 
            { 
                message = "You have already rated this recipe today. You can add another rating tomorrow.",
                existingRating = new
                {
                    rating = existingRatingToday.Rating,
                    comments = existingRatingToday.Comments,
                    frequencyPreference = existingRatingToday.FrequencyPreference,
                    ratedUtc = existingRatingToday.RatedUtc
                }
            });
            return conflictResponse;
        }

        // Create new rating (ratings accumulate as historical record)
        var newRating = new RecipeRating
        {
            Id = Guid.NewGuid(),
            RecipeId = id,
            UserId = userId,
            Rating = rateRequest.Rating,
            Comments = rateRequest.Comments,
            FrequencyPreference = rateRequest.FrequencyPreference,
            RatedUtc = DateTime.UtcNow
        };
        _context.RecipeRatings.Add(newRating);

        await _context.SaveChangesAsync();

        // Calculate and return average rating
        var allRatings = await _context.RecipeRatings
            .Where(r => r.RecipeId == id)
            .ToListAsync();

        var averageRating = allRatings.Count > 0
            ? Math.Round(allRatings.Average(r => r.Rating), 1)
            : 0;

        _logger.LogInformation("User {UserId} rated recipe {RecipeId} with {Stars} stars", userId, id, rateRequest.Rating);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(new
        {
            message = "Rating submitted successfully",
            ratingId = newRating.Id,
            rating = rateRequest.Rating,
            frequencyPreference = rateRequest.FrequencyPreference,
            averageRating = averageRating,
            totalRatings = allRatings.Count
        });

        return response;
    }
}

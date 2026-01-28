using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Models;

namespace MealPlanOrganizer.Functions.Functions;

public class RateRecipe
{
    private readonly ILogger<RateRecipe> _logger;
    private readonly AppDbContext _context;

    public RateRecipe(ILogger<RateRecipe> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    [Function("RateRecipe")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "recipes/{id:guid}/ratings")] HttpRequestData req,
        Guid id)
    {
        _logger.LogInformation("Submitting rating for recipe {RecipeId}", id);

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

        // Extract user ID from claims (placeholder: "system" for now)
        var userId = "system";

        // Check if user has already rated this recipe
        var existingRating = await _context.RecipeRatings
            .FirstOrDefaultAsync(r => r.RecipeId == id && r.UserId == userId);

        if (existingRating != null)
        {
            // Update existing rating
            existingRating.Rating = rateRequest.Rating;
            existingRating.Comments = rateRequest.Comments;
            existingRating.RatedUtc = DateTime.UtcNow;
            _context.RecipeRatings.Update(existingRating);
        }
        else
        {
            // Create new rating
            var newRating = new RecipeRating
            {
                Id = Guid.NewGuid(),
                RecipeId = id,
                UserId = userId,
                Rating = rateRequest.Rating,
                Comments = rateRequest.Comments,
                RatedUtc = DateTime.UtcNow
            };
            _context.RecipeRatings.Add(newRating);
        }

        await _context.SaveChangesAsync();

        // Calculate and return average rating
        var allRatings = await _context.RecipeRatings
            .Where(r => r.RecipeId == id)
            .ToListAsync();

        var averageRating = allRatings.Count > 0
            ? Math.Round(allRatings.Average(r => r.Rating), 1)
            : 0;

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            message = "Rating submitted successfully",
            rating = rateRequest.Rating,
            averageRating = averageRating,
            totalRatings = allRatings.Count
        });

        return response;
    }
}

using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Data;

namespace MealPlanOrganizer.Functions.Functions;

public class GetRecipeRatings
{
    private readonly ILogger<GetRecipeRatings> _logger;
    private readonly AppDbContext _context;

    public GetRecipeRatings(ILogger<GetRecipeRatings> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    [Function("GetRecipeRatings")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/{id:guid}/ratings")] HttpRequestData req,
        Guid id)
    {
        _logger.LogInformation("Getting ratings for recipe {RecipeId}", id);

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

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            recipeId = id,
            totalRatings = ratings.Count,
            averageRating = averageRating,
            ratings = ratings.Select(r => new
            {
                userId = r.UserId,
                rating = r.Rating,
                comments = r.Comments,
                ratedUtc = r.RatedUtc
            }).ToList()
        });

        return response;
    }
}

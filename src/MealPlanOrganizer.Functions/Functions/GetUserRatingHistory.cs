using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Data;

namespace MealPlanOrganizer.Functions.Functions;

public class GetUserRatingHistory
{
    private readonly ILogger<GetUserRatingHistory> _logger;
    private readonly AppDbContext _context;

    public GetUserRatingHistory(ILogger<GetUserRatingHistory> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    [Function("GetUserRatingHistory")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ratings/me")] HttpRequestData req)
    {
        _logger.LogInformation("Getting rating history for current user");

        // Extract user ID from claims (placeholder: "system" for now)
        var userId = "system";

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
                recipeId = r.RecipeId,
                recipeName = r.Recipe?.Title,
                rating = r.Rating,
                comments = r.Comments,
                ratedUtc = r.RatedUtc
            }).ToList()
        });

        return response;
    }
}

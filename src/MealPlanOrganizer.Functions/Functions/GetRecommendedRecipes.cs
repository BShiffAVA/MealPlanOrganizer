using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Services;

namespace MealPlanOrganizer.Functions.Functions;

public class GetRecommendedRecipes
{
    private readonly ILogger<GetRecommendedRecipes> _logger;
    private readonly IRecipeRecommendationService _recommendationService;
    private readonly AuthenticationHelper _authHelper;

    public GetRecommendedRecipes(
        ILogger<GetRecommendedRecipes> logger,
        IRecipeRecommendationService recommendationService,
        AuthenticationHelper authHelper)
    {
        _logger = logger;
        _recommendationService = recommendationService;
        _authHelper = authHelper;
    }

    [Function("GetRecommendedRecipes")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/recommended")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Getting recommended recipes");

        // Authenticate the request
        var authResult = await _authHelper.AuthenticateAsync(req);
        if (!authResult.IsAuthenticated)
        {
            _logger.LogWarning("Unauthorized recommendation request: {Error}", authResult.ErrorMessage);
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteAsJsonAsync(new { message = "Authentication required" });
            return unauthorizedResponse;
        }

        // Parse weekStart query parameter (default to next Monday)
        DateTime weekStartDate;
        var weekStartParam = req.Url.Query.Contains("weekStart")
            ? System.Web.HttpUtility.ParseQueryString(req.Url.Query)["weekStart"]
            : null;

        if (!string.IsNullOrEmpty(weekStartParam) && DateTime.TryParse(weekStartParam, out var parsedDate))
        {
            weekStartDate = parsedDate.Date;
        }
        else
        {
            // Default to next Monday
            weekStartDate = GetNextMonday(DateTime.UtcNow);
        }

        _logger.LogInformation("Generating recommendations for week starting {WeekStart}", weekStartDate);

        try
        {
            var recommendations = await _recommendationService.GetRecommendedRecipesAsync(weekStartDate);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                weekStartDate = weekStartDate.ToString("yyyy-MM-dd"),
                totalRecipes = recommendations.Count,
                recipes = recommendations.Select(r => new
                {
                    recipeId = r.RecipeId,
                    title = r.Title,
                    imageUrl = r.ImageUrl,
                    cuisineType = r.CuisineType,
                    prepTimeMinutes = r.PrepTimeMinutes,
                    cookTimeMinutes = r.CookTimeMinutes,
                    score = r.Score,
                    averageRating = r.AverageRating,
                    ratingCount = r.RatingCount,
                    lastCookedDate = r.LastCookedDate?.ToString("yyyy-MM-dd"),
                    frequencyPreference = r.FrequencyPreference,
                    reasonCodes = r.ReasonCodes
                })
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating recommendations");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { message = "Error generating recommendations" });
            return errorResponse;
        }
    }

    private static DateTime GetNextMonday(DateTime from)
    {
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)from.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7; // If today is Monday, get next Monday
        return from.Date.AddDays(daysUntilMonday);
    }
}

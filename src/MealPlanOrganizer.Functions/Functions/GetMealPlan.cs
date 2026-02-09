using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Services;

namespace MealPlanOrganizer.Functions.Functions;

public class GetMealPlan
{
    private readonly ILogger<GetMealPlan> _logger;
    private readonly AppDbContext _context;
    private readonly AuthenticationHelper _authHelper;
    private readonly IBlobUrlService _blobUrlService;

    public GetMealPlan(
        ILogger<GetMealPlan> logger,
        AppDbContext context,
        AuthenticationHelper authHelper,
        IBlobUrlService blobUrlService)
    {
        _logger = logger;
        _context = context;
        _authHelper = authHelper;
        _blobUrlService = blobUrlService;
    }

    [Function("GetMealPlan")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "mealplans/{id:guid}")] HttpRequestData req,
        Guid id,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Getting meal plan {MealPlanId}", id);

        // Authenticate the request
        var authResult = await _authHelper.AuthenticateAsync(req);
        if (!authResult.IsAuthenticated)
        {
            _logger.LogWarning("Unauthorized get meal plan: {Error}", authResult.ErrorMessage);
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteAsJsonAsync(new { message = "Authentication required" });
            return unauthorizedResponse;
        }

        // Find the meal plan with recipes
        var mealPlan = await _context.MealPlans
            .Include(mp => mp.Recipes)
                .ThenInclude(mpr => mpr.Recipe)
            .FirstOrDefaultAsync(mp => mp.Id == id);

        if (mealPlan == null)
        {
            _logger.LogWarning("Meal plan {MealPlanId} not found", id);
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new { message = $"Meal plan with ID {id} not found" });
            return notFoundResponse;
        }

        // Group recipes by day and generate SAS URLs
        var recipesByDay = new Dictionary<string, object?>();
        foreach (var recipeGroup in mealPlan.Recipes.OrderBy(r => r.Day).GroupBy(r => r.Day.Date))
        {
            var firstRecipe = recipeGroup.FirstOrDefault();
            if (firstRecipe?.Recipe != null)
            {
                var sasUrl = await _blobUrlService.NormalizeImageUrlAsync(firstRecipe.Recipe.ImageUrl);
                recipesByDay[recipeGroup.Key.ToString("yyyy-MM-dd")] = new
                {
                    assignmentId = firstRecipe.Id,
                    recipeId = firstRecipe.RecipeId,
                    recipeTitle = firstRecipe.Recipe.Title,
                    recipeImageUrl = sasUrl,
                    cuisineType = firstRecipe.Recipe.CuisineType,
                    prepTimeMinutes = firstRecipe.Recipe.PrepTimeMinutes,
                    cookTimeMinutes = firstRecipe.Recipe.CookTimeMinutes
                };
            }
        }

        // Generate all days in the meal plan range
        var allDays = new List<object>();
        for (var day = mealPlan.StartDate; day <= mealPlan.EndDate; day = day.AddDays(1))
        {
            var dayKey = day.ToString("yyyy-MM-dd");
            recipesByDay.TryGetValue(dayKey, out var recipe);
            
            allDays.Add(new
            {
                date = dayKey,
                dayOfWeek = day.DayOfWeek.ToString(),
                recipe = recipe
            });
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            id = mealPlan.Id,
            name = mealPlan.Name,
            startDate = mealPlan.StartDate.ToString("yyyy-MM-dd"),
            endDate = mealPlan.EndDate.ToString("yyyy-MM-dd"),
            createdBy = mealPlan.CreatedBy,
            status = mealPlan.Status,
            createdUtc = mealPlan.CreatedUtc,
            totalDays = (mealPlan.EndDate - mealPlan.StartDate).Days + 1,
            recipesAssigned = mealPlan.Recipes.Count,
            days = allDays
        });

        return response;
    }
}

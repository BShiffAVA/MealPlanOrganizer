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

    public GetMealPlan(
        ILogger<GetMealPlan> logger,
        AppDbContext context,
        AuthenticationHelper authHelper)
    {
        _logger = logger;
        _context = context;
        _authHelper = authHelper;
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

        // Group recipes by day
        var recipesByDay = mealPlan.Recipes
            .OrderBy(r => r.Day)
            .GroupBy(r => r.Day.Date)
            .ToDictionary(
                g => g.Key.ToString("yyyy-MM-dd"),
                g => g.Select(r => new
                {
                    assignmentId = r.Id,
                    recipeId = r.RecipeId,
                    recipeTitle = r.Recipe?.Title,
                    recipeImageUrl = r.Recipe?.ImageUrl,
                    cuisineType = r.Recipe?.CuisineType,
                    prepTimeMinutes = r.Recipe?.PrepTimeMinutes,
                    cookTimeMinutes = r.Recipe?.CookTimeMinutes
                }).FirstOrDefault() // Only one dinner per day
            );

        // Generate all days in the meal plan range
        var allDays = new List<object>();
        for (var day = mealPlan.StartDate; day <= mealPlan.EndDate; day = day.AddDays(1))
        {
            var dayKey = day.ToString("yyyy-MM-dd");
            var recipe = recipesByDay.ContainsKey(dayKey) ? recipesByDay[dayKey] : null;
            
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

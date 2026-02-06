using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Services;

namespace MealPlanOrganizer.Functions.Functions;

/// <summary>
/// Removes a recipe assignment from a specific day in a meal plan.
/// </summary>
public class RemoveRecipeFromMealPlan
{
    private readonly ILogger<RemoveRecipeFromMealPlan> _logger;
    private readonly AppDbContext _context;
    private readonly AuthenticationHelper _authHelper;

    public RemoveRecipeFromMealPlan(
        ILogger<RemoveRecipeFromMealPlan> logger,
        AppDbContext context,
        AuthenticationHelper authHelper)
    {
        _logger = logger;
        _context = context;
        _authHelper = authHelper;
    }

    [Function("RemoveRecipeFromMealPlan")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "mealplans/{id}/recipes/{day}")] 
        HttpRequestData req,
        string id,
        string day)
    {
        _logger.LogInformation("RemoveRecipeFromMealPlan triggered for meal plan {MealPlanId}, day {Day}", id, day);

        // Authenticate
        var authResult = await _authHelper.AuthenticateAsync(req);
        if (!authResult.IsAuthenticated)
        {
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteAsJsonAsync(new { message = authResult.ErrorMessage ?? "Unauthorized" });
            return unauthorizedResponse;
        }

        // Parse meal plan ID
        if (!Guid.TryParse(id, out var mealPlanId))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { message = "Invalid meal plan ID format" });
            return badResponse;
        }

        // Parse day
        if (!DateTime.TryParse(day, out var dayDate))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { message = "Invalid day format. Use yyyy-MM-dd" });
            return badResponse;
        }

        // Verify meal plan exists
        var mealPlan = await _context.MealPlans
            .FirstOrDefaultAsync(mp => mp.Id == mealPlanId);

        if (mealPlan == null)
        {
            _logger.LogWarning("Meal plan {MealPlanId} not found", mealPlanId);
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new { message = "Meal plan not found" });
            return notFoundResponse;
        }

        // Find and remove the recipe assignment
        var assignment = await _context.MealPlanRecipes
            .FirstOrDefaultAsync(mpr => mpr.MealPlanId == mealPlanId && mpr.Day == dayDate);

        if (assignment == null)
        {
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new { message = "No recipe assigned to this day" });
            return notFoundResponse;
        }

        _context.MealPlanRecipes.Remove(assignment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Removed recipe from meal plan {MealPlanId} for day {Day}", mealPlanId, dayDate);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            mealPlanId = mealPlanId,
            day = dayDate.ToString("yyyy-MM-dd"),
            message = "Recipe removed from meal plan"
        });

        return response;
    }
}

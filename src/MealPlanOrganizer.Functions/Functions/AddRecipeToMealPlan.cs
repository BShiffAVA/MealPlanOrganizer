using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Models;
using MealPlanOrganizer.Functions.Services;

namespace MealPlanOrganizer.Functions.Functions;

public class AddRecipeToMealPlan
{
    private readonly ILogger<AddRecipeToMealPlan> _logger;
    private readonly AppDbContext _context;
    private readonly AuthenticationHelper _authHelper;

    public AddRecipeToMealPlan(
        ILogger<AddRecipeToMealPlan> logger,
        AppDbContext context,
        AuthenticationHelper authHelper)
    {
        _logger = logger;
        _context = context;
        _authHelper = authHelper;
    }

    [Function("AddRecipeToMealPlan")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mealplans/{id:guid}/recipes")] HttpRequestData req,
        Guid id,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Adding recipe to meal plan {MealPlanId}", id);

        // Authenticate the request
        var authResult = await _authHelper.AuthenticateAsync(req);
        if (!authResult.IsAuthenticated)
        {
            _logger.LogWarning("Unauthorized add recipe to meal plan: {Error}", authResult.ErrorMessage);
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteAsJsonAsync(new { message = "Authentication required" });
            return unauthorizedResponse;
        }

        // Find the meal plan
        var mealPlan = await _context.MealPlans.FindAsync(id);
        if (mealPlan == null)
        {
            _logger.LogWarning("Meal plan {MealPlanId} not found", id);
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new { message = $"Meal plan with ID {id} not found" });
            return notFoundResponse;
        }

        // Parse request body
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        AddRecipeToMealPlanRequest? request = null;

        try
        {
            var requestBody = await req.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(requestBody))
            {
                request = JsonSerializer.Deserialize<AddRecipeToMealPlanRequest>(requestBody, options);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse request body");
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { message = "Invalid request body" });
            return badResponse;
        }

        if (request == null || request.RecipeId == Guid.Empty)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { message = "RecipeId is required" });
            return badResponse;
        }

        if (request.Day == default)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { message = "Day is required" });
            return badResponse;
        }

        // Validate day is within meal plan range
        var day = request.Day.Date;
        if (day < mealPlan.StartDate || day > mealPlan.EndDate)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new 
            { 
                message = $"Day must be between {mealPlan.StartDate:yyyy-MM-dd} and {mealPlan.EndDate:yyyy-MM-dd}" 
            });
            return badResponse;
        }

        // Verify recipe exists
        var recipeExists = await _context.Recipes.AnyAsync(r => r.Id == request.RecipeId);
        if (!recipeExists)
        {
            _logger.LogWarning("Recipe {RecipeId} not found", request.RecipeId);
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new { message = $"Recipe with ID {request.RecipeId} not found" });
            return notFoundResponse;
        }

        // Check if there's already a recipe assigned to this day (replace it)
        var existingAssignment = await _context.MealPlanRecipes
            .FirstOrDefaultAsync(mpr => mpr.MealPlanId == id && mpr.Day == day);

        if (existingAssignment != null)
        {
            // Update existing assignment
            existingAssignment.RecipeId = request.RecipeId;
            existingAssignment.CreatedUtc = DateTime.UtcNow;
            _logger.LogInformation("Updated recipe assignment for {Day} to {RecipeId}", day, request.RecipeId);
        }
        else
        {
            // Create new assignment
            var mealPlanRecipe = new MealPlanRecipe
            {
                Id = Guid.NewGuid(),
                MealPlanId = id,
                RecipeId = request.RecipeId,
                Day = day,
                CreatedUtc = DateTime.UtcNow
            };
            _context.MealPlanRecipes.Add(mealPlanRecipe);
            _logger.LogInformation("Added recipe {RecipeId} to meal plan {MealPlanId} for {Day}",
                request.RecipeId, id, day);
        }

        await _context.SaveChangesAsync();

        // Return the updated meal plan with recipes
        var updatedRecipes = await _context.MealPlanRecipes
            .Where(mpr => mpr.MealPlanId == id)
            .Include(mpr => mpr.Recipe)
            .OrderBy(mpr => mpr.Day)
            .ToListAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            mealPlanId = id,
            day = day.ToString("yyyy-MM-dd"),
            recipeId = request.RecipeId,
            message = existingAssignment != null ? "Recipe assignment updated" : "Recipe added to meal plan",
            totalRecipes = updatedRecipes.Count,
            recipes = updatedRecipes.Select(r => new
            {
                day = r.Day.ToString("yyyy-MM-dd"),
                recipeId = r.RecipeId,
                recipeTitle = r.Recipe?.Title
            })
        });

        return response;
    }
}

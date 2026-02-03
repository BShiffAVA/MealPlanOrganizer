using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;

namespace MealPlanOrganizer.Functions.Functions
{
    public class UpdateRecipe
    {
        private readonly ILogger _logger;
        private readonly AppDbContext _context;

        public UpdateRecipe(ILoggerFactory loggerFactory, AppDbContext context)
        {
            _logger = loggerFactory.CreateLogger<UpdateRecipe>();
            _context = context;
        }

        [Function("UpdateRecipe")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "recipes/{recipeId}")] HttpRequestData req,
            string recipeId)
        {
            _logger.LogInformation("Updating recipe: {RecipeId}", recipeId);

            try
            {
                // Validate recipeId
                if (string.IsNullOrWhiteSpace(recipeId) || !Guid.TryParse(recipeId, out var recipeGuid))
                {
                    _logger.LogWarning("Invalid recipeId format: {RecipeId}", recipeId);
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync(JsonSerializer.Serialize(new { error = "Invalid recipe ID format" }));
                    return badReq;
                }

                // Parse request body
                var requestBody = await req.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    _logger.LogWarning("Empty request body");
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync(JsonSerializer.Serialize(new { error = "Request body is required" }));
                    return badReq;
                }

                var updateRequest = JsonSerializer.Deserialize<UpdateRecipeRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (updateRequest == null)
                {
                    _logger.LogWarning("Invalid request body");
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync(JsonSerializer.Serialize(new { error = "Invalid request body" }));
                    return badReq;
                }

                // Find the recipe with related entities
                var recipe = await _context.Recipes
                    .Include(r => r.Ingredients)
                    .Include(r => r.Steps)
                    .FirstOrDefaultAsync(r => r.Id == recipeGuid);

                if (recipe == null)
                {
                    _logger.LogWarning("Recipe not found: {RecipeId}", recipeId);
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync(JsonSerializer.Serialize(new { error = "Recipe not found" }));
                    return notFound;
                }

                // Update basic properties
                recipe.Title = updateRequest.Title ?? recipe.Title;
                recipe.Description = updateRequest.Description;
                recipe.CuisineType = updateRequest.CuisineType;
                recipe.PrepTimeMinutes = updateRequest.PrepTimeMinutes;
                recipe.CookTimeMinutes = updateRequest.CookTimeMinutes;
                recipe.Servings = updateRequest.Servings;
                
                // Update image URL
                recipe.ImageUrl = string.IsNullOrWhiteSpace(updateRequest.ImageUrl)
                    ? null
                    : updateRequest.ImageUrl;

                // Record edit timestamp
                recipe.UpdatedUtc = DateTime.UtcNow;

                // Update ingredients - delete old ones and add new ones
                if (updateRequest.Ingredients != null)
                {
                    // Delete existing ingredients using ExecuteDelete
                    await _context.RecipeIngredients
                        .Where(i => i.RecipeId == recipe.Id)
                        .ExecuteDeleteAsync();

                    // Add new ingredients
                    foreach (var ingredient in updateRequest.Ingredients)
                    {
                        _context.RecipeIngredients.Add(new RecipeIngredient
                        {
                            RecipeId = recipe.Id,
                            Name = ingredient.Name,
                            Quantity = ingredient.Quantity
                        });
                    }
                }

                // Update steps - delete old ones and add new ones
                if (updateRequest.Steps != null)
                {
                    // Delete existing steps using ExecuteDelete
                    await _context.RecipeSteps
                        .Where(s => s.RecipeId == recipe.Id)
                        .ExecuteDeleteAsync();

                    // Add new steps
                    int stepNumber = 1;
                    foreach (var step in updateRequest.Steps)
                    {
                        _context.RecipeSteps.Add(new RecipeStep
                        {
                            RecipeId = recipe.Id,
                            StepNumber = stepNumber++,
                            Instruction = step
                        });
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully updated recipe: {RecipeId}", recipeId);

                // Return updated recipe
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    id = recipe.Id,
                    title = recipe.Title,
                    description = recipe.Description,
                    cuisineType = recipe.CuisineType,
                    prepTimeMinutes = recipe.PrepTimeMinutes,
                    cookTimeMinutes = recipe.CookTimeMinutes,
                    servings = recipe.Servings,
                    imageUrl = recipe.ImageUrl,
                    createdBy = recipe.CreatedBy,
                    createdUtc = recipe.CreatedUtc,
                    updatedUtc = recipe.UpdatedUtc
                }));

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating recipe: {RecipeId}", recipeId);
                var errResp = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResp.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "Failed to update recipe",
                    details = ex.Message
                }));
                return errResp;
            }
        }

        private class UpdateRecipeRequest
        {
            public string? Title { get; set; }
            public string? Description { get; set; }
            public string? CuisineType { get; set; }
            public int? PrepTimeMinutes { get; set; }
            public int? CookTimeMinutes { get; set; }
            public int? Servings { get; set; }
            public string? ImageUrl { get; set; }
            public List<IngredientInput>? Ingredients { get; set; }
            public List<string>? Steps { get; set; }
        }

        private class IngredientInput
        {
            public string Name { get; set; } = string.Empty;
            public string? Quantity { get; set; }
        }
    }
}

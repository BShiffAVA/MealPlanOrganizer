using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Functions
{
    public class CreateRecipe
    {
        private readonly ILogger _logger;
        private readonly AppDbContext _db;

        public CreateRecipe(ILoggerFactory loggerFactory, AppDbContext db)
        {
            _logger = loggerFactory.CreateLogger<CreateRecipe>();
            _db = db;
        }

        [Function("CreateRecipe")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "recipes")] HttpRequestData req)
        {
            _logger.LogInformation("Received CreateRecipe request");

            // Read request body (stub - validation/parsing only)
            var body = await req.ReadAsStringAsync();
            _logger.LogInformation("Request body: {Body}", body);
            try
            {
                var model = JsonSerializer.Deserialize<Models.RecipeCreateRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Minimal shape check (no persistence yet)
                if (model == null || string.IsNullOrWhiteSpace(model.Title))
                {
                    _logger.LogWarning("Validation failed: model null or title empty");
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Invalid request: 'title' is required.");
                    return bad;
                }

                _logger.LogInformation("Parsed recipe: Title={Title}, Ingredients={IngrCount}, Steps={StepCount}", 
                    model.Title, model.Ingredients?.Count ?? 0, model.Steps?.Count ?? 0);

                var recipe = new Recipe
                {
                    Title = model.Title!.Trim(),
                    Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description!.Trim(),
                    CuisineType = string.IsNullOrWhiteSpace(model.CuisineType) ? null : model.CuisineType!.Trim(),
                    PrepTimeMinutes = model.PrepTimeMinutes,
                    CookTimeMinutes = model.CookTimeMinutes,
                    Servings = model.Servings,
                    ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? null : model.ImageUrl!.Trim(),
                    CreatedBy = "system" // TODO: Replace with actual authenticated user
                };

                if (model.Ingredients != null && model.Ingredients.Any())
                {
                    foreach (var ing in model.Ingredients.Where(i => i != null && !string.IsNullOrWhiteSpace(i.Name)))
                    {
                        recipe.Ingredients.Add(new RecipeIngredient 
                        { 
                            Name = ing.Name!.Trim(),
                            Quantity = string.IsNullOrWhiteSpace(ing.Quantity) ? null : ing.Quantity!.Trim()
                        });
                    }
                    _logger.LogInformation("Added {Count} ingredients", recipe.Ingredients.Count);
                }

                if (model.Steps != null && model.Steps.Any())
                {
                    int stepNumber = 1;
                    foreach (var step in model.Steps.Where(s => !string.IsNullOrWhiteSpace(s)))
                    {
                        recipe.Steps.Add(new RecipeStep { StepNumber = stepNumber++, Instruction = step.Trim() });
                    }
                    _logger.LogInformation("Added {Count} steps", recipe.Steps.Count);
                }
                else
                {
                    _logger.LogWarning("No valid steps found in request");
                }

                _db.Recipes.Add(recipe);
                await _db.SaveChangesAsync();
                _logger.LogInformation("Recipe saved with ID: {RecipeId}", recipe.Id);

                var created = req.CreateResponse(HttpStatusCode.Created);
                created.Headers.Add("Location", $"/api/recipes/{recipe.Id}");
                created.Headers.Add("Content-Type", "application/json");
                await created.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    id = recipe.Id,
                    title = recipe.Title
                }));
                return created;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error");
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync($"Invalid JSON payload: {ex.Message}");
                return bad;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating recipe");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Internal error: {ex.Message}");
                return error;
            }
        }
    }
}

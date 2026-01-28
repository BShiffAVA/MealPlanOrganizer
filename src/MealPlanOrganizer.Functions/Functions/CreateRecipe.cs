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
            try
            {
                var model = JsonSerializer.Deserialize<Models.RecipeCreateRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Minimal shape check (no persistence yet)
                if (model == null || string.IsNullOrWhiteSpace(model.Title))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Invalid request: 'title' is required.");
                    return bad;
                }

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
                    foreach (var ing in model.Ingredients.Where(i => !string.IsNullOrWhiteSpace(i)))
                    {
                        recipe.Ingredients.Add(new RecipeIngredient { Name = ing.Trim() });
                    }
                }

                if (model.Steps != null && model.Steps.Any())
                {
                    int stepNumber = 1;
                    foreach (var step in model.Steps.Where(s => !string.IsNullOrWhiteSpace(s)))
                    {
                        recipe.Steps.Add(new RecipeStep { StepNumber = stepNumber++, Instruction = step.Trim() });
                    }
                }

                _db.Recipes.Add(recipe);
                await _db.SaveChangesAsync();

                var created = req.CreateResponse(HttpStatusCode.Created);
                created.Headers.Add("Location", $"/api/recipes/{recipe.Id}");
                await created.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    id = recipe.Id,
                    title = recipe.Title
                }));
                return created;
            }
            catch (JsonException)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid JSON payload.");
                return bad;
            }
        }
    }
}

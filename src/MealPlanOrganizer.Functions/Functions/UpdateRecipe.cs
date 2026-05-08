using System;
using System.Collections.Generic;
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
using MealPlanOrganizer.Functions.Services;

namespace MealPlanOrganizer.Functions.Functions
{
    public class UpdateRecipe
    {
        private readonly ILogger _logger;
        private readonly AppDbContext _context;
        private readonly AuthenticationHelper _authHelper;

        public UpdateRecipe(ILoggerFactory loggerFactory, AppDbContext context, AuthenticationHelper authHelper)
        {
            _logger = loggerFactory.CreateLogger<UpdateRecipe>();
            _context = context;
            _authHelper = authHelper;
        }

        [Function("UpdateRecipe")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "recipes/{recipeId}")] HttpRequestData req,
            string recipeId)
        {
            _logger.LogInformation("Updating recipe: {RecipeId}", recipeId);

            try
            {
                // Authenticate the request
                var authResult = await _authHelper.AuthenticateAsync(req);
                if (!authResult.IsAuthenticated)
                {
                    _logger.LogWarning("Authentication failed: {Error}", authResult.ErrorMessage);
                    var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauthorized.WriteStringAsync(JsonSerializer.Serialize(new { error = authResult.ErrorMessage ?? "Unauthorized" }));
                    return unauthorized;
                }
                var userId = authResult.UserId;
                _logger.LogInformation("Authenticated user: {UserId}", userId);

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
                    .Include(r => r.TagAssignments)
                    .FirstOrDefaultAsync(r => r.Id == recipeGuid);

                if (recipe == null)
                {
                    _logger.LogWarning("Recipe not found: {RecipeId}", recipeId);
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync(JsonSerializer.Serialize(new { error = "Recipe not found" }));
                    return notFound;
                }

                // Look up the User by their External ID to get the internal User.Id
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.ExternalIdObjectId == userId);
                
                if (user == null)
                {
                    _logger.LogWarning("User with ExternalIdObjectId {UserId} not found in database", userId);
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync(JsonSerializer.Serialize(new { error = "User not found" }));
                    return forbidden;
                }

                // Authorization: Only the creator can edit a recipe
                if (recipe.CreatedByUserId != user.Id)
                {
                    _logger.LogWarning("User {UserId} attempted to edit recipe {RecipeId} created by {CreatedByUserId}", user.Id, recipeId, recipe.CreatedByUserId);
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync(JsonSerializer.Serialize(new { error = "Only the recipe creator can edit this recipe" }));
                    return forbidden;
                }

                // Update basic properties (ratings are preserved - not touched during update)
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

                // Update tags - delete old assignments and re-add
                if (updateRequest.Tags != null)
                {
                    await _context.RecipeTagAssignments
                        .Where(ta => ta.RecipeId == recipe.Id)
                        .ExecuteDeleteAsync();

                    var normalizedTags = TagHelper.NormalizeAll(updateRequest.Tags);
                    foreach (var tagName in normalizedTags)
                    {
                        var tag = await _context.RecipeTags.FirstOrDefaultAsync(t => t.Name == tagName);
                        if (tag == null)
                        {
                            tag = new RecipeTag { Name = tagName };
                            _context.RecipeTags.Add(tag);
                            await _context.SaveChangesAsync();
                        }
                        _context.RecipeTagAssignments.Add(new RecipeTagAssignment
                        {
                            RecipeId = recipe.Id,
                            TagId = tag.Id
                        });
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully updated recipe: {RecipeId}", recipeId);

                // Reload tags for response
                var tags = await _context.RecipeTagAssignments
                    .Where(ta => ta.RecipeId == recipe.Id)
                    .Include(ta => ta.Tag)
                    .Select(ta => ta.Tag.Name)
                    .OrderBy(n => n)
                    .ToListAsync();

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
                    updatedUtc = recipe.UpdatedUtc,
                    tags
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
            public List<string>? Tags { get; set; }
        }

        private class IngredientInput
        {
            public string Name { get; set; } = string.Empty;
            public string? Quantity { get; set; }
        }
    }
}

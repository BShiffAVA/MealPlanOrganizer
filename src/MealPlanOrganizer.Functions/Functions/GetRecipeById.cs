using System;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Services;

namespace MealPlanOrganizer.Functions.Functions;

public class GetRecipeById
{
    private readonly ILogger<GetRecipeById> _logger;
    private readonly AppDbContext _context;
    private readonly IBlobUrlService _blobUrlService;

    public GetRecipeById(
        ILogger<GetRecipeById> logger,
        AppDbContext context,
        IBlobUrlService blobUrlService)
    {
        _logger = logger;
        _context = context;
        _blobUrlService = blobUrlService;
    }

    [Function("GetRecipeById")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/{id:guid}")] HttpRequestData req,
        Guid id)
    {
        _logger.LogInformation("Getting recipe with ID: {RecipeId}", id);

        var recipe = await _context.Recipes
            .Include(r => r.Ingredients.OrderBy(i => i.Id))
            .Include(r => r.Steps.OrderBy(s => s.StepNumber))
            .Include(r => r.Ratings)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (recipe == null)
        {
            _logger.LogWarning("Recipe with ID {RecipeId} not found", id);
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new { message = $"Recipe with ID {id} not found" });
            return notFoundResponse;
        }

        var imageUrl = await _blobUrlService.NormalizeImageUrlAsync(recipe.ImageUrl);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            id = recipe.Id,
            title = recipe.Title,
            description = recipe.Description,
            cuisineType = recipe.CuisineType,
            prepTimeMinutes = recipe.PrepTimeMinutes,
            cookTimeMinutes = recipe.CookTimeMinutes,
            servings = recipe.Servings,
            imageUrl = imageUrl,
            createdBy = recipe.CreatedBy,
            createdUtc = recipe.CreatedUtc,
            averageRating = recipe.Ratings.Count > 0 ? recipe.Ratings.Average(r => r.Rating) : 0.0,
            ratingCount = recipe.Ratings.Count,
            ratings = recipe.Ratings
                .OrderByDescending(r => r.RatedUtc)
                .Select(r => new
                {
                    userId = r.UserId,
                    rating = r.Rating,
                    comments = r.Comments,
                    ratedUtc = r.RatedUtc
                })
                .ToList(),
            ingredients = recipe.Ingredients.Select(i => new
            {
                name = i.Name,
                quantity = i.Quantity
            }).ToList(),
            steps = recipe.Steps.Select(s => new
            {
                stepNumber = s.StepNumber,
                instruction = s.Instruction
            }).ToList()
        });

        return response;
    }
}

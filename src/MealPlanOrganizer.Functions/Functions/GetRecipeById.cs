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
    private readonly AuthenticationHelper _authHelper;

    public GetRecipeById(
        ILogger<GetRecipeById> logger,
        AppDbContext context,
        IBlobUrlService blobUrlService,
        AuthenticationHelper authHelper)
    {
        _logger = logger;
        _context = context;
        _blobUrlService = blobUrlService;
        _authHelper = authHelper;
    }

    [Function("GetRecipeById")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/{id:guid}")] HttpRequestData req,
        Guid id)
    {
        _logger.LogInformation("Getting recipe with ID: {RecipeId}", id);

        // Try to authenticate (optional for this endpoint)
        Guid? currentUserId = null;
        try
        {
            var authResult = await _authHelper.AuthenticateAsync(req);
            if (authResult.IsAuthenticated && !string.IsNullOrEmpty(authResult.UserId))
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.ExternalIdObjectId == authResult.UserId);
                currentUserId = user?.Id;
            }
        }
        catch
        {
            // Authentication is optional - ignore errors
        }

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
        
        // Determine if current user is the creator
        var isCurrentUserCreator = currentUserId.HasValue && recipe.CreatedByUserId == currentUserId.Value;
        
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
            createdByUserId = recipe.CreatedByUserId,
            isCurrentUserCreator = isCurrentUserCreator,
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

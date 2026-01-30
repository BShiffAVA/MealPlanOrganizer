using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile.Services;

public class RecipeService : IRecipeService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RecipeService> _logger;
    private readonly string _baseUrl;
    private readonly string _functionKey;

    public RecipeService(HttpClient httpClient, ILogger<RecipeService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["AzureFunctions:BaseUrl"] ?? throw new InvalidOperationException("AzureFunctions:BaseUrl not configured");
        _functionKey = configuration["AzureFunctions:FunctionKey"] ?? throw new InvalidOperationException("AzureFunctions:FunctionKey not configured");
    }

    public async Task<List<RecipeDto>> GetRecipesAsync()
    {
        try
        {
            _logger.LogInformation("Fetching recipes from Azure Functions");

            var url = $"{_baseUrl}/recipes?code={_functionKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch recipes: {StatusCode}", response.StatusCode);
                return new List<RecipeDto>();
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var recipes = JsonSerializer.Deserialize<List<RecipeDto>>(jsonContent, options) ?? new List<RecipeDto>();

            _logger.LogInformation("Successfully fetched {Count} recipes", recipes.Count);
            return recipes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception fetching recipes");
            return new List<RecipeDto>();
        }
    }

    public async Task<RecipeDetailDto?> GetRecipeByIdAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Fetching recipe details for ID: {RecipeId}", id);

            var url = $"{_baseUrl}/recipes/{id}?code={_functionKey}";
            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Recipe {RecipeId} not found", id);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch recipe details: {StatusCode}", response.StatusCode);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var recipe = JsonSerializer.Deserialize<RecipeDetailDto>(jsonContent, options);

            _logger.LogInformation("Successfully fetched recipe: {Title}", recipe?.Title);
            return recipe;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception fetching recipe details for ID: {RecipeId}", id);
            return null;
        }
    }

    public async Task<Guid?> CreateRecipeAsync(CreateRecipeDto recipe)
    {
        try
        {
            _logger.LogInformation("Creating new recipe: {Title}", recipe.Title);

            var url = $"{_baseUrl}/recipes?code={_functionKey}";
            var json = JsonSerializer.Serialize(recipe);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to create recipe: {StatusCode}", response.StatusCode);
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error details: {ErrorContent}", errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            
            // Parse response to get recipe ID
            using var doc = System.Text.Json.JsonDocument.Parse(responseContent);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("id", out var idElement) && Guid.TryParse(idElement.GetString(), out var recipeId))
            {
                _logger.LogInformation("Successfully created recipe with ID: {RecipeId}", recipeId);
                return recipeId;
            }

            _logger.LogWarning("Recipe created but no ID returned in response");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception creating recipe: {Title}", recipe.Title);
            return null;
        }
    }
}

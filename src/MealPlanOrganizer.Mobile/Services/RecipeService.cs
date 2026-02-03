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

    public async Task<bool> UpdateRecipeAsync(Guid recipeId, UpdateRecipeDto recipe)
    {
        try
        {
            _logger.LogInformation("Updating recipe: {RecipeId}", recipeId);

            var url = $"{_baseUrl}/recipes/{recipeId}?code={_functionKey}";
            var json = JsonSerializer.Serialize(recipe);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to update recipe: {StatusCode}", response.StatusCode);
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error details: {ErrorContent}", errorContent);
                return false;
            }

            _logger.LogInformation("Successfully updated recipe: {RecipeId}", recipeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception updating recipe: {RecipeId}", recipeId);
            return false;
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

    public async Task<string?> UploadRecipeImageAsync(FileResult photo, Guid recipeId)
    {
        try
        {
            _logger.LogInformation("Starting image upload for recipe: {RecipeId}", recipeId);

            if (photo == null)
            {
                _logger.LogWarning("Photo is null");
                return null;
            }

            var url = $"{_baseUrl}/recipes/{recipeId}/upload-image?code={_functionKey}";
            _logger.LogInformation("Upload URL: {Url}", url);

            // Read photo file
            _logger.LogInformation("Opening photo file: {FileName}", photo.FileName);
            using var stream = await photo.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            _logger.LogInformation("Photo loaded, size: {Size} bytes, ContentType: {ContentType}", fileBytes.Length, photo.ContentType);

            // Create multipart form data
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            content.Add(fileContent, "file", photo.FileName);

            _logger.LogInformation("Sending multipart POST request to {Url}", url);
            var response = await _httpClient.PostAsync(url, content);

            _logger.LogInformation("Upload response status: {StatusCode}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to upload image: {StatusCode}", response.StatusCode);
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error details: {ErrorContent}", errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Upload response content: {Response}", responseContent);

            // Parse response to get image URL
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                if (root.TryGetProperty("imageUrl", out var urlElement))
                {
                    var imageUrl = urlElement.GetString();
                    _logger.LogInformation("Successfully uploaded image, URL: {ImageUrl}", imageUrl);
                    return imageUrl;
                }

                _logger.LogWarning("Image uploaded but no imageUrl in response. Response: {Response}", responseContent);
                return null;
            }
            catch (Exception parseEx)
            {
                _logger.LogError(parseEx, "Failed to parse upload response: {Response}", responseContent);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception uploading recipe image for recipe: {RecipeId}. Exception: {ExceptionMessage}", recipeId, ex.Message);
            return null;
        }
    }
}

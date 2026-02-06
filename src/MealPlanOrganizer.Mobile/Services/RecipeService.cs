using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile.Services;

public class RecipeService : IRecipeService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;
    private readonly ILogger<RecipeService> _logger;
    private readonly string _baseUrl;
    private readonly string _functionKey;

    public RecipeService(HttpClient httpClient, IAuthService authService, ILogger<RecipeService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _authService = authService;
        _logger = logger;
        _baseUrl = configuration["AzureFunctions:BaseUrl"] ?? throw new InvalidOperationException("AzureFunctions:BaseUrl not configured");
        _functionKey = configuration["AzureFunctions:FunctionKey"] ?? throw new InvalidOperationException("AzureFunctions:FunctionKey not configured");
    }

    /// <summary>
    /// Attaches the Bearer token to the HTTP client for authenticated requests.
    /// </summary>
    private async Task AttachBearerTokenAsync()
    {
        try
        {
            var accessToken = await _authService.GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(accessToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                _logger.LogDebug("Bearer token attached to request");
            }
            else
            {
                _logger.LogWarning("No access token available - request will be unauthenticated");
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to attach Bearer token");
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    public async Task<List<RecipeDto>> GetRecipesAsync()
    {
        try
        {
            _logger.LogInformation("Fetching recipes from Azure Functions");

            await AttachBearerTokenAsync();

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

            await AttachBearerTokenAsync();

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

            await AttachBearerTokenAsync();

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

            await AttachBearerTokenAsync();

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

            await AttachBearerTokenAsync();

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

    public async Task<Models.RecipeExtractionResponse?> ExtractRecipeAsync(Models.RecipeExtractionRequest request)
    {
        try
        {
            _logger.LogInformation("Extracting recipe via GenAI. InputType: {InputType}", request.InputType);

            await AttachBearerTokenAsync();

            var url = $"{_baseUrl}/recipes/extract?code={_functionKey}";

            // Build API request body matching backend RecipeExtractionRequest model
            var apiRequest = new
            {
                inputType = request.InputType,
                image = request.InputType == "image" ? request.ImageBase64 : null,
                url = request.InputType == "url" ? request.Url : null,
                text = request.InputType == "text" ? request.Text : null
            };

            var json = JsonSerializer.Serialize(apiRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Extraction response status: {StatusCode}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Extraction failed: {StatusCode}. Response: {Response}", response.StatusCode, responseContent);
                return new Models.RecipeExtractionResponse
                {
                    Success = false,
                    ErrorMessage = $"Extraction failed with status {response.StatusCode}"
                };
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var extractionResponse = JsonSerializer.Deserialize<Models.RecipeExtractionResponse>(responseContent, options);

            if (extractionResponse == null)
            {
                _logger.LogError("Failed to parse extraction response: {Response}", responseContent);
                return new Models.RecipeExtractionResponse
                {
                    Success = false,
                    ErrorMessage = "Failed to parse extraction response"
                };
            }

            _logger.LogInformation("Recipe extracted successfully. Confidence: {Confidence:P0}", extractionResponse.Confidence);
            return extractionResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during recipe extraction");
            return new Models.RecipeExtractionResponse
            {
                Success = false,
                ErrorMessage = $"An error occurred: {ex.Message}"
            };
        }
    }

    public async Task<RateRecipeResult> RateRecipeAsync(Guid recipeId, int rating, string? comments, string? frequencyPreference)
    {
        try
        {
            _logger.LogInformation("Submitting rating for recipe: {RecipeId}, Rating: {Rating}", recipeId, rating);

            await AttachBearerTokenAsync();

            var url = $"{_baseUrl}/recipes/{recipeId}/ratings?code={_functionKey}";
            
            var request = new RateRecipeRequestDto
            {
                Rating = rating,
                Comments = comments,
                FrequencyPreference = frequencyPreference
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Rating response status: {StatusCode}", response.StatusCode);

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _logger.LogInformation("User already rated this recipe today");
                return new RateRecipeResult
                {
                    Success = false,
                    AlreadyRatedToday = true,
                    ErrorMessage = "You have already rated this recipe today. You can add another rating tomorrow."
                };
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Unauthorized rating attempt");
                return new RateRecipeResult
                {
                    Success = false,
                    ErrorMessage = "Please log in to rate recipes."
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to submit rating: {StatusCode}. Response: {Response}", response.StatusCode, responseContent);
                return new RateRecipeResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to submit rating: {response.StatusCode}"
                };
            }

            // Parse successful response
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            Guid? ratingId = null;
            double? avgRating = null;
            int? totalRatings = null;

            if (root.TryGetProperty("ratingId", out var ratingIdElement) && ratingIdElement.ValueKind == JsonValueKind.String)
            {
                Guid.TryParse(ratingIdElement.GetString(), out var parsedId);
                ratingId = parsedId;
            }
            if (root.TryGetProperty("averageRating", out var avgElement))
            {
                avgRating = avgElement.GetDouble();
            }
            if (root.TryGetProperty("totalRatings", out var totalElement))
            {
                totalRatings = totalElement.GetInt32();
            }

            _logger.LogInformation("Successfully submitted rating. RatingId: {RatingId}, AvgRating: {AvgRating}", ratingId, avgRating);
            
            return new RateRecipeResult
            {
                Success = true,
                RatingId = ratingId,
                AverageRating = avgRating,
                TotalRatings = totalRatings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception submitting rating for recipe: {RecipeId}", recipeId);
            return new RateRecipeResult
            {
                Success = false,
                ErrorMessage = $"An error occurred: {ex.Message}"
            };
        }
    }
}

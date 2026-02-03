using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using MealPlanOrganizer.Functions.Models;
using MealPlanOrganizer.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Functions
{
    /// <summary>
    /// Azure Function for GenAI-powered recipe extraction.
    /// Supports extracting recipes from images, URLs, and plain text.
    /// </summary>
    public class ExtractRecipe
    {
        private readonly ILogger<ExtractRecipe> _logger;
        private readonly IRecipeExtractionService _extractionService;

        // Maximum image size: 10 MB
        private const int MaxImageSizeBytes = 10 * 1024 * 1024;
        
        // Maximum text length: 50,000 characters
        private const int MaxTextLength = 50_000;

        public ExtractRecipe(
            ILogger<ExtractRecipe> logger,
            IRecipeExtractionService extractionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _extractionService = extractionService ?? throw new ArgumentNullException(nameof(extractionService));
        }

        /// <summary>
        /// POST /recipes/extract
        /// Extract structured recipe data from an image, URL, or text.
        /// </summary>
        [Function("ExtractRecipe")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "recipes/extract")] HttpRequestData req)
        {
            _logger.LogInformation("Recipe extraction request received");

            try
            {
                // Read and parse request body
                var body = await req.ReadAsStringAsync();
                
                if (string.IsNullOrWhiteSpace(body))
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, 
                        "Request body is required.");
                }

                var request = JsonSerializer.Deserialize<RecipeExtractionRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || string.IsNullOrWhiteSpace(request.InputType))
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, 
                        "Invalid request: 'inputType' is required (image, url, or text).");
                }

                // Process based on input type
                RecipeExtractionResponse extractionResponse;
                
                switch (request.InputType.ToLowerInvariant())
                {
                    case "image":
                        extractionResponse = await ProcessImageInput(request);
                        break;
                    
                    case "url":
                        extractionResponse = await ProcessUrlInput(request);
                        break;
                    
                    case "text":
                        extractionResponse = await ProcessTextInput(request);
                        break;
                    
                    default:
                        return await CreateErrorResponse(req, HttpStatusCode.BadRequest, 
                            $"Invalid inputType '{request.InputType}'. Must be 'image', 'url', or 'text'.");
                }

                // Return response
                var statusCode = extractionResponse.Success ? HttpStatusCode.OK : HttpStatusCode.UnprocessableEntity;
                var response = req.CreateResponse(statusCode);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(extractionResponse, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                }));

                return response;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid JSON in request body");
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, 
                    "Invalid JSON in request body.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during recipe extraction");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, 
                    "An unexpected error occurred. Please try again later.");
            }
        }

        private async Task<RecipeExtractionResponse> ProcessImageInput(RecipeExtractionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Image))
            {
                return new RecipeExtractionResponse
                {
                    Success = false,
                    Confidence = 0,
                    ErrorMessage = "Image data is required when inputType is 'image'."
                };
            }

            // Validate image size (base64 is ~33% larger than binary)
            var estimatedBinarySize = request.Image.Length * 3 / 4;
            if (estimatedBinarySize > MaxImageSizeBytes)
            {
                return new RecipeExtractionResponse
                {
                    Success = false,
                    Confidence = 0,
                    ErrorMessage = $"Image too large. Maximum size is {MaxImageSizeBytes / (1024 * 1024)} MB."
                };
            }

            _logger.LogInformation("Processing image extraction ({Size} bytes estimated)", estimatedBinarySize);
            return await _extractionService.ExtractFromBase64ImageAsync(request.Image);
        }

        private async Task<RecipeExtractionResponse> ProcessUrlInput(RecipeExtractionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                return new RecipeExtractionResponse
                {
                    Success = false,
                    Confidence = 0,
                    ErrorMessage = "URL is required when inputType is 'url'."
                };
            }

            // Validate URL format
            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                return new RecipeExtractionResponse
                {
                    Success = false,
                    Confidence = 0,
                    ErrorMessage = "Invalid URL format. Please provide a valid HTTP or HTTPS URL."
                };
            }

            _logger.LogInformation("Processing URL extraction: {Url}", request.Url);
            return await _extractionService.ExtractFromUrlAsync(request.Url);
        }

        private async Task<RecipeExtractionResponse> ProcessTextInput(RecipeExtractionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return new RecipeExtractionResponse
                {
                    Success = false,
                    Confidence = 0,
                    ErrorMessage = "Text content is required when inputType is 'text'."
                };
            }

            // Validate text length
            if (request.Text.Length > MaxTextLength)
            {
                return new RecipeExtractionResponse
                {
                    Success = false,
                    Confidence = 0,
                    ErrorMessage = $"Text too long. Maximum length is {MaxTextLength} characters."
                };
            }

            _logger.LogInformation("Processing text extraction ({Length} chars)", request.Text.Length);
            return await _extractionService.ExtractFromTextAsync(request.Text);
        }

        private static async Task<HttpResponseData> CreateErrorResponse(
            HttpRequestData req, 
            HttpStatusCode statusCode, 
            string message)
        {
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json");
            
            var errorResponse = new RecipeExtractionResponse
            {
                Success = false,
                Confidence = 0,
                ErrorMessage = message
            };
            
            await response.WriteStringAsync(JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
            
            return response;
        }
    }
}

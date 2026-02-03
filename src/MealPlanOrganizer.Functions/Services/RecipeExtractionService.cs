using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using MealPlanOrganizer.Functions.Models;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace MealPlanOrganizer.Functions.Services
{
    /// <summary>
    /// Implementation of IRecipeExtractionService using Azure OpenAI GPT-4o with Vision.
    /// </summary>
    public class RecipeExtractionService : IRecipeExtractionService
    {
        private readonly AzureOpenAIClient _openAIClient;
        private readonly string _deploymentName;
        private readonly ILogger<RecipeExtractionService> _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly HttpClient _httpClient;

        private const string SystemPrompt = @"You are a recipe extraction assistant. Extract structured recipe data from the provided content.

Return a JSON object with the following structure:
{
  ""name"": ""Recipe name"",
  ""description"": ""Brief description"",
  ""cuisineType"": ""Cuisine category (e.g., Italian, Mexican, Dessert)"",
  ""prepMinutes"": number or null,
  ""cookMinutes"": number or null,
  ""servings"": number or null,
  ""confidence"": number between 0.0 and 1.0 indicating extraction quality,
  ""ingredients"": [
    { ""name"": ""ingredient name"", ""quantity"": number or null, ""unit"": ""unit or null"" }
  ],
  ""steps"": [
    { ""stepNumber"": 1, ""instruction"": ""Step instruction"" }
  ]
}

Guidelines:
- Extract all ingredients with quantities and units when available
- Number steps sequentially starting from 1
- Use common unit abbreviations (cups, tbsp, tsp, oz, lb, g, ml)
- If information is unclear, use null
- Set confidence based on how complete and clear the extraction is (1.0 = perfect, 0.5 = partial, 0.0 = failed)
- Return valid JSON only, no markdown formatting";

        public RecipeExtractionService(
            AzureOpenAIClient openAIClient,
            IConfiguration configuration,
            ILogger<RecipeExtractionService> logger,
            TelemetryClient telemetryClient,
            IHttpClientFactory httpClientFactory)
        {
            _openAIClient = openAIClient ?? throw new ArgumentNullException(nameof(openAIClient));
            _deploymentName = configuration["OpenAI__DeploymentName"] ?? configuration["OpenAI:DeploymentName"] ?? "gpt-4o";
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            _httpClient = httpClientFactory?.CreateClient() ?? new HttpClient();
        }

        public async Task<RecipeExtractionResponse> ExtractFromImageAsync(
            Stream imageStream, 
            string contentType, 
            CancellationToken ct = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("Starting recipe extraction from image stream");
                TrackExtractionRequested("image");

                // Convert stream to base64
                using var memoryStream = new MemoryStream();
                await imageStream.CopyToAsync(memoryStream, ct);
                var base64Image = Convert.ToBase64String(memoryStream.ToArray());

                return await ExtractFromBase64ImageInternalAsync(base64Image, contentType, stopwatch, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting recipe from image stream");
                TrackExtractionFailed("image", ex.GetType().Name);
                
                return new RecipeExtractionResponse
                {
                    Success = false,
                    Confidence = 0,
                    ErrorMessage = $"Image extraction failed: {ex.Message}"
                };
            }
        }

        public async Task<RecipeExtractionResponse> ExtractFromBase64ImageAsync(
            string base64Image, 
            CancellationToken ct = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("Starting recipe extraction from base64 image");
                TrackExtractionRequested("image");

                // Detect content type from base64 data or default to jpeg
                var contentType = DetectImageContentType(base64Image);
                
                // Remove data URI prefix if present
                var cleanBase64 = RemoveDataUriPrefix(base64Image);

                return await ExtractFromBase64ImageInternalAsync(cleanBase64, contentType, stopwatch, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting recipe from base64 image");
                TrackExtractionFailed("image", ex.GetType().Name);
                
                return new RecipeExtractionResponse
                {
                    Success = false,
                    Confidence = 0,
                    ErrorMessage = $"Image extraction failed: {ex.Message}"
                };
            }
        }

        public async Task<RecipeExtractionResponse> ExtractFromUrlAsync(
            string url, 
            CancellationToken ct = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("Starting recipe extraction from URL: {Url}", url);
                TrackExtractionRequested("url");

                // Validate URL
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || 
                    (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    return new RecipeExtractionResponse
                    {
                        Success = false,
                        Confidence = 0,
                        ErrorMessage = "Invalid URL. Please provide a valid HTTP or HTTPS URL."
                    };
                }

                // Fetch page content
                var pageContent = await FetchUrlContentAsync(url, ct);
                if (string.IsNullOrWhiteSpace(pageContent))
                {
                    return new RecipeExtractionResponse
                    {
                        Success = false,
                        Confidence = 0,
                        ErrorMessage = "Could not fetch content from the provided URL."
                    };
                }

                // Use OpenAI to extract recipe from HTML/text content
                return await ExtractFromContentAsync(pageContent, "url", stopwatch, ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Failed to fetch URL: {Url}", url);
                TrackExtractionFailed("url", "HttpRequestException");
                
                return new RecipeExtractionResponse
                {
                    Success = false,
                    Confidence = 0,
                    ErrorMessage = $"Could not access the URL: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting recipe from URL: {Url}", url);
                TrackExtractionFailed("url", ex.GetType().Name);
                
                return new RecipeExtractionResponse
                {
                    Success = false,
                    Confidence = 0,
                    ErrorMessage = $"URL extraction failed: {ex.Message}"
                };
            }
        }

        public async Task<RecipeExtractionResponse> ExtractFromTextAsync(
            string text, 
            CancellationToken ct = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("Starting recipe extraction from text ({Length} chars)", text?.Length ?? 0);
                TrackExtractionRequested("text");

                if (string.IsNullOrWhiteSpace(text))
                {
                    return new RecipeExtractionResponse
                    {
                        Success = false,
                        Confidence = 0,
                        ErrorMessage = "No text content provided."
                    };
                }

                return await ExtractFromContentAsync(text, "text", stopwatch, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting recipe from text");
                TrackExtractionFailed("text", ex.GetType().Name);
                
                return new RecipeExtractionResponse
                {
                    Success = false,
                    Confidence = 0,
                    ErrorMessage = $"Text extraction failed: {ex.Message}"
                };
            }
        }

        private async Task<RecipeExtractionResponse> ExtractFromBase64ImageInternalAsync(
            string base64Image,
            string contentType,
            Stopwatch stopwatch,
            CancellationToken ct)
        {
            var chatClient = _openAIClient.GetChatClient(_deploymentName);

            // Decode base64 to bytes for direct image passing
            byte[] imageBytes;
            try
            {
                imageBytes = Convert.FromBase64String(base64Image);
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex, "Invalid base64 image data");
                return new RecipeExtractionResponse
                {
                    Success = false,
                    Confidence = 0,
                    ErrorMessage = "Invalid base64 image encoding."
                };
            }

            // Create BinaryData from bytes for Azure OpenAI
            var imageBinaryData = BinaryData.FromBytes(imageBytes);
            
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(
                    ChatMessageContentPart.CreateTextPart("Extract the recipe from this image:"),
                    ChatMessageContentPart.CreateImagePart(imageBinaryData, contentType)
                )
            };

            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 2000,
                Temperature = 0.1f
            };

            var response = await chatClient.CompleteChatAsync(messages, options, ct);
            stopwatch.Stop();

            return ProcessOpenAIResponse(response.Value, "image", stopwatch.ElapsedMilliseconds);
        }

        private async Task<RecipeExtractionResponse> ExtractFromContentAsync(
            string content,
            string inputType,
            Stopwatch stopwatch,
            CancellationToken ct)
        {
            var chatClient = _openAIClient.GetChatClient(_deploymentName);

            // Truncate content if too long (to manage token usage)
            var truncatedContent = content.Length > 15000 
                ? content.Substring(0, 15000) + "\n\n[Content truncated...]" 
                : content;

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage($"Extract the recipe from the following content:\n\n{truncatedContent}")
            };

            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 2000,
                Temperature = 0.1f
            };

            var response = await chatClient.CompleteChatAsync(messages, options, ct);
            stopwatch.Stop();

            return ProcessOpenAIResponse(response.Value, inputType, stopwatch.ElapsedMilliseconds);
        }

        private RecipeExtractionResponse ProcessOpenAIResponse(
            ChatCompletion completion,
            string inputType,
            long durationMs)
        {
            var responseText = completion.Content[0].Text;
            
            _logger.LogInformation("OpenAI response received in {DurationMs}ms", durationMs);

            // Parse JSON response
            try
            {
                // Clean up response (remove markdown code blocks if present)
                var jsonText = CleanJsonResponse(responseText);
                
                var extractedData = JsonSerializer.Deserialize<ExtractedRecipeJson>(jsonText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (extractedData == null || string.IsNullOrWhiteSpace(extractedData.Name))
                {
                    _logger.LogWarning("Extraction returned empty or invalid data");
                    TrackExtractionFailed(inputType, "EmptyResult");
                    
                    return new RecipeExtractionResponse
                    {
                        Success = false,
                        Confidence = 0,
                        ErrorMessage = "Could not extract recipe data from the content."
                    };
                }

                var extractedRecipe = new ExtractedRecipe
                {
                    Name = extractedData.Name,
                    Description = extractedData.Description,
                    CuisineType = extractedData.CuisineType,
                    PrepMinutes = extractedData.PrepMinutes,
                    CookMinutes = extractedData.CookMinutes,
                    Servings = extractedData.Servings,
                    Ingredients = extractedData.Ingredients ?? new List<ExtractedIngredient>(),
                    Steps = extractedData.Steps ?? new List<ExtractedStep>()
                };

                var confidence = extractedData.Confidence ?? CalculateConfidence(extractedRecipe);

                var tokenUsage = completion.Usage?.TotalTokenCount ?? 0;
                TrackExtractionCompleted(inputType, confidence, tokenUsage, durationMs);

                _logger.LogInformation(
                    "Recipe extraction successful: {RecipeName}, confidence: {Confidence}, ingredients: {IngredientCount}, steps: {StepCount}",
                    extractedRecipe.Name,
                    confidence,
                    extractedRecipe.Ingredients.Count,
                    extractedRecipe.Steps.Count);

                return new RecipeExtractionResponse
                {
                    Success = true,
                    Confidence = confidence,
                    ExtractedRecipe = extractedRecipe
                };
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse OpenAI response as JSON: {Response}", responseText);
                TrackExtractionFailed(inputType, "JsonParseError");
                
                return new RecipeExtractionResponse
                {
                    Success = false,
                    Confidence = 0,
                    ErrorMessage = "Failed to parse the extracted recipe data."
                };
            }
        }

        private async Task<string> FetchUrlContentAsync(string url, CancellationToken ct)
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MealPlanOrganizer/1.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(ct);
            
            // Strip HTML tags for cleaner processing (basic approach)
            return StripHtmlTags(content);
        }

        private static string StripHtmlTags(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;
            
            // Remove script and style elements
            html = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
            
            // Remove HTML tags
            html = Regex.Replace(html, @"<[^>]+>", " ");
            
            // Decode HTML entities
            html = System.Net.WebUtility.HtmlDecode(html);
            
            // Normalize whitespace
            html = Regex.Replace(html, @"\s+", " ").Trim();
            
            return html;
        }

        private static string CleanJsonResponse(string response)
        {
            if (string.IsNullOrEmpty(response)) return response;
            
            // Remove markdown code block markers
            response = Regex.Replace(response, @"^```json\s*", "", RegexOptions.IgnoreCase);
            response = Regex.Replace(response, @"^```\s*", "");
            response = Regex.Replace(response, @"\s*```$", "");
            
            return response.Trim();
        }

        private static string DetectImageContentType(string base64Data)
        {
            // Check for data URI prefix
            if (base64Data.StartsWith("data:image/jpeg", StringComparison.OrdinalIgnoreCase))
                return "image/jpeg";
            if (base64Data.StartsWith("data:image/png", StringComparison.OrdinalIgnoreCase))
                return "image/png";
            if (base64Data.StartsWith("data:image/gif", StringComparison.OrdinalIgnoreCase))
                return "image/gif";
            if (base64Data.StartsWith("data:image/webp", StringComparison.OrdinalIgnoreCase))
                return "image/webp";
            
            // Default to JPEG
            return "image/jpeg";
        }

        private static string RemoveDataUriPrefix(string base64Data)
        {
            var match = Regex.Match(base64Data, @"^data:image/[^;]+;base64,(.+)$", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : base64Data;
        }

        private static decimal CalculateConfidence(ExtractedRecipe recipe)
        {
            var score = 0m;
            var maxScore = 6m;

            if (!string.IsNullOrWhiteSpace(recipe.Name)) score += 1;
            if (!string.IsNullOrWhiteSpace(recipe.Description)) score += 0.5m;
            if (recipe.Ingredients.Count > 0) score += 1.5m;
            if (recipe.Steps.Count > 0) score += 1.5m;
            if (recipe.PrepMinutes.HasValue || recipe.CookMinutes.HasValue) score += 0.5m;
            if (recipe.Servings.HasValue) score += 0.5m;
            if (!string.IsNullOrWhiteSpace(recipe.CuisineType)) score += 0.5m;

            return Math.Round(score / maxScore, 2);
        }

        private void TrackExtractionRequested(string inputType)
        {
            _telemetryClient.TrackEvent("RecipeExtractionRequested", new Dictionary<string, string>
            {
                { "inputType", inputType }
            });
        }

        private void TrackExtractionCompleted(string inputType, decimal confidence, int tokenCount, long durationMs)
        {
            _telemetryClient.TrackEvent("RecipeExtractionCompleted", 
                new Dictionary<string, string>
                {
                    { "inputType", inputType },
                    { "confidence", confidence.ToString("F2") }
                },
                new Dictionary<string, double>
                {
                    { "durationMs", durationMs },
                    { "tokenCount", tokenCount }
                });
        }

        private void TrackExtractionFailed(string inputType, string errorType)
        {
            _telemetryClient.TrackEvent("RecipeExtractionFailed", new Dictionary<string, string>
            {
                { "inputType", inputType },
                { "errorType", errorType }
            });
        }

        /// <summary>
        /// Internal DTO for parsing OpenAI JSON response
        /// </summary>
        private class ExtractedRecipeJson
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
            public string? CuisineType { get; set; }
            public int? PrepMinutes { get; set; }
            public int? CookMinutes { get; set; }
            public int? Servings { get; set; }
            public decimal? Confidence { get; set; }
            public List<ExtractedIngredient>? Ingredients { get; set; }
            public List<ExtractedStep>? Steps { get; set; }
        }
    }
}

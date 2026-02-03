using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MealPlanOrganizer.Functions.Models;

namespace MealPlanOrganizer.Functions.Services
{
    /// <summary>
    /// Service interface for GenAI-powered recipe extraction.
    /// Supports extracting structured recipe data from images, URLs, and plain text.
    /// </summary>
    public interface IRecipeExtractionService
    {
        /// <summary>
        /// Extract recipe data from an image (cookbook photo, screenshot, handwritten recipe)
        /// </summary>
        /// <param name="imageStream">Stream containing the image data</param>
        /// <param name="contentType">MIME type of the image (e.g., "image/jpeg", "image/png")</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Extraction response with structured recipe data and confidence score</returns>
        Task<RecipeExtractionResponse> ExtractFromImageAsync(
            Stream imageStream, 
            string contentType, 
            CancellationToken ct = default);

        /// <summary>
        /// Extract recipe data from a base64-encoded image
        /// </summary>
        /// <param name="base64Image">Base64-encoded image data</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Extraction response with structured recipe data and confidence score</returns>
        Task<RecipeExtractionResponse> ExtractFromBase64ImageAsync(
            string base64Image, 
            CancellationToken ct = default);

        /// <summary>
        /// Extract recipe data from a recipe website URL
        /// </summary>
        /// <param name="url">URL to a recipe page</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Extraction response with structured recipe data and confidence score</returns>
        Task<RecipeExtractionResponse> ExtractFromUrlAsync(
            string url, 
            CancellationToken ct = default);

        /// <summary>
        /// Extract recipe data from plain text content
        /// </summary>
        /// <param name="text">Recipe text content (copy/pasted from a source)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Extraction response with structured recipe data and confidence score</returns>
        Task<RecipeExtractionResponse> ExtractFromTextAsync(
            string text, 
            CancellationToken ct = default);
    }
}

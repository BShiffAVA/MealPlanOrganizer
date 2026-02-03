namespace MealPlanOrganizer.Functions.Models
{
    /// <summary>
    /// Request model for GenAI recipe extraction.
    /// Supports image (base64), URL, or plain text input.
    /// </summary>
    public class RecipeExtractionRequest
    {
        /// <summary>
        /// Type of input: "image", "url", or "text"
        /// </summary>
        public string InputType { get; set; } = string.Empty;

        /// <summary>
        /// Base64-encoded image data (when InputType = "image")
        /// </summary>
        public string? Image { get; set; }

        /// <summary>
        /// URL to a recipe page (when InputType = "url")
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Plain text containing recipe content (when InputType = "text")
        /// </summary>
        public string? Text { get; set; }
    }
}

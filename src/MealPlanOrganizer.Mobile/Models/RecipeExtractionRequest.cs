namespace MealPlanOrganizer.Mobile.Models;

/// <summary>
/// Request model for GenAI recipe extraction.
/// </summary>
public class RecipeExtractionRequest
{
    /// <summary>
    /// The type of input: "image", "url", or "text"
    /// </summary>
    public string InputType { get; set; } = "image";

    /// <summary>
    /// Base64-encoded image data (when inputType is "image")
    /// </summary>
    public string? ImageBase64 { get; set; }

    /// <summary>
    /// Image content type (e.g., "image/jpeg", "image/png")
    /// </summary>
    public string? ImageContentType { get; set; }

    /// <summary>
    /// URL to a recipe website (when inputType is "url")
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Pasted recipe text (when inputType is "text")
    /// </summary>
    public string? Text { get; set; }
}

namespace MealPlanOrganizer.Mobile.Models;

/// <summary>
/// Response model from GenAI recipe extraction.
/// </summary>
public class RecipeExtractionResponse
{
    /// <summary>
    /// Whether the extraction was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Confidence score (0.0 - 1.0) indicating extraction quality.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// The extracted recipe data.
    /// </summary>
    public ExtractedRecipe? ExtractedRecipe { get; set; }

    /// <summary>
    /// URL to the source image in blob storage (if image input).
    /// </summary>
    public string? SourceImageUrl { get; set; }

    /// <summary>
    /// Error message if extraction failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

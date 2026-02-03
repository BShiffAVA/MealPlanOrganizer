namespace MealPlanOrganizer.Functions.Models
{
    /// <summary>
    /// An ingredient extracted from a recipe by GenAI.
    /// </summary>
    public class ExtractedIngredient
    {
        /// <summary>
        /// Name of the ingredient (e.g., "all-purpose flour")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Numeric quantity (e.g., 2.0, 0.5)
        /// </summary>
        public decimal? Quantity { get; set; }

        /// <summary>
        /// Unit of measure (e.g., "cups", "tbsp", "oz")
        /// </summary>
        public string? Unit { get; set; }
    }
}

using System.Collections.Generic;

namespace MealPlanOrganizer.Functions.Models
{
    /// <summary>
    /// Structured recipe data extracted by GenAI.
    /// </summary>
    public class ExtractedRecipe
    {
        /// <summary>
        /// Recipe name/title
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Brief description of the recipe
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Cuisine type or category (e.g., Italian, Mexican, Dessert)
        /// </summary>
        public string? CuisineType { get; set; }

        /// <summary>
        /// Preparation time in minutes
        /// </summary>
        public int? PrepMinutes { get; set; }

        /// <summary>
        /// Cooking time in minutes
        /// </summary>
        public int? CookMinutes { get; set; }

        /// <summary>
        /// Number of servings
        /// </summary>
        public int? Servings { get; set; }

        /// <summary>
        /// List of ingredients with quantities and units
        /// </summary>
        public List<ExtractedIngredient> Ingredients { get; set; } = new();

        /// <summary>
        /// List of cooking steps/instructions
        /// </summary>
        public List<ExtractedStep> Steps { get; set; } = new();
    }
}

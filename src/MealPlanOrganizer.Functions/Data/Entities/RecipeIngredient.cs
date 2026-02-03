using System;

namespace MealPlanOrganizer.Functions.Data.Entities
{
    public class RecipeIngredient
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RecipeId { get; set; }
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Numeric quantity value (e.g., 2.0, 0.5)
        /// </summary>
        public decimal? QuantityValue { get; set; }
        
        /// <summary>
        /// Unit of measure (e.g., "cups", "tbsp", "oz")
        /// </summary>
        public string? Unit { get; set; }
        
        /// <summary>
        /// Original quantity text for display (e.g., "2 cups", "1/2 tsp")
        /// </summary>
        public string? Quantity { get; set; }

        public Recipe? Recipe { get; set; }
    }
}

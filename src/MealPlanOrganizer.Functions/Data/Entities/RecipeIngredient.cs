using System;

namespace MealPlanOrganizer.Functions.Data.Entities
{
    public class RecipeIngredient
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RecipeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public String? Quantity { get; set; }

        public Recipe? Recipe { get; set; }
    }
}

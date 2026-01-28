using System;
using System.Collections.Generic;

namespace MealPlanOrganizer.Functions.Data.Entities
{
    public class Recipe
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }        public string? CuisineType { get; set; }
        public int? PrepTimeMinutes { get; set; }
        public int? CookTimeMinutes { get; set; }
        public int? Servings { get; set; }
        public string? ImageUrl { get; set; }
        public string? CreatedBy { get; set; }        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
        public ICollection<RecipeStep> Steps { get; set; } = new List<RecipeStep>();
    }
}

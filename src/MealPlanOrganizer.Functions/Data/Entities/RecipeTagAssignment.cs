using System;

namespace MealPlanOrganizer.Functions.Data.Entities
{
    public class RecipeTagAssignment
    {
        public Guid RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;

        public Guid TagId { get; set; }
        public RecipeTag Tag { get; set; } = null!;
    }
}

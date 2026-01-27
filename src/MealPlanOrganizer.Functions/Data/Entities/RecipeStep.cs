using System;

namespace MealPlanOrganizer.Functions.Data.Entities
{
    public class RecipeStep
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RecipeId { get; set; }
        public int StepNumber { get; set; }
        public string Instruction { get; set; } = string.Empty;

        public Recipe? Recipe { get; set; }
    }
}

using System;

namespace MealPlanOrganizer.Functions.Data.Entities
{
    public class RecipeTag
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Normalized tag name: lowercase, no # prefix, alphanumeric and hyphens only.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public ICollection<RecipeTagAssignment> Assignments { get; set; } = new List<RecipeTagAssignment>();
    }
}

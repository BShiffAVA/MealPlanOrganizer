using System.Collections.Generic;

namespace MealPlanOrganizer.Functions.Models
{
    public class RecipeCreateRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public IList<string>? Ingredients { get; set; }
        public IList<string>? Steps { get; set; }
        public string? ImageUrl { get; set; }
    }
}

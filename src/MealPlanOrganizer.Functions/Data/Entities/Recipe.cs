using System;
using System.Collections.Generic;

namespace MealPlanOrganizer.Functions.Data.Entities
{
    public class Recipe
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CuisineType { get; set; }
        public int? PrepTimeMinutes { get; set; }
        public int? CookTimeMinutes { get; set; }
        public int? Servings { get; set; }
        public string? ImageUrl { get; set; }
        
        /// <summary>
        /// Display name of the user who created the recipe
        /// </summary>
        public string? CreatedBy { get; set; }
        
        /// <summary>
        /// Foreign key to the User who created this recipe
        /// </summary>
        public Guid? CreatedByUserId { get; set; }
        
        /// <summary>
        /// Navigation property to the creator
        /// </summary>
        public User? Creator { get; set; }
        
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedUtc { get; set; }

        // GenAI extraction metadata
        /// <summary>
        /// Indicates whether this recipe was created via GenAI extraction
        /// </summary>
        public bool IsExtracted { get; set; } = false;
        
        /// <summary>
        /// URL to the source image used for extraction (if applicable)
        /// </summary>
        public string? SourceImageUrl { get; set; }
        
        /// <summary>
        /// Confidence score from the GenAI extraction (0.0 to 1.0)
        /// </summary>
        public decimal? ExtractionConfidence { get; set; }

        public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
        public ICollection<RecipeStep> Steps { get; set; } = new List<RecipeStep>();
        public ICollection<RecipeRating> Ratings { get; set; } = new List<RecipeRating>();
        public ICollection<RecipeTagAssignment> TagAssignments { get; set; } = new List<RecipeTagAssignment>();
    }
}

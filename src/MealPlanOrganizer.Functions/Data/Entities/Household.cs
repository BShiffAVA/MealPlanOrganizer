using System;
using System.Collections.Generic;

namespace MealPlanOrganizer.Functions.Data.Entities
{
    /// <summary>
    /// Represents a household that contains family members sharing recipes and meal plans
    /// </summary>
    public class Household
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public string Name { get; set; } = string.Empty;
        
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// The user who created this household (the initial admin)
        /// </summary>
        public Guid CreatedByUserId { get; set; }
        
        /// <summary>
        /// IANA timezone identifier for the household (e.g., "America/New_York").
        /// Used for scheduling notifications at the correct local time.
        /// </summary>
        public string TimeZoneId { get; set; } = "America/New_York";
        
        // Navigation properties
        public User? CreatedByUser { get; set; }
        public ICollection<HouseholdMember> Members { get; set; } = new List<HouseholdMember>();
        public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
        public ICollection<MealPlan> MealPlans { get; set; } = new List<MealPlan>();
    }
}

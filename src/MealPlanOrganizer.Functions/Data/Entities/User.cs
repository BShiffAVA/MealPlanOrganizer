using System;
using System.Collections.Generic;

namespace MealPlanOrganizer.Functions.Data.Entities
{
    /// <summary>
    /// Represents a user synced from Microsoft Entra External ID
    /// </summary>
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// Object ID from Microsoft Entra External ID (oid claim)
        /// </summary>
        public string ExternalIdObjectId { get; set; } = string.Empty;
        
        public string Email { get; set; } = string.Empty;
        
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether the user's email has been confirmed in External ID
        /// </summary>
        public bool EmailConfirmed { get; set; } = false;
        
        public string? PhotoUrl { get; set; }
        
        public string? PreferencesJson { get; set; }
        
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public ICollection<HouseholdMember> HouseholdMemberships { get; set; } = new List<HouseholdMember>();
        public ICollection<Recipe> CreatedRecipes { get; set; } = new List<Recipe>();
        public ICollection<MealPlan> CreatedMealPlans { get; set; } = new List<MealPlan>();
    }
}

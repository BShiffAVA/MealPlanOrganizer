using System;

namespace MealPlanOrganizer.Functions.Models
{
    /// <summary>
    /// Response model for household creation and details
    /// </summary>
    public class HouseholdResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public Guid CreatedByUserId { get; set; }
        
        /// <summary>
        /// IANA timezone identifier for the household (e.g., "America/New_York").
        /// </summary>
        public string TimeZoneId { get; set; } = "America/New_York";
    }
}

using System;
using System.Collections.Generic;

namespace MealPlanOrganizer.Functions.Models
{
    /// <summary>
    /// Response model for the current user including household information
    /// </summary>
    public class UserResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }
        public DateTime CreatedUtc { get; set; }
        public HouseholdInfo? Household { get; set; }
    }

    public class HouseholdInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = "America/New_York";
        public List<HouseholdMemberInfo> Members { get; set; } = new();
    }

    public class HouseholdMemberInfo
    {
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int Weight { get; set; } = 3;
        public DateTime JoinedUtc { get; set; }
    }
}

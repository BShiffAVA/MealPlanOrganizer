using System;

namespace MealPlanOrganizer.Functions.Models
{
    /// <summary>
    /// Response model for validating an invite code (before committing to join)
    /// </summary>
    public class ValidateInviteCodeResponse
    {
        /// <summary>
        /// Whether the invite code is valid (not expired, not revoked, not used)
        /// </summary>
        public bool IsValid { get; set; }
        
        /// <summary>
        /// Name of the household this code provides access to (null if invalid)
        /// </summary>
        public string? HouseholdName { get; set; }
        
        /// <summary>
        /// When this invite code expires (null if invalid)
        /// </summary>
        public DateTime? ExpiresUtc { get; set; }
        
        /// <summary>
        /// Error message if the code is invalid
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}

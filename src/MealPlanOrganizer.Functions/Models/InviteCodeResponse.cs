using System;

namespace MealPlanOrganizer.Functions.Models
{
    /// <summary>
    /// Response model for invite code operations
    /// </summary>
    public class InviteCodeResponse
    {
        public Guid Id { get; set; }
        
        /// <summary>
        /// The 8-character alphanumeric invite code
        /// </summary>
        public string Code { get; set; } = string.Empty;
        
        /// <summary>
        /// Name of the household this code provides access to
        /// </summary>
        public string HouseholdName { get; set; } = string.Empty;
        
        public Guid HouseholdId { get; set; }
        
        /// <summary>
        /// When this invite code expires
        /// </summary>
        public DateTime ExpiresUtc { get; set; }
        
        /// <summary>
        /// When this invite code was created
        /// </summary>
        public DateTime CreatedUtc { get; set; }
        
        /// <summary>
        /// Whether this invite code has been used
        /// </summary>
        public bool IsUsed { get; set; }
        
        /// <summary>
        /// Email of the user who used this code (if used)
        /// </summary>
        public string? UsedByEmail { get; set; }
        
        /// <summary>
        /// When this code was used (if used)
        /// </summary>
        public DateTime? UsedUtc { get; set; }
        
        /// <summary>
        /// Whether this invite code has been revoked
        /// </summary>
        public bool IsRevoked { get; set; }
        
        /// <summary>
        /// Whether this invite code is currently valid
        /// </summary>
        public bool IsValid { get; set; }
    }
}

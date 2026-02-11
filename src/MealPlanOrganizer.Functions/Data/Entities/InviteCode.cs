using System;

namespace MealPlanOrganizer.Functions.Data.Entities
{
    /// <summary>
    /// Represents an invite code that allows a family member to join an existing household
    /// </summary>
    public class InviteCode
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// The unique 8-character alphanumeric invite code (uppercase letters and digits, no ambiguous chars)
        /// </summary>
        public string Code { get; set; } = string.Empty;
        
        /// <summary>
        /// The household this invite code grants access to
        /// </summary>
        public Guid HouseholdId { get; set; }
        
        /// <summary>
        /// The admin user who created this invite code
        /// </summary>
        public Guid CreatedByUserId { get; set; }
        
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When this invite code expires (default: 30 days from creation)
        /// </summary>
        public DateTime ExpiresUtc { get; set; }
        
        /// <summary>
        /// Whether this invite code has been revoked by an admin
        /// </summary>
        public bool IsRevoked { get; set; } = false;
        
        /// <summary>
        /// The user who used this invite code (null if not yet used)
        /// </summary>
        public Guid? UsedByUserId { get; set; }
        
        /// <summary>
        /// When this invite code was used (null if not yet used)
        /// </summary>
        public DateTime? UsedUtc { get; set; }
        
        // Navigation properties
        public Household? Household { get; set; }
        public User? CreatedByUser { get; set; }
        public User? UsedByUser { get; set; }
        
        /// <summary>
        /// Checks if the invite code is valid (not expired, not revoked, not used)
        /// </summary>
        public bool IsValid => !IsRevoked && !UsedByUserId.HasValue && ExpiresUtc > DateTime.UtcNow;
        
        /// <summary>
        /// Generates a random 8-character alphanumeric code without ambiguous characters
        /// </summary>
        public static string GenerateCode()
        {
            // Exclude ambiguous characters: 0, O, I, 1, L
            const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
            var random = new Random();
            var code = new char[8];
            for (int i = 0; i < 8; i++)
            {
                code[i] = chars[random.Next(chars.Length)];
            }
            return new string(code);
        }
    }
}

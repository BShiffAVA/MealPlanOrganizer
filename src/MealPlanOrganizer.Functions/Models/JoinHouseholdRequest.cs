using System.ComponentModel.DataAnnotations;

namespace MealPlanOrganizer.Functions.Models
{
    /// <summary>
    /// Request model for joining a household with an invite code
    /// </summary>
    public class JoinHouseholdRequest
    {
        /// <summary>
        /// The invite code to use for joining the household
        /// </summary>
        [Required(ErrorMessage = "Invite code is required")]
        [StringLength(8, MinimumLength = 8, ErrorMessage = "Invite code must be 8 characters")]
        public string InviteCode { get; set; } = string.Empty;
    }
}

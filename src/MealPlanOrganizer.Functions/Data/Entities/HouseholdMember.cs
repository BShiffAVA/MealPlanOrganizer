using System;

namespace MealPlanOrganizer.Functions.Data.Entities
{
    /// <summary>
    /// Junction entity linking users to households with role information
    /// </summary>
    public class HouseholdMember
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public Guid UserId { get; set; }
        
        public Guid HouseholdId { get; set; }
        
        /// <summary>
        /// Role of the member in the household (Admin or Member)
        /// </summary>
        public HouseholdRole Role { get; set; } = HouseholdRole.Member;
        
        public DateTime JoinedUtc { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public User? User { get; set; }
        public Household? Household { get; set; }
    }
    
    public enum HouseholdRole
    {
        /// <summary>
        /// Regular family member with read/write access to recipes and meal plans
        /// </summary>
        Member = 0,
        
        /// <summary>
        /// Household administrator who can manage members and household settings
        /// </summary>
        Admin = 1
    }
}

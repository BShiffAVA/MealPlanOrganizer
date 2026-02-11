namespace MealPlanOrganizer.Mobile.Models;

/// <summary>
/// Response model for joining a household with an invite code.
/// </summary>
public class JoinHouseholdResponse
{
    /// <summary>
    /// Whether the join was successful.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// The ID of the household that was joined.
    /// </summary>
    public Guid HouseholdId { get; set; }
    
    /// <summary>
    /// The name of the household that was joined.
    /// </summary>
    public string? HouseholdName { get; set; }
    
    /// <summary>
    /// The role assigned to the user (typically "Member").
    /// </summary>
    public string? Role { get; set; }
}

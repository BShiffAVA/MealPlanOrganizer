namespace MealPlanOrganizer.Mobile.Models;

/// <summary>
/// Response model for validating an invite code.
/// </summary>
public class ValidateInviteCodeResponse
{
    /// <summary>
    /// Whether the code is valid and can be used.
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// Name of the household the code belongs to (only if valid).
    /// </summary>
    public string? HouseholdName { get; set; }
    
    /// <summary>
    /// When the code expires (only if valid).
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }
    
    /// <summary>
    /// Error message if not valid.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

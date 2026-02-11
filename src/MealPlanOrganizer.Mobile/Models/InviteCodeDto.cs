namespace MealPlanOrganizer.Mobile.Models;

/// <summary>
/// Data transfer object for invite codes from the backend.
/// </summary>
public class InviteCodeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string HouseholdName { get; set; } = string.Empty;
    public Guid HouseholdId { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public bool IsUsed { get; set; }
    public string? UsedByEmail { get; set; }
    public DateTime? UsedUtc { get; set; }
    public bool IsRevoked { get; set; }
    public bool IsValid { get; set; }
    
    /// <summary>
    /// Returns a formatted display of time remaining until expiration.
    /// </summary>
    public string ExpiresDisplay
    {
        get
        {
            if (!IsValid) return IsRevoked ? "Revoked" : IsUsed ? "Used" : "Expired";
            var remaining = ExpiresUtc - DateTime.UtcNow;
            if (remaining.TotalDays >= 1)
                return $"Expires in {(int)remaining.TotalDays} days";
            if (remaining.TotalHours >= 1)
                return $"Expires in {(int)remaining.TotalHours} hours";
            return "Expires soon";
        }
    }
}

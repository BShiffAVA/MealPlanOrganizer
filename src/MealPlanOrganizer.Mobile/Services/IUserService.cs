using MealPlanOrganizer.Mobile.Models;

namespace MealPlanOrganizer.Mobile.Services;

/// <summary>
/// Service for user management operations.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Registers the current authenticated user in the backend database.
    /// Called after successful Entra External ID authentication.
    /// </summary>
    Task<UserDto?> RegisterUserAsync();
    
    /// <summary>
    /// Gets the current user's information including household membership.
    /// </summary>
    Task<UserDto?> GetCurrentUserAsync();
    
    /// <summary>
    /// Creates a new household with the current user as admin.
    /// </summary>
    Task<HouseholdDto?> CreateHouseholdAsync(string name);
    
    /// <summary>
    /// Returns true if the current user has a household.
    /// </summary>
    Task<bool> HasHouseholdAsync();
    
    /// <summary>
    /// Validates an invite code and returns household info if valid.
    /// </summary>
    Task<ValidateInviteCodeResponse?> ValidateInviteCodeAsync(string code);
    
    /// <summary>
    /// Joins a household using an invite code.
    /// </summary>
    Task<JoinHouseholdResponse?> JoinHouseholdAsync(string code);
    
    /// <summary>
    /// Generates a new invite code for the specified household. Admin only.
    /// </summary>
    Task<InviteCodeDto?> GenerateInviteCodeAsync(Guid householdId);
    
    /// <summary>
    /// Gets all invite codes for the specified household. Admin only.
    /// </summary>
    Task<List<InviteCodeDto>> GetInviteCodesAsync(Guid householdId, bool includeUsed = false);
    
    /// <summary>
    /// Revokes an invite code. Admin only.
    /// </summary>
    Task<bool> RevokeInviteCodeAsync(string code);
    
    /// <summary>
    /// Removes a member from the household. Admin only.
    /// </summary>
    Task<bool> RemoveMemberAsync(Guid householdId, Guid memberId);
    
    /// <summary>
    /// Updates a member's weight (1-5). Admin only.
    /// </summary>
    Task<HouseholdMemberDto?> UpdateMemberWeightAsync(Guid householdId, Guid memberId, int weight);
}

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
}

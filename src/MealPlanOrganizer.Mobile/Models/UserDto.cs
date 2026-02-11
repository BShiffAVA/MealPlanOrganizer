using System;
using System.Collections.Generic;

namespace MealPlanOrganizer.Mobile.Models;

/// <summary>
/// Data transfer object for user information from the backend.
/// </summary>
public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public DateTime CreatedUtc { get; set; }
    public HouseholdInfoDto? Household { get; set; }
}

/// <summary>
/// Household information included with user response.
/// </summary>
public class HouseholdInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public List<HouseholdMemberDto> Members { get; set; } = new();
}

/// <summary>
/// Member information within a household.
/// </summary>
public class HouseholdMemberDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedUtc { get; set; }
}

/// <summary>
/// Data transfer object for household creation response.
/// </summary>
public class HouseholdDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
}

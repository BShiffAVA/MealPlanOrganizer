using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPlanOrganizer.Mobile.Models;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for the ManageHouseholdPage.
/// Allows viewing members, generating invite codes, and managing invites.
/// </summary>
public partial class ManageHouseholdViewModel : ObservableObject
{
    private readonly IUserService _userService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isGeneratingCode;

    [ObservableProperty]
    private string _householdName = string.Empty;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private Guid _householdId;

    [ObservableProperty]
    private ObservableCollection<HouseholdMemberDto> _members = new();

    [ObservableProperty]
    private ObservableCollection<InviteCodeDto> _inviteCodes = new();

    [ObservableProperty]
    private bool _isErrorVisible;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccessVisible;

    [ObservableProperty]
    private string _successMessage = string.Empty;

    [ObservableProperty]
    private string? _copiedCode;

    public ManageHouseholdViewModel(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Loads household data including members and invite codes.
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            IsErrorVisible = false;
            IsSuccessVisible = false;

            var user = await _userService.GetCurrentUserAsync();
            
            if (user?.Household == null)
            {
                ShowError("You are not a member of any household.");
                return;
            }

            HouseholdId = user.Household.Id;
            HouseholdName = user.Household.Name;
            IsAdmin = user.Household.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);

            // Load members
            Members.Clear();
            foreach (var member in user.Household.Members.OrderByDescending(m => m.Role == "Admin").ThenBy(m => m.DisplayName))
            {
                Members.Add(member);
            }

            // Load invite codes if admin
            if (IsAdmin)
            {
                await LoadInviteCodesAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading household: {ex.Message}");
            ShowError("Failed to load household information.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadInviteCodesAsync()
    {
        try
        {
            var codes = await _userService.GetInviteCodesAsync(HouseholdId, includeUsed: false);
            
            InviteCodes.Clear();
            foreach (var code in codes.OrderByDescending(c => c.CreatedUtc))
            {
                InviteCodes.Add(code);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading invite codes: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a new invite code.
    /// </summary>
    [RelayCommand]
    private async Task GenerateInviteCodeAsync()
    {
        if (!IsAdmin || HouseholdId == Guid.Empty)
        {
            ShowError("Only admins can generate invite codes.");
            return;
        }

        try
        {
            IsGeneratingCode = true;
            IsErrorVisible = false;
            IsSuccessVisible = false;

            var newCode = await _userService.GenerateInviteCodeAsync(HouseholdId);

            if (newCode != null)
            {
                InviteCodes.Insert(0, newCode);
                ShowSuccess($"Invite code {newCode.Code} generated! Share it with a family member.");
                
                // Copy to clipboard
                await CopyToClipboardAsync(newCode.Code);
            }
            else
            {
                ShowError("Failed to generate invite code. You may have reached the limit of 10 active codes.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error generating invite code: {ex.Message}");
            ShowError("An error occurred while generating the invite code.");
        }
        finally
        {
            IsGeneratingCode = false;
        }
    }

    /// <summary>
    /// Copies an invite code to the clipboard.
    /// </summary>
    [RelayCommand]
    private async Task CopyCodeAsync(InviteCodeDto code)
    {
        await CopyToClipboardAsync(code.Code);
        CopiedCode = code.Code;
        ShowSuccess($"Code {code.Code} copied to clipboard!");
        
        // Clear the copied indicator after a delay
        await Task.Delay(2000);
        if (CopiedCode == code.Code)
        {
            CopiedCode = null;
        }
    }

    /// <summary>
    /// Revokes an invite code.
    /// </summary>
    [RelayCommand]
    private async Task RevokeCodeAsync(InviteCodeDto code)
    {
        if (!IsAdmin)
        {
            ShowError("Only admins can revoke invite codes.");
            return;
        }

        try
        {
            IsErrorVisible = false;
            IsSuccessVisible = false;

            var success = await _userService.RevokeInviteCodeAsync(code.Code);

            if (success)
            {
                InviteCodes.Remove(code);
                ShowSuccess($"Invite code {code.Code} has been revoked.");
            }
            else
            {
                ShowError("Failed to revoke invite code.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error revoking invite code: {ex.Message}");
            ShowError("An error occurred while revoking the invite code.");
        }
    }

    /// <summary>
    /// Shares an invite code via the system share dialog.
    /// </summary>
    [RelayCommand]
    private async Task ShareCodeAsync(InviteCodeDto code)
    {
        try
        {
            await Share.RequestAsync(new ShareTextRequest
            {
                Title = "Join our household",
                Text = $"Join our household \"{HouseholdName}\" using this invite code: {code.Code}\n\nThe code expires {code.ExpiresDisplay.ToLowerInvariant()}."
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error sharing invite code: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes a member from the household.
    /// </summary>
    [RelayCommand]
    private async Task RemoveMemberAsync(HouseholdMemberDto member)
    {
        if (!IsAdmin)
        {
            ShowError("Only admins can remove members.");
            return;
        }

        try
        {
            IsErrorVisible = false;
            IsSuccessVisible = false;

            var success = await _userService.RemoveMemberAsync(HouseholdId, member.UserId);

            if (success)
            {
                Members.Remove(member);
                ShowSuccess($"{member.DisplayName} has been removed from the household.");
            }
            else
            {
                ShowError("Failed to remove member. If you're the only admin, you cannot remove yourself.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error removing member: {ex.Message}");
            ShowError("An error occurred while removing the member.");
        }
    }

    /// <summary>
    /// Updates a member's weight.
    /// </summary>
    [RelayCommand]
    private async Task UpdateMemberWeightAsync(HouseholdMemberDto member)
    {
        if (!IsAdmin)
        {
            ShowError("Only admins can update member weights.");
            return;
        }

        try
        {
            IsErrorVisible = false;

            var updatedMember = await _userService.UpdateMemberWeightAsync(HouseholdId, member.UserId, member.Weight);

            if (updatedMember != null)
            {
                // Update succeeded - the member's weight is already updated in the UI through binding
                ShowSuccess($"{member.DisplayName}'s weight updated to {member.Weight}.");
            }
            else
            {
                ShowError("Failed to update member weight.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating member weight: {ex.Message}");
            ShowError("An error occurred while updating the member weight.");
        }
    }

    /// <summary>
    /// Increases a member's weight by 1 (max 5).
    /// </summary>
    [RelayCommand]
    private async Task IncreaseWeightAsync(HouseholdMemberDto member)
    {
        if (member.Weight >= 5) return;
        
        member.Weight++;
        await UpdateMemberWeightAsync(member);
    }

    /// <summary>
    /// Decreases a member's weight by 1 (min 1).
    /// </summary>
    [RelayCommand]
    private async Task DecreaseWeightAsync(HouseholdMemberDto member)
    {
        if (member.Weight <= 1) return;
        
        member.Weight--;
        await UpdateMemberWeightAsync(member);
    }

    private async Task CopyToClipboardAsync(string text)
    {
        try
        {
            await Clipboard.SetTextAsync(text);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error copying to clipboard: {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        IsErrorVisible = true;
        IsSuccessVisible = false;
    }

    private void ShowSuccess(string message)
    {
        SuccessMessage = message;
        IsSuccessVisible = true;
        IsErrorVisible = false;
    }
}

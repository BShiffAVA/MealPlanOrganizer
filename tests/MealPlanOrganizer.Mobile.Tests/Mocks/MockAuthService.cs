namespace MealPlanOrganizer.Mobile.Tests.Mocks;

/// <summary>
/// Mock implementation of IAuthService for testing.
/// </summary>
public class MockAuthService
{
    private string? _accessToken;
    private bool _isAuthenticated;

    public MockAuthService()
    {
        _isAuthenticated = true;
        _accessToken = "mock-access-token-12345";
    }

    public void SetAuthenticated(bool authenticated, string? token = null)
    {
        _isAuthenticated = authenticated;
        _accessToken = token ?? (authenticated ? "mock-access-token-12345" : null);
    }

    public Task<bool> IsAuthenticatedAsync() => Task.FromResult(_isAuthenticated);

    public Task<string?> GetAccessTokenAsync() => Task.FromResult(_accessToken);

    public Task LogoutAsync()
    {
        _isAuthenticated = false;
        _accessToken = null;
        return Task.CompletedTask;
    }
}

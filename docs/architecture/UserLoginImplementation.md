1. Configure Microsoft Entra External ID tenant – Set up external identity tenant in Azure Portal, register mobile app, configure redirect URIs (msal://callback), and enable email/password as a sign-in method. Add app roles and API permissions for Azure Functions access.

2. Add MSAL SDK to mobile app – Install Microsoft.Identity.Client NuGet package in MealPlanOrganizer.Mobile.csproj, configure platform-specific handlers for iOS/Android, and create IAuthService interface with LoginAsync(), LogoutAsync(), GetAccessTokenAsync(), and IsAuthenticatedAsync() methods.

3. Implement AuthService with token caching – Create AuthService class using IPublicClientApplication with MSAL's built-in token cache for 30-day session persistence. Store refresh tokens securely using platform secure storage (Keychain/EncryptedSharedPreferences). Implement silent token acquisition for offline scenarios.

4. Create LoginPage UI – Build LoginPage.xaml with email/password entry fields, login button, "Forgot Password?" link (triggers MSAL password reset flow), loading indicator, and error display for invalid credentials.

5. Update app navigation and HTTP client – Modify AppShell.xaml.cs to check authentication state on startup and route to LoginPage or MainPage. Update RecipeService to inject IAuthService and attach Bearer token to API requests.

6. Configure Azure Functions JWT validation – Add JWT validation middleware in Program.cs using Microsoft.Identity.Web to validate tokens from External ID tenant. Configure issuer and audience from External ID settings.
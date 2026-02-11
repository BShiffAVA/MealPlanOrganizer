# User Login and Account Creation Implementation

## Overview
This document describes the implementation of user authentication and household account creation for the Meal Plan Organizer application, supporting US-001 (Family Admin Creates Household Account).

## Architecture

### Authentication Flow
1. User opens app → LoginPage is shown
2. User taps "Sign In with Microsoft" or "Create Account" → MSAL interactive login
3. After successful Entra External ID authentication → Register user in backend database
4. If user has no household → Navigate to CreateHouseholdPage
5. If user has household → Navigate to AppShell (main app)

### Components

#### Backend (Azure Functions)
- **POST `/users/register`** - Syncs authenticated user from Entra to local database
- **GET `/users/me`** - Returns current user with household membership info
- **POST `/households`** - Creates a new household with calling user as admin

#### Mobile Services
- **IUserService / UserService** - Handles user registration and household operations
- **IAuthService / AuthService** - Handles MSAL authentication with Entra External ID

#### Mobile ViewModels
- **LoginViewModel** - Sign in/up flow, user registration, navigation routing
- **CreateHouseholdViewModel** - Household creation for new users

#### Mobile Pages
- **LoginPage** - Sign in and create account buttons
- **CreateHouseholdPage** - Household name entry for new users

## Implementation Steps

1. Configure Microsoft Entra External ID tenant – Set up external identity tenant in Azure Portal, register mobile app, configure redirect URIs (msal://callback), and enable email/password as a sign-in method. Add app roles and API permissions for Azure Functions access.

2. Add MSAL SDK to mobile app – Install Microsoft.Identity.Client NuGet package in MealPlanOrganizer.Mobile.csproj, configure platform-specific handlers for iOS/Android, and create IAuthService interface with LoginAsync(), LogoutAsync(), GetAccessTokenAsync(), and IsAuthenticatedAsync() methods.

3. Implement AuthService with token caching – Create AuthService class using IPublicClientApplication with MSAL's built-in token cache for 30-day session persistence. Store refresh tokens securely using platform secure storage (Keychain/EncryptedSharedPreferences). Implement silent token acquisition for offline scenarios.

4. Create LoginPage UI – Build LoginPage.xaml with "Sign In with Microsoft" and "Create Account" buttons, loading indicator, and error display for invalid credentials.

5. Update app navigation and HTTP client – Modify AppShell.xaml.cs to check authentication state on startup and route to LoginPage or MainPage. Update RecipeService to inject IAuthService and attach Bearer token to API requests.

6. Configure Azure Functions JWT validation – Add JWT validation middleware in Program.cs using Microsoft.Identity.Web to validate tokens from External ID tenant. Configure issuer and audience from External ID settings.

7. Add User and Household database entities – Create User, Household, and HouseholdMember entities in the Functions project with proper relationships and indexes.

8. Create user registration endpoint – Implement POST /users/register to sync Entra user to local database after first login.

9. Create household endpoints – Implement GET /users/me and POST /households for user info and household creation.

10. Add CreateHouseholdPage – New users without a household are redirected here to create their household.

## Entra External ID Configuration

### User Flow Settings (Azure Portal)
- **Identity Provider**: Email/Password (local accounts)
- **Email Verification**: Required before account activation
- **Password Policy**: 
  - Minimum 8 characters
  - Mixed case required
  - Number required
  - Special character required
- **Verification Code Expiry**: 24 hours

### App Registration
- **Platform**: Mobile and desktop applications
- **Redirect URIs**: 
  - `msal{ClientId}://auth` (Android/iOS)
  - `http://localhost` (Windows development)
- **API Permissions**: Default Microsoft Graph (User.Read)

## Database Schema

### Users Table
| Column | Type | Description |
|--------|------|-------------|
| Id | uniqueidentifier | Primary key |
| ExternalIdObjectId | nvarchar(100) | Entra object ID (unique) |
| Email | nvarchar(256) | User email (unique) |
| DisplayName | nvarchar(200) | Display name |
| EmailConfirmed | bit | Email confirmed status |
| PhotoUrl | nvarchar(2000) | Profile photo URL |
| PreferencesJson | nvarchar(max) | User preferences |
| CreatedUtc | datetime2 | Account creation date |

### Households Table
| Column | Type | Description |
|--------|------|-------------|
| Id | uniqueidentifier | Primary key |
| Name | nvarchar(200) | Household name |
| CreatedUtc | datetime2 | Creation date |
| CreatedByUserId | uniqueidentifier | FK to Users |

### HouseholdMembers Table
| Column | Type | Description |
|--------|------|-------------|
| Id | uniqueidentifier | Primary key |
| UserId | uniqueidentifier | FK to Users |
| HouseholdId | uniqueidentifier | FK to Households |
| Role | nvarchar(50) | Admin or Member |
| JoinedUtc | datetime2 | Join date |
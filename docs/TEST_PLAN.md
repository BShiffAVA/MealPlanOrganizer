# Meal Plan Organizer - Automated Test Plan

## Overview

This document defines the automated testing strategy for the MealPlanOrganizer application, covering the Azure Functions backend and .NET MAUI mobile app. UI testing is documented for future implementation as the mobile UI is expected to change frequently.

**Testing Framework**: xUnit with FluentAssertions  
**Mocking**: Moq  
**Integration Testing**: Testcontainers (SQL Server, Azurite)  
**Target Coverage**: 80%+ for business logic  

---

## Test Project Structure

```
tests/
├── MealPlanOrganizer.Functions.Tests/
│   ├── MealPlanOrganizer.Functions.Tests.csproj
│   ├── Unit/
│   │   ├── Services/
│   │   │   ├── RecipeRecommendationServiceTests.cs
│   │   │   ├── RecipeExtractionServiceTests.cs
│   │   │   ├── JwtValidationServiceTests.cs
│   │   │   ├── AuthenticationHelperTests.cs
│   │   │   └── BlobUrlServiceTests.cs
│   │   └── Functions/
│   │       ├── RecipeFunctionsTests.cs
│   │       ├── RatingFunctionsTests.cs
│   │       └── MealPlanFunctionsTests.cs
│   ├── Integration/
│   │   ├── Endpoints/
│   │   │   ├── RecipeEndpointsTests.cs
│   │   │   ├── ExtractionEndpointsTests.cs
│   │   │   ├── RatingsEndpointsTests.cs
│   │   │   ├── MealPlanEndpointsTests.cs
│   │   │   ├── RecommendationsEndpointsTests.cs
│   │   │   └── AuthEndpointsTests.cs
│   │   └── Fixtures/
│   │       ├── DatabaseFixture.cs
│   │       ├── AzuriteFixture.cs
│   │       ├── MockOpenAIHandler.cs
│   │       └── TestAuthHandler.cs
│   └── Builders/
│       ├── RecipeBuilder.cs
│       ├── MealPlanBuilder.cs
│       └── UserBuilder.cs
│
└── MealPlanOrganizer.Mobile.Tests/
    ├── MealPlanOrganizer.Mobile.Tests.csproj
    ├── Services/
    │   ├── RecipeServiceTests.cs
    │   └── AuthServiceTests.cs
    └── Mocks/
        ├── MockHttpMessageHandler.cs
        └── MockAuthService.cs
```

---

## Part 1: Azure Functions Backend Testing

### 1.1 Unit Tests - Services

#### RecipeRecommendationServiceTests

Tests the smart recipe scoring algorithm (30% rating, 40% frequency preference, 30% recency penalty).

| Test Case | Description | Expected Result |
|-----------|-------------|-----------------|
| `ScoreRecipe_HighRating_ReturnsHighScore` | Recipe with 5-star average | Score reflects 30% weight for rating |
| `ScoreRecipe_FrequencyOnceAWeek_BoostsScore` | Recipe marked "once a week" | 40% frequency weight applied |
| `ScoreRecipe_RecentlyUsed_AppliesPenalty` | Recipe used in last 7 days | Recency penalty reduces score |
| `ScoreRecipe_NoRatings_UsesDefaultScore` | Recipe with no ratings | Returns baseline score |
| `ScoreRecipe_MultipleUserRatings_AveragesCorrectly` | 4 family members rated | Correctly averages all ratings |
| `GetRecommendations_EmptyDatabase_ReturnsEmptyList` | No recipes exist | Returns empty list, no errors |
| `GetRecommendations_ExcludesRecentlyUsed` | Recipe used yesterday | Excluded from recommendations |
| `GetRecommendations_RespectsMealPlanDuration` | 7-day meal plan context | Adjusts recency window appropriately |

#### RecipeExtractionServiceTests

Tests GenAI-powered recipe extraction from images, URLs, and text.

| Test Case | Description | Expected Result |
|-----------|-------------|-----------------|
| `ExtractFromImage_ValidImage_ReturnsRecipe` | Base64 cookbook photo | Extracts title, ingredients, steps |
| `ExtractFromImage_LowQuality_ReturnsLowConfidence` | Blurry image | Confidence score < 0.7 |
| `ExtractFromUrl_ValidRecipeUrl_ReturnsRecipe` | URL to recipe website | Parses webpage and extracts recipe |
| `ExtractFromUrl_NonRecipePage_ThrowsException` | URL to news article | Throws `ExtractionFailedException` |
| `ExtractFromText_PlainText_ReturnsRecipe` | Pasted recipe text | Parses ingredients and steps |
| `ExtractFromText_PartialRecipe_FillsDefaults` | Missing prep time | Uses default values, notes in confidence |
| `Extract_OpenAIError_ThrowsServiceException` | OpenAI returns 500 | Throws `RecipeExtractionException` |
| `Extract_MalformedResponse_HandlesGracefully` | Invalid JSON from OpenAI | Throws with meaningful error message |
| `Extract_RateLimited_ReturnsRetryAfter` | OpenAI 429 response | Returns retry-after information |

#### JwtValidationServiceTests

Tests Microsoft Entra External ID JWT token validation.

| Test Case | Description | Expected Result |
|-----------|-------------|-----------------|
| `ValidateToken_ValidJwt_ReturnsClaimsPrincipal` | Valid, unexpired token | Returns authenticated principal |
| `ValidateToken_ExpiredJwt_ThrowsSecurityException` | Token expired 1 hour ago | Throws `SecurityTokenExpiredException` |
| `ValidateToken_InvalidSignature_ThrowsException` | Tampered token | Throws `SecurityTokenInvalidSignatureException` |
| `ValidateToken_WrongAudience_ThrowsException` | Token for different app | Throws `SecurityTokenInvalidAudienceException` |
| `ValidateToken_WrongIssuer_ThrowsException` | Token from different tenant | Throws `SecurityTokenInvalidIssuerException` |
| `ValidateToken_MissingSubClaim_ThrowsException` | No user identifier | Throws `SecurityTokenException` |
| `ValidateToken_Null_ThrowsArgumentException` | Null token string | Throws `ArgumentNullException` |

#### AuthenticationHelperTests

Tests HTTP request authentication handling.

| Test Case | Description | Expected Result |
|-----------|-------------|-----------------|
| `Authenticate_ValidBearerToken_ReturnsUser` | `Authorization: Bearer {jwt}` | Returns authenticated user ID |
| `Authenticate_MissingHeader_ReturnsNull` | No Authorization header | Returns null (unauthenticated) |
| `Authenticate_MalformedHeader_ReturnsNull` | `Authorization: token123` | Returns null |
| `Authenticate_EmptyBearer_ReturnsNull` | `Authorization: Bearer ` | Returns null |
| `Authenticate_BasicAuth_ReturnsNull` | `Authorization: Basic xyz` | Returns null (wrong scheme) |

#### BlobUrlServiceTests

Tests Azure Blob Storage SAS URL generation.

| Test Case | Description | Expected Result |
|-----------|-------------|-----------------|
| `GenerateSasUrl_ValidBlob_ReturnsUrl` | Existing blob name | Returns valid SAS URL |
| `GenerateSasUrl_ExpiryTime_SetCorrectly` | Default expiry | URL expires in configured time |
| `GenerateSasUrl_ReadPermissions_Correct` | Download URL | SAS has read permission only |
| `GenerateSasUrl_WritePermissions_Correct` | Upload URL | SAS has write permission |
| `GenerateSasUrl_EmptyBlobName_ThrowsException` | Empty string | Throws `ArgumentException` |

---

### 1.2 Unit Tests - Functions

#### RecipeFunctionsTests

| Test Case | Description | Expected Result |
|-----------|-------------|-----------------|
| `CreateRecipe_ValidRequest_Returns201` | Complete recipe data | Returns 201 with recipe ID |
| `CreateRecipe_MissingTitle_Returns400` | No title field | Returns 400 with validation error |
| `CreateRecipe_EmptyIngredients_Returns400` | Zero ingredients | Returns 400 with validation error |
| `GetRecipe_ExistingId_Returns200` | Valid recipe GUID | Returns recipe with ingredients and steps |
| `GetRecipe_NonExistentId_Returns404` | Random GUID | Returns 404 |
| `UpdateRecipe_ValidRequest_Returns200` | Updated fields | Returns updated recipe |
| `ListRecipes_WithRatings_IncludesAverages` | Recipes with ratings | Each recipe includes avgRating |

#### RatingFunctionsTests

| Test Case | Description | Expected Result |
|-----------|-------------|-----------------|
| `RateRecipe_ValidRating_Returns200` | 1-5 star rating | Saves rating, returns success |
| `RateRecipe_InvalidRating_Returns400` | Rating = 6 | Returns 400 validation error |
| `RateRecipe_Unauthenticated_Returns401` | No auth header | Returns 401 |
| `RateRecipe_UpdateExisting_Returns200` | User rates again | Updates existing rating |
| `GetRatings_HasRatings_ReturnsAll` | Recipe with 4 ratings | Returns all ratings with users |
| `GetUserHistory_Authenticated_ReturnsHistory` | User with 10 ratings | Returns paginated history |

#### MealPlanFunctionsTests

| Test Case | Description | Expected Result |
|-----------|-------------|-----------------|
| `CreateMealPlan_ValidDates_Returns201` | Valid week range | Returns meal plan ID |
| `CreateMealPlan_EndBeforeStart_Returns400` | Invalid date range | Returns 400 |
| `AddRecipe_ValidDay_Returns200` | Day within plan range | Recipe assigned to day |
| `AddRecipe_OutOfRange_Returns400` | Day outside plan dates | Returns 400 |
| `RemoveRecipe_Existing_Returns200` | Recipe on day | Removes assignment |
| `GetMealPlan_WithRecipes_ReturnsFull` | Plan with 5 recipes | Returns all days with recipes |

---

### 1.3 Integration Tests

Integration tests use Testcontainers for SQL Server and Azurite for blob storage. Tests run against actual Azure Function hosts with injected test dependencies.

#### Test Fixtures

**DatabaseFixture**
- Spins up SQL Server container
- Runs EF Core migrations
- Seeds test data (recipes, users, ratings)
- Provides fresh database per test class

**AzuriteFixture**
- Runs Azurite container for blob storage
- Pre-seeds test images
- Provides isolated container per test class

**MockOpenAIHandler**
- HTTP handler that intercepts OpenAI calls
- Returns deterministic extraction responses
- Simulates various error conditions

**TestAuthHandler**
- Generates valid JWT tokens for test users
- Configurable user ID, household ID, roles

#### RecipeEndpointsTests

| Test Case | Description |
|-----------|-------------|
| `POST_Recipes_CreatesRecipeInDatabase` | Full end-to-end recipe creation |
| `GET_Recipes_ReturnsAllWithPagination` | List with page/pageSize query params |
| `GET_Recipe_IncludesAllRelatedData` | Single recipe with ingredients, steps, ratings |
| `PUT_Recipe_UpdatesAllFields` | Update and verify in database |
| `POST_UploadImage_StoresInBlob` | Multipart upload stored in Azurite |
| `DELETE_Recipe_CascadesCorrectly` | Removes related ingredients, steps, ratings |

#### ExtractionEndpointsTests

| Test Case | Description |
|-----------|-------------|
| `POST_Extract_Image_ReturnsRecipe` | Base64 image extraction flow |
| `POST_Extract_Url_ReturnsRecipe` | URL-based extraction |
| `POST_Extract_Text_ReturnsRecipe` | Plain text extraction |
| `POST_Extract_InvalidInput_Returns400` | Missing inputType field |
| `POST_Extract_ServiceError_Returns500` | OpenAI unavailable |

#### RatingsEndpointsTests

| Test Case | Description |
|-----------|-------------|
| `POST_Rating_SavesWithUserId` | Rating linked to authenticated user |
| `POST_Rating_UpdatesRecipeAverage` | avgRating recalculated |
| `GET_Ratings_FiltersByRecipe` | Only ratings for specified recipe |
| `GET_UserHistory_PaginatesCorrectly` | Respects page/pageSize |
| `POST_Rating_WithFrequency_SavesPreference` | frequencyPreference stored |

#### MealPlanEndpointsTests

| Test Case | Description |
|-----------|-------------|
| `POST_MealPlan_CreatesWithStatus` | Status defaults to "Draft" |
| `GET_MealPlan_IncludesRecipeDetails` | Recipes populated with full info |
| `POST_AddRecipe_AssociatesCorrectly` | Junction table updated |
| `DELETE_RemoveRecipe_RemovesAssociation` | Only removes junction record |
| `GET_MealPlans_FiltersByHousehold` | Users only see their plans |

#### RecommendationsEndpointsTests

| Test Case | Description |
|-----------|-------------|
| `GET_Recommendations_UsesRatingAlgorithm` | High-rated recipes score higher |
| `GET_Recommendations_RespectsRecency` | Recently used recipes penalized |
| `GET_Recommendations_RequiresAuth` | 401 without token |
| `GET_Recommendations_EmptyState_ReturnsEmpty` | No recipes returns [] |

#### AuthEndpointsTests

| Test Case | Description |
|-----------|-------------|
| `ProtectedEndpoint_NoToken_Returns401` | CreateRecipe without auth |
| `ProtectedEndpoint_ExpiredToken_Returns401` | Expired JWT rejected |
| `ProtectedEndpoint_ValidToken_Returns200` | Valid JWT accepted |
| `PublicEndpoint_NoToken_Returns200` | ListRecipes works unauthenticated |

---

## Part 2: Mobile App Testing

### 2.1 Unit Tests - Services

#### RecipeServiceTests

Tests HTTP client wrapper with mocked responses.

| Test Case | Description | Expected Result |
|-----------|-------------|-----------------|
| `GetRecipes_Success_ReturnsList` | 200 response | Deserializes recipe list |
| `GetRecipes_Empty_ReturnsEmptyList` | 200 with [] | Returns empty list |
| `GetRecipes_NetworkError_ThrowsException` | Connection timeout | Throws `HttpRequestException` |
| `GetRecipeById_Found_ReturnsDetail` | 200 with recipe | Full recipe with ingredients |
| `GetRecipeById_NotFound_ReturnsNull` | 404 response | Returns null |
| `CreateRecipe_Success_ReturnsId` | 201 response | Returns new recipe ID |
| `CreateRecipe_ValidationError_ThrowsException` | 400 response | Throws with validation details |
| `UpdateRecipe_Success_ReturnsUpdated` | 200 response | Returns updated recipe |
| `RateRecipe_Success_ReturnsResult` | 200 response | Returns rating result |
| `RateRecipe_Unauthorized_ThrowsException` | 401 response | Throws `UnauthorizedAccessException` |
| `GetMealPlans_Success_ReturnsList` | 200 response | Deserializes meal plan list |
| `CreateMealPlan_Success_ReturnsId` | 201 response | Returns new meal plan ID |
| `AddRecipeToMealPlan_Success_ReturnsResult` | 200 response | Confirms assignment |
| `GetRecommendations_Success_ReturnsList` | 200 response | Deserializes recommendations |
| `ExtractRecipe_Image_ReturnsExtracted` | 200 response | Returns extracted recipe data |
| `ExtractRecipe_LowConfidence_IncludesWarning` | Confidence < 0.7 | Response includes warning flag |

#### AuthServiceTests

Tests MSAL authentication flow with mocked identity client.

| Test Case | Description | Expected Result |
|-----------|-------------|-----------------|
| `Login_Success_ReturnsToken` | Valid credentials | Returns access token |
| `Login_InvalidCredentials_ThrowsException` | Wrong password | Throws `MsalUiRequiredException` |
| `Login_NetworkError_ThrowsException` | No connectivity | Throws `MsalServiceException` |
| `GetCachedToken_Valid_ReturnsToken` | Token in cache | Returns without network call |
| `GetCachedToken_Expired_RefreshesToken` | Expired token | Silently refreshes |
| `Logout_ClearsCache` | User logs out | Token cache cleared |
| `IsAuthenticated_WithToken_ReturnsTrue` | Valid token exists | Returns true |
| `IsAuthenticated_NoToken_ReturnsFalse` | No cached token | Returns false |

---

### 2.2 Test Mocks

#### MockHttpMessageHandler

```csharp
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, HttpResponseMessage> _responses = new();
    
    public void SetupResponse(string url, HttpStatusCode status, object content);
    public void SetupError(string url, Exception exception);
    public void VerifyRequestMade(string url, HttpMethod method, Times times);
}
```

#### MockAuthService

```csharp
public class MockAuthService : IAuthService
{
    public bool IsAuthenticated { get; set; }
    public string MockToken { get; set; }
    public string MockUserId { get; set; }
    
    public Task<string> LoginAsync() => Task.FromResult(MockToken);
    public Task LogoutAsync() { IsAuthenticated = false; return Task.CompletedTask; }
}
```

---

## Part 3: UI Testing (Documented for Future Implementation)

> **Note**: UI testing is deferred as the mobile UI is expected to change frequently. This section documents the planned approach for when UI stabilizes.

### 3.1 Recommended Framework

- **Primary**: .NET MAUI UITest (Appium-based)
- **Alternative**: Xamarin.UITest (legacy compatibility)
- **Device Farms**: Azure DevOps with App Center Test (optional)

### 3.2 UI Test Scenarios by Page

#### LoginPage

| Test | Steps | Expected |
|------|-------|----------|
| Valid Login | Enter email, password, tap Login | Navigates to MainPage |
| Invalid Credentials | Enter wrong password, tap Login | Shows error message |
| Empty Fields | Leave fields empty, tap Login | Shows validation message |
| Network Error | Disconnect network, attempt login | Shows offline message |

#### MainPage (Recipe List)

| Test | Steps | Expected |
|------|-------|----------|
| Load Recipes | Navigate to page | Recipes display in list |
| Empty State | No recipes exist | Shows "Add your first recipe" prompt |
| Pull to Refresh | Swipe down | Refreshes recipe list |
| Navigate to Detail | Tap recipe | Opens RecipeDetailPage |
| Search Recipes | Enter search text | Filters visible recipes |
| Add Recipe Button | Tap FAB/Add button | Opens AddRecipePage |

#### AddRecipePage

| Test | Steps | Expected |
|------|-------|----------|
| Create Recipe | Fill all fields, tap Save | Recipe created, returns to list |
| Validation | Submit empty form | Shows required field errors |
| Add Ingredient | Tap Add Ingredient | New ingredient row appears |
| Remove Ingredient | Swipe/delete ingredient | Ingredient removed |
| Add Step | Tap Add Step | New step row appears |
| Take Photo | Tap camera button | Camera opens |
| Cancel | Tap Cancel/Back | Returns without saving |

#### ExtractRecipePage

| Test | Steps | Expected |
|------|-------|----------|
| Select Image | Tap "From Image" | Image picker opens |
| Enter URL | Paste URL, tap Extract | Shows loading, then preview |
| Paste Text | Paste recipe text, tap Extract | Shows loading, then preview |
| Extraction Error | Submit invalid input | Shows error message |
| Loading State | Submit valid input | Shows progress indicator |

#### ExtractedRecipePreviewPage

| Test | Steps | Expected |
|------|-------|----------|
| Display Extraction | Navigate from extract | Shows all extracted fields |
| Edit Fields | Modify title | Field updates |
| Low Confidence Warning | Confidence < 70% | Shows warning banner |
| Save Recipe | Tap Save | Creates recipe, navigates to detail |
| Discard | Tap Discard/Cancel | Returns to extract page |

#### RecipeDetailPage

| Test | Steps | Expected |
|------|-------|----------|
| Display Details | Navigate to recipe | Shows all recipe fields |
| Show Ingredients | Scroll to ingredients | Lists all ingredients |
| Show Steps | Scroll to steps | Lists numbered steps |
| Rate Recipe | Tap rating stars | Rating submits |
| Set Frequency | Select frequency option | Preference saved |
| Edit Recipe | Tap Edit button | Opens EditRecipePage |
| Add to Meal Plan | Tap "Add to Plan" | Shows plan picker |

#### MealPlansPage

| Test | Steps | Expected |
|------|-------|----------|
| List Plans | Navigate to page | Shows meal plans |
| Empty State | No plans exist | Shows create prompt |
| Create Plan | Tap Create | Opens CreateMealPlanPage |
| View Plan | Tap plan | Opens MealPlanDetailPage |

#### CreateMealPlanPage

| Test | Steps | Expected |
|------|-------|----------|
| Set Dates | Select start/end dates | Dates update |
| Invalid Range | End before start | Shows error |
| Save Plan | Enter name, dates, save | Plan created |

#### MealPlanDetailPage

| Test | Steps | Expected |
|------|-------|----------|
| View Days | Navigate to plan | Shows day grid/list |
| Add Recipe | Tap day, select recipe | Recipe assigned |
| Remove Recipe | Long press, delete | Recipe removed from day |
| Generate Shopping List | Tap shopping list | Shows consolidated list |

### 3.3 UI Test Infrastructure (Future)

```
tests/
└── MealPlanOrganizer.UITests/
    ├── MealPlanOrganizer.UITests.csproj
    ├── AppInitializer.cs
    ├── Pages/
    │   ├── LoginPageObject.cs
    │   ├── MainPageObject.cs
    │   ├── RecipeDetailPageObject.cs
    │   └── ...
    ├── Tests/
    │   ├── LoginTests.cs
    │   ├── RecipeFlowTests.cs
    │   ├── MealPlanFlowTests.cs
    │   └── ...
    └── TestData/
        ├── TestImages/
        └── TestRecipes.json
```

---

## Part 4: Test Data Management

### 4.1 Test Data Builders

#### RecipeBuilder

```csharp
public class RecipeBuilder
{
    private Recipe _recipe = new();
    
    public RecipeBuilder WithTitle(string title);
    public RecipeBuilder WithIngredients(params (string name, decimal qty, string unit)[] ingredients);
    public RecipeBuilder WithSteps(params string[] steps);
    public RecipeBuilder WithRating(Guid userId, int stars);
    public RecipeBuilder CreatedBy(Guid userId);
    public Recipe Build();
}

// Usage
var recipe = new RecipeBuilder()
    .WithTitle("Spaghetti Carbonara")
    .WithIngredients(
        ("Spaghetti", 400, "g"),
        ("Eggs", 4, "whole"),
        ("Parmesan", 100, "g"))
    .WithSteps(
        "Boil pasta in salted water",
        "Mix eggs with cheese",
        "Combine and toss")
    .WithRating(user1Id, 5)
    .WithRating(user2Id, 4)
    .Build();
```

#### MealPlanBuilder

```csharp
public class MealPlanBuilder
{
    public MealPlanBuilder WithName(string name);
    public MealPlanBuilder ForWeekStarting(DateTime startDate);
    public MealPlanBuilder WithRecipeOnDay(Recipe recipe, DateTime day);
    public MealPlanBuilder CreatedBy(Guid userId);
    public MealPlan Build();
}
```

### 4.2 Seed Data Sets

| Data Set | Description | Use Case |
|----------|-------------|----------|
| EmptyHousehold | User with no recipes/plans | Empty state testing |
| StarterHousehold | 5 recipes, 2 ratings each | Basic functionality |
| FullHousehold | 50 recipes, 200 ratings, 10 plans | Performance/pagination |
| RecommendationTestSet | Recipes with varied ratings/frequency | Algorithm testing |

---

## Part 5: CI/CD Integration

### 5.1 GitHub Actions Workflow

```yaml
# .github/workflows/tests.yml
name: Tests

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Run Unit Tests
        run: |
          dotnet test tests/MealPlanOrganizer.Functions.Tests \
            --filter "Category!=Integration" \
            --collect:"XPlat Code Coverage"
          dotnet test tests/MealPlanOrganizer.Mobile.Tests \
            --collect:"XPlat Code Coverage"
      - name: Upload Coverage
        uses: codecov/codecov-action@v4

  integration-tests:
    runs-on: ubuntu-latest
    needs: unit-tests
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Run Integration Tests
        run: |
          dotnet test tests/MealPlanOrganizer.Functions.Tests \
            --filter "Category=Integration"
```

### 5.2 Test Categories

| Category | Trigger | Duration |
|----------|---------|----------|
| Unit | Every PR | < 2 min |
| Integration | Merge to main | < 10 min |
| UI (future) | Nightly/Manual | < 30 min |

### 5.3 Coverage Requirements

| Project | Target | Critical Paths |
|---------|--------|----------------|
| Functions.Tests | 80% overall | 90% for Services/ |
| Mobile.Tests | 70% overall | 90% for Services/ |

---

## Part 6: Test Implementation Checklist

### Phase 1: Foundation (Sprint 1)
- [ ] Create test project structure
- [ ] Add NuGet packages (xUnit, FluentAssertions, Moq, Testcontainers)
- [ ] Implement test fixtures (Database, Azurite, Auth)
- [ ] Implement test data builders
- [ ] Set up GitHub Actions workflow

### Phase 2: Backend Unit Tests (Sprint 2)
- [ ] RecipeRecommendationServiceTests (8 tests)
- [ ] RecipeExtractionServiceTests (9 tests)
- [ ] JwtValidationServiceTests (7 tests)
- [ ] AuthenticationHelperTests (5 tests)
- [ ] BlobUrlServiceTests (5 tests)
- [ ] RecipeFunctionsTests (7 tests)
- [ ] RatingFunctionsTests (6 tests)
- [ ] MealPlanFunctionsTests (6 tests)

### Phase 3: Backend Integration Tests (Sprint 3)
- [ ] RecipeEndpointsTests (6 tests)
- [ ] ExtractionEndpointsTests (5 tests)
- [ ] RatingsEndpointsTests (5 tests)
- [ ] MealPlanEndpointsTests (5 tests)
- [ ] RecommendationsEndpointsTests (4 tests)
- [ ] AuthEndpointsTests (4 tests)

### Phase 4: Mobile Unit Tests (Sprint 4)
- [ ] RecipeServiceTests (16 tests)
- [ ] AuthServiceTests (8 tests)
- [ ] MockHttpMessageHandler implementation
- [ ] MockAuthService implementation

### Phase 5: UI Tests (Future - When UI Stabilizes)
- [ ] Set up UITest project
- [ ] Implement page objects
- [ ] Implement test scenarios
- [ ] Configure device cloud testing

---

## Appendix A: NuGet Packages

### Functions.Tests

```xml
<PackageReference Include="xunit" Version="2.9.*" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.*" />
<PackageReference Include="FluentAssertions" Version="8.*" />
<PackageReference Include="Moq" Version="4.20.*" />
<PackageReference Include="Testcontainers" Version="4.*" />
<PackageReference Include="Testcontainers.MsSql" Version="4.*" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
<PackageReference Include="coverlet.collector" Version="6.*" />
```

### Mobile.Tests

```xml
<PackageReference Include="xunit" Version="2.9.*" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.*" />
<PackageReference Include="FluentAssertions" Version="8.*" />
<PackageReference Include="Moq" Version="4.20.*" />
<PackageReference Include="RichardSzalay.MockHttp" Version="7.*" />
<PackageReference Include="coverlet.collector" Version="6.*" />
```

---

## Appendix B: Estimated Test Counts

| Area | Unit Tests | Integration Tests | Total |
|------|------------|-------------------|-------|
| Backend Services | 34 | - | 34 |
| Backend Functions | 19 | - | 19 |
| Backend Endpoints | - | 29 | 29 |
| Mobile Services | 24 | - | 24 |
| **Total** | **77** | **29** | **106** |

UI Tests (future): ~45 scenarios across 9 pages

---

## Appendix C: Related Documents

- [Architecture](architecture/ARCHITECTURE.md) - System architecture and components
- [Project Spec](../PROJECT_SPEC.md) - Requirements and features
- [Recipe Extraction Plan](genai/RECIPE_EXTRACTION_PLAN.md) - GenAI implementation details

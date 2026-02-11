using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Models;
using MealPlanOrganizer.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Functions
{
    /// <summary>
    /// Registers a user in the local database after successful Entra External ID authentication.
    /// Creates a new user record if one doesn't exist, or returns the existing user.
    /// </summary>
    public class RegisterUser
    {
        private readonly ILogger _logger;
        private readonly AppDbContext _db;
        private readonly AuthenticationHelper _authHelper;

        public RegisterUser(ILoggerFactory loggerFactory, AppDbContext db, AuthenticationHelper authHelper)
        {
            _logger = loggerFactory.CreateLogger<RegisterUser>();
            _db = db;
            _authHelper = authHelper;
        }

        [Function("RegisterUser")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "users/register")] HttpRequestData req)
        {
            _logger.LogInformation("Received RegisterUser request");

            // Authenticate the request
            var authResult = await _authHelper.AuthenticateAsync(req);
            if (!authResult.IsAuthenticated)
            {
                _logger.LogWarning("Authentication failed: {Error}", authResult.ErrorMessage);
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteStringAsync(authResult.ErrorMessage ?? "Unauthorized");
                return unauthorized;
            }

            var externalId = authResult.UserId;
            var email = authResult.UserEmail;
            var displayName = authResult.UserDisplayName ?? email;

            if (string.IsNullOrEmpty(externalId) || string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Missing required claims: externalId={ExternalId}, email={Email}", externalId, email);
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Missing required claims in token");
                return badRequest;
            }

            _logger.LogInformation("Registering user: ExternalId={ExternalId}, Email={Email}, DisplayName={DisplayName}",
                externalId, email, displayName);

            try
            {
                // Check if user already exists
                var existingUser = await _db.Users
                    .FirstOrDefaultAsync(u => u.ExternalIdObjectId == externalId);

                if (existingUser != null)
                {
                    _logger.LogInformation("User already exists: {UserId}", existingUser.Id);

                    // Update display name and email if changed
                    if (existingUser.DisplayName != displayName || existingUser.Email != email)
                    {
                        existingUser.DisplayName = displayName ?? existingUser.DisplayName;
                        existingUser.Email = email ?? existingUser.Email;
                        await _db.SaveChangesAsync();
                        _logger.LogInformation("Updated existing user info");
                    }

                    var existingResponse = await BuildUserResponse(existingUser);
                    var okResponse = req.CreateResponse(HttpStatusCode.OK);
                    okResponse.Headers.Add("Content-Type", "application/json");
                    await okResponse.WriteStringAsync(JsonSerializer.Serialize(existingResponse, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }));
                    return okResponse;
                }

                // Create new user
                var newUser = new User
                {
                    Id = Guid.NewGuid(),
                    ExternalIdObjectId = externalId,
                    Email = email,
                    DisplayName = displayName ?? email,
                    EmailConfirmed = true, // Entra External ID handles email confirmation
                    CreatedUtc = DateTime.UtcNow
                };

                _db.Users.Add(newUser);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Created new user: {UserId}", newUser.Id);

                var userResponse = await BuildUserResponse(newUser);
                var response = req.CreateResponse(HttpStatusCode.Created);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(userResponse, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Error registering user: {ex.Message}");
                return error;
            }
        }

        private async Task<UserResponse> BuildUserResponse(User user)
        {
            var response = new UserResponse
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                PhotoUrl = user.PhotoUrl,
                CreatedUtc = user.CreatedUtc
            };

            // Get household membership
            var membership = await _db.HouseholdMembers
                .Include(hm => hm.Household)
                    .ThenInclude(h => h!.Members)
                        .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(hm => hm.UserId == user.Id);

            if (membership?.Household != null)
            {
                response.Household = new HouseholdInfo
                {
                    Id = membership.Household.Id,
                    Name = membership.Household.Name,
                    Role = membership.Role.ToString(),
                    Members = membership.Household.Members
                        .Where(m => m.User != null)
                        .Select(m => new HouseholdMemberInfo
                        {
                            UserId = m.UserId,
                            DisplayName = m.User!.DisplayName,
                            Role = m.Role.ToString(),
                            JoinedUtc = m.JoinedUtc
                        }).ToList()
                };
            }

            return response;
        }
    }
}

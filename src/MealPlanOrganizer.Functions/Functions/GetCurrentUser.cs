using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Models;
using MealPlanOrganizer.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Functions
{
    /// <summary>
    /// Gets the current authenticated user's information including household membership.
    /// </summary>
    public class GetCurrentUser
    {
        private readonly ILogger _logger;
        private readonly AppDbContext _db;
        private readonly AuthenticationHelper _authHelper;

        public GetCurrentUser(ILoggerFactory loggerFactory, AppDbContext db, AuthenticationHelper authHelper)
        {
            _logger = loggerFactory.CreateLogger<GetCurrentUser>();
            _db = db;
            _authHelper = authHelper;
        }

        [Function("GetCurrentUser")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "users/me")] HttpRequestData req)
        {
            _logger.LogInformation("Received GetCurrentUser request");

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
            if (string.IsNullOrEmpty(externalId))
            {
                _logger.LogWarning("Missing user ID in token");
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Missing user ID in token");
                return badRequest;
            }

            try
            {
                // Find user by external ID
                var user = await _db.Users
                    .FirstOrDefaultAsync(u => u.ExternalIdObjectId == externalId);

                if (user == null)
                {
                    _logger.LogInformation("User not found in database: {ExternalId}", externalId);
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("User not registered. Call POST /users/register first.");
                    return notFound;
                }

                var userResponse = new UserResponse
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
                    userResponse.Household = new HouseholdInfo
                    {
                        Id = membership.Household.Id,
                        Name = membership.Household.Name,
                        Role = membership.Role.ToString(),
                        TimeZoneId = membership.Household.TimeZoneId,
                        Members = membership.Household.Members
                            .Where(m => m.User != null)
                            .Select(m => new HouseholdMemberInfo
                            {
                                UserId = m.UserId,
                                DisplayName = m.User!.DisplayName,
                                Role = m.Role.ToString(),
                                Weight = m.Weight,
                                JoinedUtc = m.JoinedUtc
                            }).ToList()
                    };
                }

                _logger.LogInformation("Returning user: {UserId}, Household: {HouseholdId}",
                    user.Id, membership?.HouseholdId);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(userResponse, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Error getting user: {ex.Message}");
                return error;
            }
        }
    }
}

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
    /// Creates a new household and assigns the calling user as the admin.
    /// </summary>
    public class CreateHousehold
    {
        private readonly ILogger _logger;
        private readonly AppDbContext _db;
        private readonly AuthenticationHelper _authHelper;

        public CreateHousehold(ILoggerFactory loggerFactory, AppDbContext db, AuthenticationHelper authHelper)
        {
            _logger = loggerFactory.CreateLogger<CreateHousehold>();
            _db = db;
            _authHelper = authHelper;
        }

        [Function("CreateHousehold")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "households")] HttpRequestData req)
        {
            _logger.LogInformation("Received CreateHousehold request");

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
                    _logger.LogWarning("User not found: {ExternalId}", externalId);
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("User not registered. Call POST /users/register first.");
                    return notFound;
                }

                // Check if user already has a household
                var existingMembership = await _db.HouseholdMembers
                    .AnyAsync(hm => hm.UserId == user.Id);

                if (existingMembership)
                {
                    _logger.LogWarning("User {UserId} already belongs to a household", user.Id);
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteStringAsync("User already belongs to a household");
                    return conflict;
                }

                // Parse request body
                var body = await req.ReadAsStringAsync();
                var request = JsonSerializer.Deserialize<CreateHouseholdRequest>(body ?? "{}", new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || string.IsNullOrWhiteSpace(request.Name))
                {
                    _logger.LogWarning("Invalid request: name is required");
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Household name is required");
                    return badRequest;
                }

                // Create household
                var household = new Household
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name.Trim(),
                    CreatedUtc = DateTime.UtcNow,
                    CreatedByUserId = user.Id
                };

                _db.Households.Add(household);

                // Add user as admin member
                var membership = new HouseholdMember
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    HouseholdId = household.Id,
                    Role = HouseholdRole.Admin,
                    JoinedUtc = DateTime.UtcNow
                };

                _db.HouseholdMembers.Add(membership);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Created household: {HouseholdId} with admin: {UserId}",
                    household.Id, user.Id);

                var householdResponse = new HouseholdResponse
                {
                    Id = household.Id,
                    Name = household.Name,
                    CreatedUtc = household.CreatedUtc,
                    CreatedByUserId = household.CreatedByUserId,
                    TimeZoneId = household.TimeZoneId
                };

                var response = req.CreateResponse(HttpStatusCode.Created);
                response.Headers.Add("Content-Type", "application/json");
                response.Headers.Add("Location", $"/api/households/{household.Id}");
                await response.WriteStringAsync(JsonSerializer.Serialize(householdResponse, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating household");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Error creating household: {ex.Message}");
                return error;
            }
        }
    }
}

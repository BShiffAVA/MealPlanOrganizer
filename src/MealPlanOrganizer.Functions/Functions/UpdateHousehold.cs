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
    /// Updates household settings (name, timezone). Admin only.
    /// </summary>
    public class UpdateHousehold
    {
        private readonly ILogger _logger;
        private readonly AppDbContext _db;
        private readonly AuthenticationHelper _authHelper;

        public UpdateHousehold(ILoggerFactory loggerFactory, AppDbContext db, AuthenticationHelper authHelper)
        {
            _logger = loggerFactory.CreateLogger<UpdateHousehold>();
            _db = db;
            _authHelper = authHelper;
        }

        [Function("UpdateHousehold")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "households/{householdId}")] HttpRequestData req,
            Guid householdId)
        {
            _logger.LogInformation("Received UpdateHousehold request for household {HouseholdId}", householdId);

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
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Missing user ID in token");
                return badRequest;
            }

            // Parse request body
            UpdateHouseholdRequest? updateRequest;
            try
            {
                var body = await req.ReadAsStringAsync();
                updateRequest = JsonSerializer.Deserialize<UpdateHouseholdRequest>(body ?? "{}", new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("Invalid JSON in request: {Error}", ex.Message);
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid JSON format");
                return badRequest;
            }

            if (updateRequest == null || (string.IsNullOrWhiteSpace(updateRequest.Name) && string.IsNullOrWhiteSpace(updateRequest.TimeZoneId)))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("At least one field (name or timeZoneId) must be provided");
                return badRequest;
            }

            // Validate timezone if provided
            if (!string.IsNullOrWhiteSpace(updateRequest.TimeZoneId))
            {
                // First try to find by IANA ID (works on Linux/macOS)
                // Then try Windows ID
                bool isValidTimezone = false;
                try
                {
                    TimeZoneInfo.FindSystemTimeZoneById(updateRequest.TimeZoneId);
                    isValidTimezone = true;
                }
                catch (TimeZoneNotFoundException)
                {
                    // Check if it's a known IANA ID
                    isValidTimezone = Array.Exists(UpdateHouseholdRequest.CommonTimeZones, 
                        tz => tz.Equals(updateRequest.TimeZoneId, StringComparison.OrdinalIgnoreCase));
                }
                
                if (!isValidTimezone)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync($"Invalid timezone: {updateRequest.TimeZoneId}. Use IANA timezone IDs like 'America/New_York'.");
                    return badRequest;
                }
            }

            try
            {
                // Find user by external ID
                var user = await _db.Users
                    .FirstOrDefaultAsync(u => u.ExternalIdObjectId == externalId);

                if (user == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("User not registered");
                    return notFound;
                }

                // Verify user is admin of this household
                var membership = await _db.HouseholdMembers
                    .Include(hm => hm.Household)
                    .FirstOrDefaultAsync(hm => hm.UserId == user.Id && hm.HouseholdId == householdId);

                if (membership == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Household not found or you are not a member");
                    return notFound;
                }

                if (membership.Role != HouseholdRole.Admin)
                {
                    _logger.LogWarning("User {UserId} attempted to update household {HouseholdId} without admin rights", 
                        user.Id, householdId);
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("Only admins can update household settings");
                    return forbidden;
                }

                var household = membership.Household!;
                
                // Update fields
                if (!string.IsNullOrWhiteSpace(updateRequest.Name))
                {
                    household.Name = updateRequest.Name.Trim();
                }
                
                if (!string.IsNullOrWhiteSpace(updateRequest.TimeZoneId))
                {
                    household.TimeZoneId = updateRequest.TimeZoneId;
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation("Updated household {HouseholdId}: Name={Name}, TimeZoneId={TimeZoneId}",
                    householdId, household.Name, household.TimeZoneId);

                var householdResponse = new HouseholdResponse
                {
                    Id = household.Id,
                    Name = household.Name,
                    CreatedUtc = household.CreatedUtc,
                    CreatedByUserId = household.CreatedByUserId,
                    TimeZoneId = household.TimeZoneId
                };

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(householdResponse, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating household {HouseholdId}", householdId);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Error updating household: {ex.Message}");
                return error;
            }
        }
    }
}

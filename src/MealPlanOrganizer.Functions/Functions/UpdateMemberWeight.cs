using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Functions
{
    /// <summary>
    /// Updates the weight (1-5) for a household member's ratings and preferences (admin only).
    /// </summary>
    public class UpdateMemberWeight
    {
        private readonly ILogger _logger;
        private readonly AppDbContext _db;
        private readonly AuthenticationHelper _authHelper;

        public UpdateMemberWeight(ILoggerFactory loggerFactory, AppDbContext db, AuthenticationHelper authHelper)
        {
            _logger = loggerFactory.CreateLogger<UpdateMemberWeight>();
            _db = db;
            _authHelper = authHelper;
        }

        [Function("UpdateMemberWeight")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "households/{householdId}/members/{memberId}/weight")] HttpRequestData req,
            Guid householdId,
            Guid memberId)
        {
            _logger.LogInformation("Received UpdateMemberWeight request for household {HouseholdId}, member {MemberId}", householdId, memberId);

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
            UpdateWeightRequest? weightRequest;
            try
            {
                var requestBody = await req.ReadAsStringAsync();
                if (string.IsNullOrEmpty(requestBody))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Request body is required");
                    return badRequest;
                }

                weightRequest = JsonSerializer.Deserialize<UpdateWeightRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (weightRequest == null)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Invalid request body");
                    return badRequest;
                }
            }
            catch (JsonException)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid JSON in request body");
                return badRequest;
            }

            // Validate weight value
            if (weightRequest.Weight < 1 || weightRequest.Weight > 5)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Weight must be between 1 and 5");
                return badRequest;
            }

            try
            {
                // Find requesting user by external ID
                var requestingUser = await _db.Users
                    .FirstOrDefaultAsync(u => u.ExternalIdObjectId == externalId);

                if (requestingUser == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("User not registered");
                    return notFound;
                }

                // Check if household exists
                var household = await _db.Households.FindAsync(householdId);
                if (household == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Household not found");
                    return notFound;
                }

                // Verify requesting user is an admin of the household
                var requestingMembership = await _db.HouseholdMembers
                    .FirstOrDefaultAsync(hm => hm.UserId == requestingUser.Id && hm.HouseholdId == householdId);

                if (requestingMembership == null)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("You are not a member of this household");
                    return forbidden;
                }

                if (requestingMembership.Role != HouseholdRole.Admin)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("Only household admins can update member weights");
                    return forbidden;
                }

                // Find the member to update
                var memberToUpdate = await _db.HouseholdMembers
                    .Include(hm => hm.User)
                    .FirstOrDefaultAsync(hm => hm.UserId == memberId && hm.HouseholdId == householdId);

                if (memberToUpdate == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Member not found in this household");
                    return notFound;
                }

                // Update the weight
                memberToUpdate.Weight = weightRequest.Weight;
                await _db.SaveChangesAsync();

                _logger.LogInformation("Updated weight for member {MemberId} to {Weight} in household {HouseholdId} by admin {AdminId}",
                    memberId, weightRequest.Weight, householdId, requestingUser.Id);

                // Return updated member info
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                
                var memberResponse = new
                {
                    UserId = memberToUpdate.UserId,
                    DisplayName = memberToUpdate.User?.DisplayName ?? "",
                    Role = memberToUpdate.Role.ToString(),
                    Weight = memberToUpdate.Weight,
                    JoinedUtc = memberToUpdate.JoinedUtc
                };
                
                await response.WriteStringAsync(JsonSerializer.Serialize(memberResponse, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating member weight");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync("An error occurred");
                return error;
            }
        }
    }

    /// <summary>
    /// Request model for updating member weight.
    /// </summary>
    public class UpdateWeightRequest
    {
        /// <summary>
        /// Weight value from 1 to 5.
        /// Higher values mean the member's preferences count more.
        /// </summary>
        public int Weight { get; set; }
    }
}

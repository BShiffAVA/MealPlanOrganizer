using System;
using System.Net;
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
    /// Allows an authenticated user to join a household using an invite code.
    /// </summary>
    public class JoinHousehold
    {
        private readonly ILogger _logger;
        private readonly AppDbContext _db;
        private readonly AuthenticationHelper _authHelper;

        public JoinHousehold(ILoggerFactory loggerFactory, AppDbContext db, AuthenticationHelper authHelper)
        {
            _logger = loggerFactory.CreateLogger<JoinHousehold>();
            _db = db;
            _authHelper = authHelper;
        }

        [Function("JoinHousehold")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "households/join")] HttpRequestData req)
        {
            _logger.LogInformation("Received JoinHousehold request");

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

            JoinHouseholdRequest? request;
            try
            {
                request = await req.ReadFromJsonAsync<JoinHouseholdRequest>();
            }
            catch
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid request body");
                return badRequest;
            }

            if (request == null || string.IsNullOrWhiteSpace(request.InviteCode))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invite code is required");
                return badRequest;
            }

            if (request.InviteCode.Length != 8)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid invite code format. Codes are 8 characters.");
                return badRequest;
            }

            try
            {
                // Find user by external ID
                var user = await _db.Users
                    .FirstOrDefaultAsync(u => u.ExternalIdObjectId == externalId);

                if (user == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("User not registered. Please complete registration first.");
                    return notFound;
                }

                // Find the invite code
                var inviteCode = await _db.InviteCodes
                    .Include(ic => ic.Household)
                    .FirstOrDefaultAsync(ic => ic.Code == request.InviteCode.ToUpperInvariant());

                if (inviteCode == null)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Invite code not found");
                    return badRequest;
                }

                // Validate the code
                if (inviteCode.IsRevoked)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("This invite code has been revoked");
                    return badRequest;
                }

                if (inviteCode.UsedByUserId.HasValue)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("This invite code has already been used");
                    return badRequest;
                }

                if (inviteCode.ExpiresUtc <= DateTime.UtcNow)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("This invite code has expired");
                    return badRequest;
                }

                // Check if user is already a member of this household
                var existingMembership = await _db.HouseholdMembers
                    .FirstOrDefaultAsync(hm => hm.UserId == user.Id && hm.HouseholdId == inviteCode.HouseholdId);

                if (existingMembership != null)
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteStringAsync("You are already a member of this household");
                    return conflict;
                }

                // Add user to household as Member
                var membership = new HouseholdMember
                {
                    UserId = user.Id,
                    HouseholdId = inviteCode.HouseholdId,
                    Role = HouseholdRole.Member,
                    JoinedUtc = DateTime.UtcNow
                };

                _db.HouseholdMembers.Add(membership);

                // Mark the invite code as used
                inviteCode.UsedByUserId = user.Id;
                inviteCode.UsedUtc = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                _logger.LogInformation("User {UserId} joined household {HouseholdId} using invite code {Code}", 
                    user.Id, inviteCode.HouseholdId, inviteCode.Code);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    Success = true,
                    HouseholdId = inviteCode.HouseholdId,
                    HouseholdName = inviteCode.Household?.Name,
                    Role = HouseholdRole.Member.ToString()
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining household");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync("An error occurred");
                return error;
            }
        }
    }
}

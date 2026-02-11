using System;
using System.Net;
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
    /// Revokes an invite code (admin only).
    /// </summary>
    public class RevokeInviteCode
    {
        private readonly ILogger _logger;
        private readonly AppDbContext _db;
        private readonly AuthenticationHelper _authHelper;

        public RevokeInviteCode(ILoggerFactory loggerFactory, AppDbContext db, AuthenticationHelper authHelper)
        {
            _logger = loggerFactory.CreateLogger<RevokeInviteCode>();
            _db = db;
            _authHelper = authHelper;
        }

        [Function("RevokeInviteCode")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "households/invites/{code}")] HttpRequestData req,
            string code)
        {
            _logger.LogInformation("Received RevokeInviteCode request for code {Code}", code);

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

            if (string.IsNullOrWhiteSpace(code) || code.Length != 8)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid invite code format");
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
                    await notFound.WriteStringAsync("User not registered");
                    return notFound;
                }

                // Find the invite code
                var inviteCode = await _db.InviteCodes
                    .Include(ic => ic.Household)
                    .FirstOrDefaultAsync(ic => ic.Code == code.ToUpperInvariant());

                if (inviteCode == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Invite code not found");
                    return notFound;
                }

                // Verify user is an admin of the household that owns this code
                var membership = await _db.HouseholdMembers
                    .FirstOrDefaultAsync(hm => hm.UserId == user.Id && hm.HouseholdId == inviteCode.HouseholdId);

                if (membership == null)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("You are not a member of the household that owns this code");
                    return forbidden;
                }

                if (membership.Role != HouseholdRole.Admin)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("Only household admins can revoke invite codes");
                    return forbidden;
                }

                // Check if already revoked
                if (inviteCode.IsRevoked)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Invite code is already revoked");
                    return badRequest;
                }

                // Check if already used
                if (inviteCode.UsedByUserId.HasValue)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Cannot revoke a code that has already been used");
                    return badRequest;
                }

                // Revoke the code
                inviteCode.IsRevoked = true;
                await _db.SaveChangesAsync();

                _logger.LogInformation("Revoked invite code {Code} for household {HouseholdId}", code, inviteCode.HouseholdId);

                var response = req.CreateResponse(HttpStatusCode.NoContent);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking invite code");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync("An error occurred");
                return error;
            }
        }
    }
}

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
    /// Removes a member from a household (admin only).
    /// Admins cannot remove themselves if they are the only admin.
    /// </summary>
    public class RemoveMember
    {
        private readonly ILogger _logger;
        private readonly AppDbContext _db;
        private readonly AuthenticationHelper _authHelper;

        public RemoveMember(ILoggerFactory loggerFactory, AppDbContext db, AuthenticationHelper authHelper)
        {
            _logger = loggerFactory.CreateLogger<RemoveMember>();
            _db = db;
            _authHelper = authHelper;
        }

        [Function("RemoveMember")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "households/{householdId}/members/{memberId}")] HttpRequestData req,
            Guid householdId,
            Guid memberId)
        {
            _logger.LogInformation("Received RemoveMember request for household {HouseholdId}, member {MemberId}", householdId, memberId);

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
                    await forbidden.WriteStringAsync("Only household admins can remove members");
                    return forbidden;
                }

                // Find the member to remove
                var memberToRemove = await _db.HouseholdMembers
                    .Include(hm => hm.User)
                    .FirstOrDefaultAsync(hm => hm.UserId == memberId && hm.HouseholdId == householdId);

                if (memberToRemove == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Member not found in this household");
                    return notFound;
                }

                // Check if trying to remove self
                if (memberToRemove.UserId == requestingUser.Id)
                {
                    // Count admins in the household
                    var adminCount = await _db.HouseholdMembers
                        .CountAsync(hm => hm.HouseholdId == householdId && hm.Role == HouseholdRole.Admin);

                    if (adminCount == 1)
                    {
                        var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badRequest.WriteStringAsync("Cannot remove yourself when you are the only admin. Transfer admin role first or delete the household.");
                        return badRequest;
                    }
                }

                // Remove the member
                _db.HouseholdMembers.Remove(memberToRemove);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Removed member {MemberId} from household {HouseholdId} by admin {AdminId}",
                    memberId, householdId, requestingUser.Id);

                var response = req.CreateResponse(HttpStatusCode.NoContent);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing member from household");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync("An error occurred");
                return error;
            }
        }
    }
}

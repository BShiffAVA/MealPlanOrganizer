using System;
using System.Linq;
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
    /// Lists invite codes for a household (admin only).
    /// </summary>
    public class GetInviteCodes
    {
        private readonly ILogger _logger;
        private readonly AppDbContext _db;
        private readonly AuthenticationHelper _authHelper;

        public GetInviteCodes(ILoggerFactory loggerFactory, AppDbContext db, AuthenticationHelper authHelper)
        {
            _logger = loggerFactory.CreateLogger<GetInviteCodes>();
            _db = db;
            _authHelper = authHelper;
        }

        [Function("GetInviteCodes")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "households/{householdId}/invites")] HttpRequestData req,
            Guid householdId)
        {
            _logger.LogInformation("Received GetInviteCodes request for household {HouseholdId}", householdId);

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
                // Find user by external ID
                var user = await _db.Users
                    .FirstOrDefaultAsync(u => u.ExternalIdObjectId == externalId);

                if (user == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("User not registered");
                    return notFound;
                }

                // Verify the household exists
                var household = await _db.Households.FindAsync(householdId);
                if (household == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Household not found");
                    return notFound;
                }

                // Verify user is an admin of this household
                var membership = await _db.HouseholdMembers
                    .FirstOrDefaultAsync(hm => hm.UserId == user.Id && hm.HouseholdId == householdId);

                if (membership == null)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("You are not a member of this household");
                    return forbidden;
                }

                if (membership.Role != HouseholdRole.Admin)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("Only household admins can view invite codes");
                    return forbidden;
                }

                // Query invite codes
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var includeUsed = query["includeUsed"]?.ToLowerInvariant() == "true";
                var includeRevoked = query["includeRevoked"]?.ToLowerInvariant() == "true";
                var includeExpired = query["includeExpired"]?.ToLowerInvariant() == "true";

                var codesQuery = _db.InviteCodes
                    .Include(ic => ic.UsedByUser)
                    .Where(ic => ic.HouseholdId == householdId);

                if (!includeUsed)
                {
                    codesQuery = codesQuery.Where(ic => !ic.UsedByUserId.HasValue);
                }

                if (!includeRevoked)
                {
                    codesQuery = codesQuery.Where(ic => !ic.IsRevoked);
                }

                if (!includeExpired)
                {
                    codesQuery = codesQuery.Where(ic => ic.ExpiresUtc > DateTime.UtcNow);
                }

                var codes = await codesQuery
                    .OrderByDescending(ic => ic.CreatedUtc)
                    .Select(ic => new InviteCodeResponse
                    {
                        Id = ic.Id,
                        Code = ic.Code,
                        HouseholdName = household.Name,
                        HouseholdId = household.Id,
                        ExpiresUtc = ic.ExpiresUtc,
                        CreatedUtc = ic.CreatedUtc,
                        IsUsed = ic.UsedByUserId.HasValue,
                        UsedByEmail = ic.UsedByUser != null ? ic.UsedByUser.Email : null,
                        UsedUtc = ic.UsedUtc,
                        IsRevoked = ic.IsRevoked,
                        IsValid = !ic.IsRevoked && !ic.UsedByUserId.HasValue && ic.ExpiresUtc > DateTime.UtcNow
                    })
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} invite codes for household {HouseholdId}", codes.Count, householdId);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(codes);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting invite codes");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync("An error occurred");
                return error;
            }
        }
    }
}

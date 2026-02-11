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
    /// Generates a new invite code for a household (admin only).
    /// </summary>
    public class GenerateInviteCode
    {
        private readonly ILogger _logger;
        private readonly AppDbContext _db;
        private readonly AuthenticationHelper _authHelper;

        public GenerateInviteCode(ILoggerFactory loggerFactory, AppDbContext db, AuthenticationHelper authHelper)
        {
            _logger = loggerFactory.CreateLogger<GenerateInviteCode>();
            _db = db;
            _authHelper = authHelper;
        }

        [Function("GenerateInviteCode")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "households/{householdId}/invites")] HttpRequestData req,
            Guid householdId)
        {
            _logger.LogInformation("Received GenerateInviteCode request for household {HouseholdId}", householdId);

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
                    await forbidden.WriteStringAsync("Only household admins can generate invite codes");
                    return forbidden;
                }

                // Check limit: max 10 active invite codes per household
                var activeCodeCount = await _db.InviteCodes
                    .CountAsync(ic => ic.HouseholdId == householdId 
                        && !ic.IsRevoked 
                        && !ic.UsedByUserId.HasValue 
                        && ic.ExpiresUtc > DateTime.UtcNow);

                if (activeCodeCount >= 10)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Maximum of 10 active invite codes per household. Please revoke unused codes.");
                    return badRequest;
                }

                // Generate unique code
                string code;
                int attempts = 0;
                do
                {
                    code = InviteCode.GenerateCode();
                    attempts++;
                    if (attempts > 10)
                    {
                        _logger.LogError("Failed to generate unique invite code after 10 attempts");
                        var serverError = req.CreateResponse(HttpStatusCode.InternalServerError);
                        await serverError.WriteStringAsync("Failed to generate invite code");
                        return serverError;
                    }
                } while (await _db.InviteCodes.AnyAsync(ic => ic.Code == code));

                // Create invite code (30 day expiry)
                var inviteCode = new InviteCode
                {
                    Code = code,
                    HouseholdId = householdId,
                    CreatedByUserId = user.Id,
                    ExpiresUtc = DateTime.UtcNow.AddDays(30)
                };

                _db.InviteCodes.Add(inviteCode);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Generated invite code {Code} for household {HouseholdId}", code, householdId);

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(new InviteCodeResponse
                {
                    Id = inviteCode.Id,
                    Code = inviteCode.Code,
                    HouseholdName = household.Name,
                    HouseholdId = household.Id,
                    ExpiresUtc = inviteCode.ExpiresUtc,
                    CreatedUtc = inviteCode.CreatedUtc,
                    IsUsed = false,
                    IsRevoked = false,
                    IsValid = true
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invite code");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync("An error occurred");
                return error;
            }
        }
    }
}

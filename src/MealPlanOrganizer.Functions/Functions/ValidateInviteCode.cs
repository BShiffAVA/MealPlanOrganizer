using System;
using System.Net;
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
    /// Validates an invite code and returns household info if valid.
    /// This endpoint requires authentication but allows any authenticated user to validate a code.
    /// </summary>
    public class ValidateInviteCode
    {
        private readonly ILogger _logger;
        private readonly AppDbContext _db;
        private readonly AuthenticationHelper _authHelper;

        public ValidateInviteCode(ILoggerFactory loggerFactory, AppDbContext db, AuthenticationHelper authHelper)
        {
            _logger = loggerFactory.CreateLogger<ValidateInviteCode>();
            _db = db;
            _authHelper = authHelper;
        }

        [Function("ValidateInviteCode")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "households/invites/{code}/validate")] HttpRequestData req,
            string code)
        {
            _logger.LogInformation("Received ValidateInviteCode request for code {Code}", code);

            // Authenticate the request
            var authResult = await _authHelper.AuthenticateAsync(req);
            if (!authResult.IsAuthenticated)
            {
                _logger.LogWarning("Authentication failed: {Error}", authResult.ErrorMessage);
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteStringAsync(authResult.ErrorMessage ?? "Unauthorized");
                return unauthorized;
            }

            if (string.IsNullOrWhiteSpace(code) || code.Length != 8)
            {
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new ValidateInviteCodeResponse
                {
                    IsValid = false,
                    ErrorMessage = "Invalid code format. Codes are 8 characters."
                });
                return response;
            }

            try
            {
                // Find the invite code
                var inviteCode = await _db.InviteCodes
                    .Include(ic => ic.Household)
                    .FirstOrDefaultAsync(ic => ic.Code == code.ToUpperInvariant());

                if (inviteCode == null)
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new ValidateInviteCodeResponse
                    {
                        IsValid = false,
                        ErrorMessage = "Invite code not found"
                    });
                    return response;
                }

                if (inviteCode.IsRevoked)
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new ValidateInviteCodeResponse
                    {
                        IsValid = false,
                        ErrorMessage = "This invite code has been revoked"
                    });
                    return response;
                }

                if (inviteCode.UsedByUserId.HasValue)
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new ValidateInviteCodeResponse
                    {
                        IsValid = false,
                        ErrorMessage = "This invite code has already been used"
                    });
                    return response;
                }

                if (inviteCode.ExpiresUtc <= DateTime.UtcNow)
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new ValidateInviteCodeResponse
                    {
                        IsValid = false,
                        ErrorMessage = "This invite code has expired"
                    });
                    return response;
                }

                // Code is valid
                _logger.LogInformation("Invite code {Code} is valid for household {HouseholdName}", 
                    code, inviteCode.Household?.Name);

                var successResponse = req.CreateResponse(HttpStatusCode.OK);
                await successResponse.WriteAsJsonAsync(new ValidateInviteCodeResponse
                {
                    IsValid = true,
                    HouseholdName = inviteCode.Household?.Name,
                    ExpiresUtc = inviteCode.ExpiresUtc
                });
                return successResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating invite code");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync("An error occurred");
                return error;
            }
        }
    }
}

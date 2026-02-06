using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Models;
using MealPlanOrganizer.Functions.Services;

namespace MealPlanOrganizer.Functions.Functions;

public class CreateMealPlan
{
    private readonly ILogger<CreateMealPlan> _logger;
    private readonly AppDbContext _context;
    private readonly AuthenticationHelper _authHelper;

    public CreateMealPlan(
        ILogger<CreateMealPlan> logger,
        AppDbContext context,
        AuthenticationHelper authHelper)
    {
        _logger = logger;
        _context = context;
        _authHelper = authHelper;
    }

    [Function("CreateMealPlan")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mealplans")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Creating new meal plan");

        // Authenticate the request
        var authResult = await _authHelper.AuthenticateAsync(req);
        if (!authResult.IsAuthenticated)
        {
            _logger.LogWarning("Unauthorized meal plan creation: {Error}", authResult.ErrorMessage);
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteAsJsonAsync(new { message = "Authentication required" });
            return unauthorizedResponse;
        }

        // Get user identifier (display name or email or userId)
        var createdBy = authResult.UserDisplayName 
                     ?? authResult.UserEmail 
                     ?? authResult.UserId 
                     ?? "anonymous";

        // Parse request body
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        CreateMealPlanRequest? request = null;

        try
        {
            var requestBody = await req.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(requestBody))
            {
                request = JsonSerializer.Deserialize<CreateMealPlanRequest>(requestBody, options);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse request body");
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { message = "Invalid request body" });
            return badResponse;
        }

        // Validate request
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { message = "Name is required" });
            return badResponse;
        }

        if (request.StartDate == default)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { message = "StartDate is required" });
            return badResponse;
        }

        if (request.EndDate == default)
        {
            // Default to 6 days after start (7 day week)
            request.EndDate = request.StartDate.AddDays(6);
        }

        if (request.EndDate < request.StartDate)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { message = "EndDate must be after StartDate" });
            return badResponse;
        }

        // Create the meal plan
        var mealPlan = new MealPlan
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            CreatedBy = createdBy,
            CreatedUtc = DateTime.UtcNow,
            Status = "Draft"
        };

        _context.MealPlans.Add(mealPlan);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created meal plan {MealPlanId} '{Name}' for {StartDate} to {EndDate}",
            mealPlan.Id, mealPlan.Name, mealPlan.StartDate, mealPlan.EndDate);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(new
        {
            id = mealPlan.Id,
            name = mealPlan.Name,
            startDate = mealPlan.StartDate.ToString("yyyy-MM-dd"),
            endDate = mealPlan.EndDate.ToString("yyyy-MM-dd"),
            createdBy = mealPlan.CreatedBy,
            status = mealPlan.Status,
            createdUtc = mealPlan.CreatedUtc
        });

        return response;
    }
}

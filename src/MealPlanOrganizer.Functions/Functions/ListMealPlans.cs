using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Services;

namespace MealPlanOrganizer.Functions.Functions;

public class ListMealPlans
{
    private readonly ILogger<ListMealPlans> _logger;
    private readonly AppDbContext _context;
    private readonly AuthenticationHelper _authHelper;

    public ListMealPlans(
        ILogger<ListMealPlans> logger,
        AppDbContext context,
        AuthenticationHelper authHelper)
    {
        _logger = logger;
        _context = context;
        _authHelper = authHelper;
    }

    [Function("ListMealPlans")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "mealplans")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Listing meal plans");

        // Authenticate the request
        var authResult = await _authHelper.AuthenticateAsync(req);
        if (!authResult.IsAuthenticated)
        {
            _logger.LogWarning("Unauthorized list meal plans: {Error}", authResult.ErrorMessage);
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteAsJsonAsync(new { message = "Authentication required" });
            return unauthorizedResponse;
        }

        // Get all meal plans with recipe counts
        var mealPlans = await _context.MealPlans
            .Include(mp => mp.Recipes)
            .OrderByDescending(mp => mp.StartDate)
            .ToListAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            totalMealPlans = mealPlans.Count,
            mealPlans = mealPlans.Select(mp => new
            {
                id = mp.Id,
                name = mp.Name,
                startDate = mp.StartDate.ToString("yyyy-MM-dd"),
                endDate = mp.EndDate.ToString("yyyy-MM-dd"),
                createdBy = mp.CreatedBy,
                status = mp.Status,
                createdUtc = mp.CreatedUtc,
                totalDays = (mp.EndDate - mp.StartDate).Days + 1,
                recipesAssigned = mp.Recipes.Count
            })
        });

        return response;
    }
}

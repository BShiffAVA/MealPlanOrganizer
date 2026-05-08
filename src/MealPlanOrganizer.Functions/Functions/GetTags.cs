using System.Net;
using System.Threading.Tasks;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Functions;

public class GetTags
{
    private readonly ILogger<GetTags> _logger;
    private readonly AppDbContext _context;
    private readonly AuthenticationHelper _authHelper;

    public GetTags(ILogger<GetTags> logger, AppDbContext context, AuthenticationHelper authHelper)
    {
        _logger = logger;
        _context = context;
        _authHelper = authHelper;
    }

    [Function("GetTags")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "tags")] HttpRequestData req)
    {
        var authResult = await _authHelper.AuthenticateAsync(req);
        if (!authResult.IsAuthenticated)
        {
            var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauth.WriteStringAsync(authResult.ErrorMessage ?? "Unauthorized");
            return unauth;
        }

        var search = req.Query["search"] ?? string.Empty;
        var normalizedSearch = TagHelper.Normalize(search) ?? string.Empty;

        _logger.LogInformation("GetTags request with search='{Search}'", normalizedSearch);

        // Return matching tags sorted by usage count (descending), then alphabetically
        var query = _context.RecipeTags
            .Select(t => new
            {
                t.Id,
                t.Name,
                UsageCount = t.Assignments.Count
            });

        if (!string.IsNullOrEmpty(normalizedSearch))
        {
            query = query.Where(t => t.Name.StartsWith(normalizedSearch));
        }

        var tags = await query
            .OrderByDescending(t => t.UsageCount)
            .ThenBy(t => t.Name)
            .Take(10)
            .Select(t => t.Name)
            .ToListAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(tags);
        return response;
    }
}

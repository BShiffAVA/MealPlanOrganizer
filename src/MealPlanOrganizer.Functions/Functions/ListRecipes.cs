using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Functions
{
    public class ListRecipes
    {
        private readonly ILogger _logger;
        private readonly AppDbContext _db;
        private readonly IBlobUrlService _blobUrlService;

        public ListRecipes(ILoggerFactory loggerFactory, AppDbContext db, IBlobUrlService blobUrlService)
        {
            _logger = loggerFactory.CreateLogger<ListRecipes>();
            _db = db;
            _blobUrlService = blobUrlService;
        }

        [Function("ListRecipes")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes")] HttpRequestData req)
        {
            _logger.LogInformation("Listing recipes");

            var query = _db.Recipes
                .Include(r => r.Ratings)
                .Include(r => r.TagAssignments).ThenInclude(ta => ta.Tag)
                .OrderByDescending(r => r.CreatedUtc)
                .AsQueryable();

            var tagFilter = req.Query["tag"];
            if (!string.IsNullOrWhiteSpace(tagFilter))
            {
                var normalizedTag = TagHelper.Normalize(tagFilter);
                if (normalizedTag != null)
                {
                    _logger.LogInformation("Filtering by tag: {Tag}", normalizedTag);
                    query = query.Where(r => r.TagAssignments.Any(ta => ta.Tag.Name == normalizedTag));
                }
            }

            var recipes = await query.Take(50).ToListAsync();

            var normalizedRecipes = new List<object>(recipes.Count);
            foreach (var r in recipes)
            {
                var normalizedUrl = await _blobUrlService.NormalizeImageUrlAsync(r.ImageUrl);
                normalizedRecipes.Add(new
                {
                    id = r.Id,
                    title = r.Title,
                    description = r.Description,
                    cuisineType = r.CuisineType,
                    prepTimeMinutes = r.PrepTimeMinutes,
                    averageRating = r.Ratings.Count > 0 ? r.Ratings.Average(rt => rt.Rating) : 0.0,
                    createdBy = r.CreatedBy,
                    createdUtc = r.CreatedUtc,
                    imageUrl = normalizedUrl,
                    tags = r.TagAssignments.Select(ta => ta.Tag.Name).OrderBy(n => n).ToList()
                });
            }

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteStringAsync(JsonSerializer.Serialize(normalizedRecipes));
            return res;
        }
    }
}


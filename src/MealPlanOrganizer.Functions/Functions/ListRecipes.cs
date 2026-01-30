using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using MealPlanOrganizer.Functions.Data;
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

        public ListRecipes(ILoggerFactory loggerFactory, AppDbContext db)
        {
            _logger = loggerFactory.CreateLogger<ListRecipes>();
            _db = db;
        }

        [Function("ListRecipes")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes")] HttpRequestData req)
        {
            _logger.LogInformation("Listing recipes");

            var recipes = await _db.Recipes
                .Include(r => r.Ratings)
                .OrderByDescending(r => r.CreatedUtc)
                .Select(r => new
                {
                    id = r.Id,
                    title = r.Title,
                    description = r.Description,
                    cuisineType = r.CuisineType,
                    prepTimeMinutes = r.PrepTimeMinutes,
                    averageRating = r.Ratings.Count > 0 ? r.Ratings.Average(rt => rt.Rating) : 0.0,
                    createdBy = r.CreatedBy,
                    createdUtc = r.CreatedUtc
                })
                .Take(50)
                .ToListAsync();

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteStringAsync(JsonSerializer.Serialize(recipes));
            return res;
        }
    }
}


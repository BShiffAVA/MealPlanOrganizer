using MealPlanOrganizer.Functions.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MealPlanOrganizer.Functions.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // Prefer env var provided by tooling; fallback to local SQLEXPRESS
            var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Sql")
                     ?? "Server=.\\SQLEXPRESS;Database=MealPlanOrganizer;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False";

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(cs);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}

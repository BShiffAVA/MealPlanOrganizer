using MealPlanOrganizer.Functions.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealPlanOrganizer.Functions.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Recipe> Recipes => Set<Recipe>();
        public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
        public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Recipe>(b =>
            {
                b.ToTable("Recipes");
                b.HasKey(x => x.Id);
                b.Property(x => x.Title).IsRequired().HasMaxLength(200);
                b.Property(x => x.Description).HasMaxLength(2000);
                b.Property(x => x.CuisineType).HasMaxLength(100);
                b.Property(x => x.ImageUrl).HasMaxLength(500);
                b.Property(x => x.CreatedBy).HasMaxLength(200);
                b.Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
            });

            modelBuilder.Entity<RecipeIngredient>(b =>
            {
                b.ToTable("RecipeIngredients");
                b.HasKey(x => x.Id);
                b.Property(x => x.Name).IsRequired().HasMaxLength(200);
                b.Property(x => x.Quantity).HasMaxLength(100);
                b.HasOne(x => x.Recipe)
                    .WithMany(x => x.Ingredients)
                    .HasForeignKey(x => x.RecipeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<RecipeStep>(b =>
            {
                b.ToTable("RecipeSteps");
                b.HasKey(x => x.Id);
                b.Property(x => x.Instruction).IsRequired().HasMaxLength(2000);
                b.HasOne(x => x.Recipe)
                    .WithMany(x => x.Steps)
                    .HasForeignKey(x => x.RecipeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed sample data via HasData
            var recipeId = Guid.Parse("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01");
            var ing1Id = Guid.Parse("3d3d5d7f-9a0c-4b3e-9f5a-2a9f7b6c1a11");
            var ing2Id = Guid.Parse("7a2b6e9d-1c4f-4b82-8b8a-9b3c7a1d2f22");
            var step1Id = Guid.Parse("c9a1d2e3-4f5b-6c7d-8e9f-0a1b2c3d4e55");
            var step2Id = Guid.Parse("f1e2d3c4-b5a6-7890-1a2b-3c4d5e6f7a88");

            modelBuilder.Entity<Recipe>().HasData(new Recipe
            {
                Id = recipeId,
                Title = "Sample Lasagna",
                Description = "Classic lasagna with noodles and sauce",
                CreatedUtc = DateTime.UtcNow
            });

            modelBuilder.Entity<RecipeIngredient>().HasData(
                new RecipeIngredient { Id = ing1Id, RecipeId = recipeId, Name = "noodles", Quantity = "12 pieces" },
                new RecipeIngredient { Id = ing2Id, RecipeId = recipeId, Name = "sauce", Quantity = "2 cups" }
            );

            modelBuilder.Entity<RecipeStep>().HasData(
                new RecipeStep { Id = step1Id, RecipeId = recipeId, StepNumber = 1, Instruction = "Boil noodles" },
                new RecipeStep { Id = step2Id, RecipeId = recipeId, StepNumber = 2, Instruction = "Bake with sauce" }
            );
        }
    }
}

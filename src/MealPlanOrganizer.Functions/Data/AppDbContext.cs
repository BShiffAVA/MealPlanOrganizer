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
        }
    }
}

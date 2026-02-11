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
        public DbSet<RecipeRating> RecipeRatings => Set<RecipeRating>();
        public DbSet<MealPlan> MealPlans => Set<MealPlan>();
        public DbSet<MealPlanRecipe> MealPlanRecipes => Set<MealPlanRecipe>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Household> Households => Set<Household>();
        public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
        public DbSet<InviteCode> InviteCodes => Set<InviteCode>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Check if we're using SQL Server for provider-specific configurations
            var isSqlServer = Database.IsSqlServer();
            
            // User entity configuration
            modelBuilder.Entity<User>(b =>
            {
                b.ToTable("Users");
                b.HasKey(x => x.Id);
                b.Property(x => x.ExternalIdObjectId).IsRequired().HasMaxLength(100);
                b.Property(x => x.Email).IsRequired().HasMaxLength(256);
                b.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
                b.Property(x => x.PhotoUrl).HasMaxLength(2000);
                
                if (isSqlServer)
                {
                    b.Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
                    b.Property(x => x.PreferencesJson).HasColumnType("nvarchar(max)");
                }
                
                // Unique indexes
                b.HasIndex(x => x.ExternalIdObjectId).IsUnique();
                b.HasIndex(x => x.Email).IsUnique();
            });
            
            // Household entity configuration
            modelBuilder.Entity<Household>(b =>
            {
                b.ToTable("Households");
                b.HasKey(x => x.Id);
                b.Property(x => x.Name).IsRequired().HasMaxLength(200);
                
                if (isSqlServer)
                {
                    b.Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
                }
                
                b.HasOne(x => x.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            
            // HouseholdMember junction entity configuration
            modelBuilder.Entity<HouseholdMember>(b =>
            {
                b.ToTable("HouseholdMembers");
                b.HasKey(x => x.Id);
                b.Property(x => x.Role).IsRequired().HasConversion<string>().HasMaxLength(50);
                
                if (isSqlServer)
                {
                    b.Property(x => x.JoinedUtc).HasDefaultValueSql("GETUTCDATE()");
                }
                
                b.HasOne(x => x.User)
                    .WithMany(x => x.HouseholdMemberships)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                b.HasOne(x => x.Household)
                    .WithMany(x => x.Members)
                    .HasForeignKey(x => x.HouseholdId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                // A user can only be a member of a household once
                b.HasIndex(x => new { x.UserId, x.HouseholdId }).IsUnique();
            });

            // InviteCode entity configuration
            modelBuilder.Entity<InviteCode>(b =>
            {
                b.ToTable("InviteCodes");
                b.HasKey(x => x.Id);
                b.Property(x => x.Code).IsRequired().HasMaxLength(8);
                b.Property(x => x.IsRevoked).HasDefaultValue(false);
                
                if (isSqlServer)
                {
                    b.Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
                    b.Property(x => x.ExpiresUtc).HasColumnType("datetime2");
                    b.Property(x => x.UsedUtc).HasColumnType("datetime2");
                }
                
                // Unique index on Code for fast lookups
                b.HasIndex(x => x.Code).IsUnique();
                
                b.HasOne(x => x.Household)
                    .WithMany()
                    .HasForeignKey(x => x.HouseholdId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                b.HasOne(x => x.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                b.HasOne(x => x.UsedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.UsedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Recipe>(b =>
            {
                b.ToTable("Recipes");
                b.HasKey(x => x.Id);
                b.Property(x => x.Title).IsRequired().HasMaxLength(200);
                b.Property(x => x.Description).HasMaxLength(2000);
                b.Property(x => x.CuisineType).HasMaxLength(100);
                b.Property(x => x.CreatedBy).HasMaxLength(200);
                
                if (isSqlServer)
                {
                    b.Property(x => x.ImageUrl).HasColumnType("nvarchar(max)");
                    b.Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
                    b.Property(x => x.UpdatedUtc).HasColumnType("datetime2");
                    b.Property(x => x.SourceImageUrl).HasColumnType("nvarchar(max)");
                    b.Property(x => x.ExtractionConfidence).HasColumnType("decimal(3,2)");
                }
                
                // GenAI extraction metadata
                b.Property(x => x.IsExtracted).HasDefaultValue(false);
            });

            modelBuilder.Entity<RecipeIngredient>(b =>
            {
                b.ToTable("RecipeIngredients");
                b.HasKey(x => x.Id);
                b.Property(x => x.Name).IsRequired().HasMaxLength(200);
                b.Property(x => x.Quantity).HasMaxLength(100);
                b.Property(x => x.Unit).HasMaxLength(50);
                
                if (isSqlServer)
                {
                    b.Property(x => x.QuantityValue).HasColumnType("decimal(10,4)");
                }
                
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

            modelBuilder.Entity<RecipeRating>(b =>
            {
                b.ToTable("RecipeRatings");
                b.HasKey(x => x.Id);
                b.Property(x => x.UserId).IsRequired().HasMaxLength(200);
                b.Property(x => x.Comments).HasMaxLength(500);
                b.Property(x => x.FrequencyPreference).HasMaxLength(50);
                
                if (isSqlServer)
                {
                    b.Property(x => x.RatedUtc).HasDefaultValueSql("GETUTCDATE()");
                }
                
                b.HasOne(x => x.Recipe)
                    .WithMany(x => x.Ratings)
                    .HasForeignKey(x => x.RecipeId)
                    .OnDelete(DeleteBehavior.Cascade);
                // Index for efficient lookups by recipe and user (not unique to allow historical ratings)
                b.HasIndex(x => new { x.RecipeId, x.UserId });
            });

            modelBuilder.Entity<MealPlan>(b =>
            {
                b.ToTable("MealPlans");
                b.HasKey(x => x.Id);
                b.Property(x => x.Name).IsRequired().HasMaxLength(200);
                b.Property(x => x.CreatedBy).HasMaxLength(200);
                b.Property(x => x.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Draft");
                
                if (isSqlServer)
                {
                    b.Property(x => x.StartDate).HasColumnType("date");
                    b.Property(x => x.EndDate).HasColumnType("date");
                    b.Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
                }
                
                // Index for listing meal plans by date
                b.HasIndex(x => x.StartDate);
            });

            modelBuilder.Entity<MealPlanRecipe>(b =>
            {
                b.ToTable("MealPlanRecipes");
                b.HasKey(x => x.Id);
                
                if (isSqlServer)
                {
                    b.Property(x => x.Day).HasColumnType("date");
                    b.Property(x => x.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
                }
                
                b.HasOne(x => x.MealPlan)
                    .WithMany(x => x.Recipes)
                    .HasForeignKey(x => x.MealPlanId)
                    .OnDelete(DeleteBehavior.Cascade);
                b.HasOne(x => x.Recipe)
                    .WithMany()
                    .HasForeignKey(x => x.RecipeId)
                    .OnDelete(DeleteBehavior.Restrict); // Don't cascade delete recipes
                // Index for efficient lookups by meal plan and day
                b.HasIndex(x => new { x.MealPlanId, x.Day });
                // Index for finding when a recipe was last cooked
                b.HasIndex(x => new { x.RecipeId, x.Day });
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

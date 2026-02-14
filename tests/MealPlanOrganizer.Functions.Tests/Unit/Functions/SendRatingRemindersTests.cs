using FluentAssertions;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Functions;
using MealPlanOrganizer.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Timer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TimeZoneConverter;
using Xunit;

namespace MealPlanOrganizer.Functions.Tests.Unit.Functions;

/// <summary>
/// Unit tests for the SendRatingReminders timer-triggered function.
/// Tests auto-dismiss logic, 8pm detection, and pending rating creation.
/// </summary>
public class SendRatingRemindersTests : IDisposable
{
    private readonly Mock<ILogger<SendRatingReminders>> _loggerMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly AppDbContext _context;
    private readonly SendRatingReminders _function;

    public SendRatingRemindersTests()
    {
        _loggerMock = new Mock<ILogger<SendRatingReminders>>();
        _notificationServiceMock = new Mock<INotificationService>();

        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);

        _function = new SendRatingReminders(_loggerMock.Object, _context, _notificationServiceMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region AutoDismiss Tests

    [Fact]
    public async Task Run_AutoDismissesOldPendingRatings()
    {
        // Arrange
        var household = await CreateHouseholdAsync("America/New_York");
        var user = await CreateUserAsync(household);

        // Create old pending rating (more than 24 hours old)
        var oldPendingRating = new PendingRating
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = user.Id,
            RecipeId = Guid.NewGuid(),
            MealPlanId = Guid.NewGuid(),
            MealPlanRecipeId = Guid.NewGuid(),
            ServedDate = DateTime.UtcNow.Date.AddDays(-2),
            Status = "Pending",
            CreatedUtc = DateTime.UtcNow.AddHours(-25) // More than 24 hours old
        };
        _context.PendingRatings.Add(oldPendingRating);
        await _context.SaveChangesAsync();

        var timerInfo = CreateTimerInfo();

        // Act
        await _function.Run(timerInfo);

        // Assert
        var updatedRating = await _context.PendingRatings.FindAsync(oldPendingRating.Id);
        updatedRating.Should().NotBeNull();
        updatedRating!.Status.Should().Be("Dismissed");
        updatedRating.CompletedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Run_DoesNotDismissRecentPendingRatings()
    {
        // Arrange
        var household = await CreateHouseholdAsync("America/New_York");
        var user = await CreateUserAsync(household);

        // Create recent pending rating (less than 24 hours old)
        var recentPendingRating = new PendingRating
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = user.Id,
            RecipeId = Guid.NewGuid(),
            MealPlanId = Guid.NewGuid(),
            MealPlanRecipeId = Guid.NewGuid(),
            ServedDate = DateTime.UtcNow.Date,
            Status = "Pending",
            CreatedUtc = DateTime.UtcNow.AddHours(-23) // Less than 24 hours old
        };
        _context.PendingRatings.Add(recentPendingRating);
        await _context.SaveChangesAsync();

        var timerInfo = CreateTimerInfo();

        // Act
        await _function.Run(timerInfo);

        // Assert
        var rating = await _context.PendingRatings.FindAsync(recentPendingRating.Id);
        rating.Should().NotBeNull();
        rating!.Status.Should().Be("Pending");
        rating.CompletedUtc.Should().BeNull();
    }

    [Fact]
    public async Task Run_DoesNotDismissCompletedRatings()
    {
        // Arrange
        var household = await CreateHouseholdAsync("America/New_York");
        var user = await CreateUserAsync(household);

        // Create old completed rating
        var completedRating = new PendingRating
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = user.Id,
            RecipeId = Guid.NewGuid(),
            MealPlanId = Guid.NewGuid(),
            MealPlanRecipeId = Guid.NewGuid(),
            ServedDate = DateTime.UtcNow.Date.AddDays(-2),
            Status = "Completed",
            CompletedUtc = DateTime.UtcNow.AddHours(-20),
            CreatedUtc = DateTime.UtcNow.AddHours(-30)
        };
        _context.PendingRatings.Add(completedRating);
        await _context.SaveChangesAsync();

        var timerInfo = CreateTimerInfo();

        // Act
        await _function.Run(timerInfo);

        // Assert
        var rating = await _context.PendingRatings.FindAsync(completedRating.Id);
        rating.Should().NotBeNull();
        rating!.Status.Should().Be("Completed"); // Should remain unchanged
    }

    #endregion

    #region Pending Rating Creation Tests

    [Fact]
    public async Task Run_CreatesPendingRatingsForHouseholdsAt8pm()
    {
        // Arrange
        // Find a timezone that is currently at 8pm
        var now = DateTime.UtcNow;
        var targetHour = 20; // 8pm

        // Create household with timezone where it's 8pm now
        // For testing, we'll use a timezone calculation
        var household = await CreateHouseholdWith8pmTimezoneAsync(now, targetHour);
        var user = await CreateUserAsync(household);
        var recipe = await CreateRecipeAsync();

        // Create meal plan with recipe served today
        var mealPlan = await CreateMealPlanAsync(household, user, recipe, now);

        var timerInfo = CreateTimerInfo();

        // Act
        await _function.Run(timerInfo);

        // Assert
        var pendingRatings = await _context.PendingRatings
            .Where(pr => pr.HouseholdId == household.Id && pr.UserId == user.Id)
            .ToListAsync();

        pendingRatings.Should().NotBeEmpty();
        pendingRatings.Should().Contain(pr => pr.RecipeId == recipe.Id);
    }

    [Fact]
    public async Task Run_DoesNotDuplicatePendingRatings()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var targetHour = 20;

        var household = await CreateHouseholdWith8pmTimezoneAsync(now, targetHour);
        var user = await CreateUserAsync(household);
        var recipe = await CreateRecipeAsync();
        var mealPlan = await CreateMealPlanAsync(household, user, recipe, now);

        // Get the meal plan recipe
        var mealPlanRecipe = await _context.MealPlanRecipes.FirstAsync(mpr => mpr.MealPlanId == mealPlan.Id);

        // Pre-create a pending rating
        var existingRating = new PendingRating
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = user.Id,
            RecipeId = recipe.Id,
            MealPlanId = mealPlan.Id,
            MealPlanRecipeId = mealPlanRecipe.Id,
            ServedDate = now.Date,
            Status = "Pending",
            CreatedUtc = now.AddHours(-1)
        };
        _context.PendingRatings.Add(existingRating);
        await _context.SaveChangesAsync();

        var initialCount = await _context.PendingRatings.CountAsync();
        var timerInfo = CreateTimerInfo();

        // Act
        await _function.Run(timerInfo);

        // Assert
        var finalCount = await _context.PendingRatings.CountAsync();
        finalCount.Should().Be(initialCount); // No new ratings created
    }

    [Fact]
    public async Task Run_CreatesPendingRatingsForAllHouseholdMembers()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var targetHour = 20;

        var household = await CreateHouseholdWith8pmTimezoneAsync(now, targetHour);
        var user1 = await CreateUserAsync(household, "user1@test.com");
        var user2 = await CreateUserAsync(household, "user2@test.com");
        var recipe = await CreateRecipeAsync();
        var mealPlan = await CreateMealPlanAsync(household, user1, recipe, now);

        var timerInfo = CreateTimerInfo();

        // Act
        await _function.Run(timerInfo);

        // Assert
        var pendingRatings = await _context.PendingRatings
            .Where(pr => pr.HouseholdId == household.Id)
            .ToListAsync();

        // Should have ratings for both users
        pendingRatings.Should().Contain(pr => pr.UserId == user1.Id);
        pendingRatings.Should().Contain(pr => pr.UserId == user2.Id);
    }

    [Fact]
    public async Task Run_OnlyProcessesActiveHouseholds()
    {
        // Arrange
        // Create household without members
        var emptyHousehold = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Empty Household",
            TimeZoneId = "America/New_York",
            CreatedUtc = DateTime.UtcNow
        };
        _context.Households.Add(emptyHousehold);
        await _context.SaveChangesAsync();

        var timerInfo = CreateTimerInfo();

        // Act
        await _function.Run(timerInfo);

        // Assert - No pending ratings should be created for empty household
        var pendingRatings = await _context.PendingRatings
            .Where(pr => pr.HouseholdId == emptyHousehold.Id)
            .ToListAsync();

        pendingRatings.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_OnlyProcessesActiveMealPlans()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var targetHour = 20;

        var household = await CreateHouseholdWith8pmTimezoneAsync(now, targetHour);
        var user = await CreateUserAsync(household);
        var recipe = await CreateRecipeAsync();

        // Create draft (inactive) meal plan
        var draftMealPlan = new MealPlan
        {
            Id = Guid.NewGuid(),
            Name = "Draft Plan",
            StartDate = now.Date,
            EndDate = now.Date.AddDays(6),
            CreatedBy = user.ExternalIdObjectId,
            HouseholdId = household.Id,
            UserId = user.Id,
            Status = "Draft", // Not active
            Recipes = new List<MealPlanRecipe>
            {
                new MealPlanRecipe
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    Day = now.Date
                }
            }
        };
        _context.MealPlans.Add(draftMealPlan);
        await _context.SaveChangesAsync();

        var timerInfo = CreateTimerInfo();

        // Act
        await _function.Run(timerInfo);

        // Assert
        var pendingRatings = await _context.PendingRatings
            .Where(pr => pr.HouseholdId == household.Id)
            .ToListAsync();

        pendingRatings.Should().BeEmpty();
    }

    #endregion

    #region Notification Tests

    [Fact]
    public async Task Run_SendsNotificationsWhenPendingRatingsCreated()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var targetHour = 20;

        var household = await CreateHouseholdWith8pmTimezoneAsync(now, targetHour);
        var user = await CreateUserAsync(household);
        var recipe = await CreateRecipeAsync();
        var mealPlan = await CreateMealPlanAsync(household, user, recipe, now);

        var timerInfo = CreateTimerInfo();

        // Act
        await _function.Run(timerInfo);

        // Assert
        _notificationServiceMock.Verify(
            x => x.SendToUserAsync(
                user.Id,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Run_ContinuesIfNotificationFails()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var targetHour = 20;

        var household = await CreateHouseholdWith8pmTimezoneAsync(now, targetHour);
        var user = await CreateUserAsync(household);
        var recipe = await CreateRecipeAsync();
        var mealPlan = await CreateMealPlanAsync(household, user, recipe, now);

        // Setup notification to throw exception
        _notificationServiceMock
            .Setup(x => x.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .ThrowsAsync(new Exception("Notification failed"));

        var timerInfo = CreateTimerInfo();

        // Act - Should not throw
        await _function.Run(timerInfo);

        // Assert - Pending ratings should still be created
        var pendingRatings = await _context.PendingRatings
            .Where(pr => pr.HouseholdId == household.Id)
            .ToListAsync();

        pendingRatings.Should().NotBeEmpty();
    }

    #endregion

    #region Helper Methods

    private static TimerInfo CreateTimerInfo()
    {
        // TimerInfo from Microsoft.Azure.Functions.Worker has a different structure
        // Create a simple mock since we don't use the timer info in our implementation
        return new TimerInfo();
    }

    private async Task<Household> CreateHouseholdAsync(string timeZoneId)
    {
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Test Household",
            TimeZoneId = timeZoneId,
            CreatedUtc = DateTime.UtcNow,
            Members = new List<HouseholdMember>()
        };
        _context.Households.Add(household);
        await _context.SaveChangesAsync();
        return household;
    }

    private async Task<Household> CreateHouseholdWith8pmTimezoneAsync(DateTime utcNow, int targetHour)
    {
        // Calculate which UTC offset would make it 8pm right now
        // If we want it to be 8pm in local time, and we have it's X:00 UTC
        // then we need offset = targetHour - currentUtcHour
        var desiredOffset = targetHour - utcNow.Hour;
        if (desiredOffset < -12) desiredOffset += 24;
        if (desiredOffset > 14) desiredOffset -= 24;

        // Find a timezone with approximately this offset
        // Use common timezones that might match
        var timezone = FindTimezoneForOffset(desiredOffset);

        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "8pm Household",
            TimeZoneId = timezone,
            CreatedUtc = DateTime.UtcNow,
            Members = new List<HouseholdMember>()
        };
        _context.Households.Add(household);
        await _context.SaveChangesAsync();
        return household;
    }

    private static string FindTimezoneForOffset(int offset)
    {
        // Map offsets to IANA timezone IDs
        return offset switch
        {
            -12 => "Etc/GMT+12",
            -11 => "Pacific/Pago_Pago",
            -10 => "Pacific/Honolulu",
            -9 => "America/Anchorage",
            -8 => "America/Los_Angeles",
            -7 => "America/Denver",
            -6 => "America/Chicago",
            -5 => "America/New_York",
            -4 => "America/Halifax",
            -3 => "America/Sao_Paulo",
            -2 => "Atlantic/South_Georgia",
            -1 => "Atlantic/Azores",
            0 => "Europe/London",
            1 => "Europe/Paris",
            2 => "Europe/Athens",
            3 => "Europe/Moscow",
            4 => "Asia/Dubai",
            5 => "Asia/Karachi",
            6 => "Asia/Dhaka",
            7 => "Asia/Bangkok",
            8 => "Asia/Singapore",
            9 => "Asia/Tokyo",
            10 => "Australia/Sydney",
            11 => "Pacific/Noumea",
            12 => "Pacific/Fiji",
            _ => "America/New_York"
        };
    }

    private async Task<User> CreateUserAsync(Household household, string? email = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = Guid.NewGuid().ToString(),
            Email = email ?? $"test-{Guid.NewGuid()}@test.com",
            DisplayName = "Test User"
        };
        _context.Users.Add(user);

        var membership = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = user.Id,
            Role = HouseholdRole.Member,
            JoinedUtc = DateTime.UtcNow
        };
        household.Members ??= new List<HouseholdMember>();
        household.Members.Add(membership);
        _context.HouseholdMembers.Add(membership);

        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Recipe> CreateRecipeAsync()
    {
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = "Test Recipe",
            Description = "Test description",
            CreatedBy = "test-user",
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Ingredient",
                    Quantity = "1",
                    Unit = "cup"
                }
            },
            Steps = new List<RecipeStep>
            {
                new RecipeStep
                {
                    Id = Guid.NewGuid(),
                    StepNumber = 1,
                    Instruction = "Test instruction"
                }
            }
        };
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();
        return recipe;
    }

    private async Task<MealPlan> CreateMealPlanAsync(Household household, User user, Recipe recipe, DateTime utcDate)
    {
        // Convert UTC to household local time to get the correct local date
        var tzInfo = TZConvert.GetTimeZoneInfo(household.TimeZoneId);
        var localDate = TimeZoneInfo.ConvertTimeFromUtc(utcDate, tzInfo).Date;
        
        var mealPlan = new MealPlan
        {
            Id = Guid.NewGuid(),
            Name = "Test Meal Plan",
            StartDate = localDate,
            EndDate = localDate.AddDays(6),
            CreatedBy = user.ExternalIdObjectId,
            HouseholdId = household.Id,
            UserId = user.Id,
            Status = "Active",
            Recipes = new List<MealPlanRecipe>
            {
                new MealPlanRecipe
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    Day = localDate
                }
            }
        };
        _context.MealPlans.Add(mealPlan);
        await _context.SaveChangesAsync();
        return mealPlan;
    }

    #endregion
}

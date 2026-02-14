using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Services;
using TimeZoneConverter;

namespace MealPlanOrganizer.Functions.Functions;

/// <summary>
/// Timer-triggered function that runs every hour to:
/// 1. Create pending ratings for recipes served today at 8pm household time
/// 2. Auto-dismiss pending ratings older than 24 hours
/// </summary>
public class SendRatingReminders
{
    private readonly ILogger<SendRatingReminders> _logger;
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;

    public SendRatingReminders(ILogger<SendRatingReminders> logger, AppDbContext context, INotificationService notificationService)
    {
        _logger = logger;
        _context = context;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Runs every hour at minute 0. Checks if it's 8pm for any household and creates pending ratings.
    /// </summary>
    [Function("SendRatingReminders")]
    public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("SendRatingReminders triggered at {UtcNow}", DateTime.UtcNow);

        try
        {
            // 1. Auto-dismiss pending ratings older than 24 hours
            await AutoDismissOldPendingRatingsAsync();

            // 2. Create pending ratings for households where it's 8pm
            await CreatePendingRatingsFor8pmHouseholdsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SendRatingReminders");
            throw;
        }
    }

    /// <summary>
    /// Auto-dismiss any pending ratings that are more than 24 hours old.
    /// </summary>
    private async Task AutoDismissOldPendingRatingsAsync()
    {
        var cutoffTime = DateTime.UtcNow.AddHours(-24);

        var oldPendingRatings = await _context.PendingRatings
            .Where(pr => pr.Status == "Pending" && pr.CreatedUtc < cutoffTime)
            .ToListAsync();

        if (oldPendingRatings.Count == 0)
        {
            _logger.LogDebug("No old pending ratings to dismiss");
            return;
        }

        foreach (var pendingRating in oldPendingRatings)
        {
            pendingRating.Status = "Dismissed";
            pendingRating.CompletedUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Auto-dismissed {Count} pending ratings older than 24 hours", oldPendingRatings.Count);
    }

    /// <summary>
    /// Find households where it's currently 8pm in their timezone and create pending ratings
    /// for any recipes served today.
    /// </summary>
    private async Task CreatePendingRatingsFor8pmHouseholdsAsync()
    {
        var utcNow = DateTime.UtcNow;
        
        // Get all households with their timezones
        var households = await _context.Households
            .Include(h => h.Members)
            .Where(h => h.Members.Any()) // Only households with members
            .ToListAsync();

        var householdsAt8pm = new List<Household>();

        foreach (var household in households)
        {
            try
            {
                var localTime = ConvertUtcToHouseholdTime(utcNow, household.TimeZoneId);
                
                // Check if it's currently the 8pm hour (20:xx)
                if (localTime.Hour == 20)
                {
                    householdsAt8pm.Add(household);
                    _logger.LogDebug("Household {HouseholdId} ({Name}) is at 8pm local time", 
                        household.Id, household.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to convert timezone for household {HouseholdId} with timezone {TimeZoneId}", 
                    household.Id, household.TimeZoneId);
            }
        }

        if (householdsAt8pm.Count == 0)
        {
            _logger.LogDebug("No households at 8pm right now");
            return;
        }

        _logger.LogInformation("Found {Count} households at 8pm local time", householdsAt8pm.Count);

        foreach (var household in householdsAt8pm)
        {
            await CreatePendingRatingsForHouseholdAsync(household, utcNow);
        }
    }

    /// <summary>
    /// Create pending ratings for all members of a household for recipes served today.
    /// </summary>
    private async Task CreatePendingRatingsForHouseholdAsync(Household household, DateTime utcNow)
    {
        // Get today's date in the household's timezone
        var localNow = ConvertUtcToHouseholdTime(utcNow, household.TimeZoneId);
        var todayDate = localNow.Date;

        // Find meal plan recipes served today for this household
        var servedRecipes = await _context.MealPlanRecipes
            .Include(mpr => mpr.MealPlan)
            .Include(mpr => mpr.Recipe)
            .Where(mpr => mpr.MealPlan != null 
                       && mpr.MealPlan.HouseholdId == household.Id
                       && mpr.MealPlan.Status == "Active"
                       && mpr.Day.Date == todayDate)
            .ToListAsync();

        if (servedRecipes.Count == 0)
        {
            _logger.LogDebug("No recipes served today for household {HouseholdId}", household.Id);
            return;
        }

        _logger.LogInformation("Found {Count} recipes served today for household {HouseholdId}", 
            servedRecipes.Count, household.Id);

        // Get all member user IDs
        var memberUserIds = household.Members.Select(m => m.UserId).ToList();

        var pendingRatingsCreated = 0;

        foreach (var mealPlanRecipe in servedRecipes)
        {
            foreach (var userId in memberUserIds)
            {
                // Check if pending rating already exists for this user and meal plan recipe
                var exists = await _context.PendingRatings
                    .AnyAsync(pr => pr.UserId == userId && pr.MealPlanRecipeId == mealPlanRecipe.Id);

                if (exists)
                {
                    _logger.LogDebug("Pending rating already exists for user {UserId} and MealPlanRecipe {MealPlanRecipeId}", 
                        userId, mealPlanRecipe.Id);
                    continue;
                }

                var pendingRating = new PendingRating
                {
                    Id = Guid.NewGuid(),
                    HouseholdId = household.Id,
                    UserId = userId,
                    RecipeId = mealPlanRecipe.RecipeId,
                    MealPlanId = mealPlanRecipe.MealPlanId,
                    MealPlanRecipeId = mealPlanRecipe.Id,
                    ServedDate = mealPlanRecipe.Day.Date,
                    CreatedUtc = utcNow,
                    Status = "Pending"
                };

                _context.PendingRatings.Add(pendingRating);
                pendingRatingsCreated++;
            }
        }

        if (pendingRatingsCreated > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created {Count} pending ratings for household {HouseholdId}", 
                pendingRatingsCreated, household.Id);
            
            // Send push notifications to household members
            await SendRatingNotificationsAsync(household, servedRecipes);
        }
    }

    /// <summary>
    /// Sends push notifications to all household members about recipes to rate.
    /// </summary>
    private async Task SendRatingNotificationsAsync(Household household, List<MealPlanRecipe> servedRecipes)
    {
        if (servedRecipes.Count == 0) return;

        // Build notification content
        string title;
        string body;

        if (servedRecipes.Count == 1)
        {
            var recipe = servedRecipes[0].Recipe;
            title = "Rate Tonight's Dinner!";
            body = $"How was {recipe?.Title ?? "tonight's recipe"}? Tap to rate.";
        }
        else
        {
            title = "Rate Tonight's Recipes!";
            body = $"You have {servedRecipes.Count} recipes to rate. Tap to get started.";
        }

        var data = new Dictionary<string, string>
        {
            ["action"] = "rate_recipe",
            ["householdId"] = household.Id.ToString()
        };

        // Send to each household member individually
        foreach (var member in household.Members)
        {
            try
            {
                await _notificationService.SendToUserAsync(member.UserId, title, body, data);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send notification to user {UserId}", member.UserId);
            }
        }

        _logger.LogInformation("Sent rating notifications to {Count} household members", household.Members.Count);
    }

    /// <summary>
    /// Converts a UTC datetime to the household's local time.
    /// Uses TimeZoneConverter for cross-platform IANA timezone support.
    /// </summary>
    private static DateTime ConvertUtcToHouseholdTime(DateTime utcTime, string ianaTimeZoneId)
    {
        // TimeZoneConverter handles IANA to Windows timezone conversion
        var tzInfo = TZConvert.GetTimeZoneInfo(ianaTimeZoneId);
        return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tzInfo);
    }
}

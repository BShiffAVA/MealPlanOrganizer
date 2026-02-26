using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Tests.Integration.Fixtures;

namespace MealPlanOrganizer.Functions.Tests.Integration.Builders;

/// <summary>
/// Fluent builder for creating MealPlan entities in tests.
/// </summary>
public class MealPlanBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Test Meal Plan";
    private DateTime _startDate = DateTime.UtcNow.Date;
    private DateTime _endDate;
    private string? _createdBy = TestData.User1Id.ToString();
    private DateTime _createdUtc = DateTime.UtcNow;
    private string _status = "Draft";
    private readonly List<MealPlanRecipe> _recipes = new();

    public MealPlanBuilder()
    {
        _endDate = _startDate.AddDays(6); // Default to a week
    }

    public static MealPlanBuilder Create() => new();

    public static MealPlanBuilder CreateForCurrentWeek()
    {
        var today = DateTime.UtcNow.Date;
        var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
        return new MealPlanBuilder()
            .WithName($"Week of {startOfWeek:MMM d}")
            .WithDateRange(startOfWeek, startOfWeek.AddDays(6));
    }

    public static MealPlanBuilder CreateForNextWeek()
    {
        var today = DateTime.UtcNow.Date;
        var startOfNextWeek = today.AddDays(7 - (int)today.DayOfWeek);
        return new MealPlanBuilder()
            .WithName($"Week of {startOfNextWeek:MMM d}")
            .WithDateRange(startOfNextWeek, startOfNextWeek.AddDays(6));
    }

    public MealPlanBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public MealPlanBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public MealPlanBuilder WithStartDate(DateTime startDate)
    {
        _startDate = startDate;
        return this;
    }

    public MealPlanBuilder WithEndDate(DateTime endDate)
    {
        _endDate = endDate;
        return this;
    }

    public MealPlanBuilder WithDateRange(DateTime startDate, DateTime endDate)
    {
        _startDate = startDate;
        _endDate = endDate;
        return this;
    }

    public MealPlanBuilder ForWeekStarting(DateTime startDate)
    {
        _startDate = startDate;
        _endDate = startDate.AddDays(6);
        _name = $"Week of {startDate:MMM d}";
        return this;
    }

    public MealPlanBuilder WithCreatedBy(string? createdBy)
    {
        _createdBy = createdBy;
        return this;
    }

    public MealPlanBuilder WithCreatedUtc(DateTime createdUtc)
    {
        _createdUtc = createdUtc;
        return this;
    }

    public MealPlanBuilder WithStatus(string status)
    {
        _status = status;
        return this;
    }

    public MealPlanBuilder AsDraft() => WithStatus("Draft");
    public MealPlanBuilder AsActive() => WithStatus("Active");
    public MealPlanBuilder AsComplete() => WithStatus("Complete");

    public MealPlanBuilder WithRecipe(Guid recipeId, DateTime day)
    {
        _recipes.Add(new MealPlanRecipe
        {
            Id = Guid.NewGuid(),
            MealPlanId = _id,
            RecipeId = recipeId,
            Day = day,
            CreatedUtc = DateTime.UtcNow
        });
        return this;
    }

    public MealPlanBuilder WithRecipeOnDay(Guid recipeId, int dayOffset)
    {
        return WithRecipe(recipeId, _startDate.AddDays(dayOffset));
    }

    public MealPlanBuilder WithRecipeOnMonday(Guid recipeId) => WithRecipeOnDay(recipeId, 1);
    public MealPlanBuilder WithRecipeOnTuesday(Guid recipeId) => WithRecipeOnDay(recipeId, 2);
    public MealPlanBuilder WithRecipeOnWednesday(Guid recipeId) => WithRecipeOnDay(recipeId, 3);
    public MealPlanBuilder WithRecipeOnThursday(Guid recipeId) => WithRecipeOnDay(recipeId, 4);
    public MealPlanBuilder WithRecipeOnFriday(Guid recipeId) => WithRecipeOnDay(recipeId, 5);
    public MealPlanBuilder WithRecipeOnSaturday(Guid recipeId) => WithRecipeOnDay(recipeId, 6);
    public MealPlanBuilder WithRecipeOnSunday(Guid recipeId) => WithRecipeOnDay(recipeId, 0);

    /// <summary>
    /// Assigns recipes to multiple days of the week.
    /// </summary>
    public MealPlanBuilder WithWeeklyRecipes(params (int dayOffset, Guid recipeId)[] assignments)
    {
        foreach (var (dayOffset, recipeId) in assignments)
        {
            WithRecipeOnDay(recipeId, dayOffset);
        }
        return this;
    }

    /// <summary>
    /// Assigns the same recipe to all weekdays.
    /// </summary>
    public MealPlanBuilder WithRecipeForAllWeekdays(Guid recipeId)
    {
        return WithRecipeOnMonday(recipeId)
            .WithRecipeOnTuesday(recipeId)
            .WithRecipeOnWednesday(recipeId)
            .WithRecipeOnThursday(recipeId)
            .WithRecipeOnFriday(recipeId);
    }

    /// <summary>
    /// Creates a complete meal plan with recipes for each day.
    /// </summary>
    public MealPlanBuilder WithFullWeek(params Guid[] recipeIds)
    {
        for (int i = 0; i < Math.Min(7, recipeIds.Length); i++)
        {
            WithRecipeOnDay(recipeIds[i], i);
        }
        return this;
    }

    public MealPlan Build()
    {
        var mealPlan = new MealPlan
        {
            Id = _id,
            Name = _name,
            StartDate = _startDate,
            EndDate = _endDate,
            CreatedBy = _createdBy,
            CreatedUtc = _createdUtc,
            Status = _status
        };

        foreach (var recipe in _recipes)
        {
            recipe.MealPlanId = _id;
            mealPlan.Recipes.Add(recipe);
        }

        return mealPlan;
    }
}

/// <summary>
/// Fluent builder for creating RecipeRating entities in tests.
/// </summary>
public class RecipeRatingBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _recipeId;
    private string _userId = TestData.User1Id.ToString();
    private int _rating = 4;
    private string? _comments;
    private string? _nextTimePreference;
    private DateTime _ratedUtc = DateTime.UtcNow;

    public static RecipeRatingBuilder Create() => new();

    public RecipeRatingBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public RecipeRatingBuilder WithRecipeId(Guid recipeId)
    {
        _recipeId = recipeId;
        return this;
    }

    public RecipeRatingBuilder WithUserId(string userId)
    {
        _userId = userId;
        return this;
    }

    public RecipeRatingBuilder WithRating(int rating)
    {
        _rating = Math.Clamp(rating, 1, 5);
        return this;
    }

    public RecipeRatingBuilder WithComments(string? comments)
    {
        _comments = comments;
        return this;
    }

    public RecipeRatingBuilder WithNextTimePreference(string? preference)
    {
        _nextTimePreference = preference;
        return this;
    }

    public RecipeRatingBuilder PreferOnceAWeek() => WithNextTimePreference("OnceAWeek");
    public RecipeRatingBuilder PreferOnceAMonth() => WithNextTimePreference("OnceAMonth");
    public RecipeRatingBuilder PreferAFewTimesAYear() => WithNextTimePreference("AFewTimesAYear");
    public RecipeRatingBuilder PreferYearly() => WithNextTimePreference("Yearly");
    public RecipeRatingBuilder PreferNever() => WithNextTimePreference("Never");

    public RecipeRatingBuilder WithRatedUtc(DateTime ratedUtc)
    {
        _ratedUtc = ratedUtc;
        return this;
    }

    /// <summary>
    /// Creates a 5-star rating with positive comments.
    /// </summary>
    public RecipeRatingBuilder AsFavorite()
    {
        return WithRating(5)
            .WithComments("One of my favorites!")
            .PreferOnceAWeek();
    }

    /// <summary>
    /// Creates a 1-star rating with negative comments.
    /// </summary>
    public RecipeRatingBuilder AsDisliked()
    {
        return WithRating(1)
            .WithComments("Not for me")
            .PreferNever();
    }

    public RecipeRating Build()
    {
        return new RecipeRating
        {
            Id = _id,
            RecipeId = _recipeId,
            UserId = _userId,
            Rating = _rating,
            Comments = _comments,
            NextTimePreference = _nextTimePreference,
            RatedUtc = _ratedUtc
        };
    }
}

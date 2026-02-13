using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Tests.Integration.Fixtures;

namespace MealPlanOrganizer.Functions.Tests.Integration.Builders;

/// <summary>
/// Fluent builder for creating Recipe entities in tests.
/// </summary>
public class RecipeBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _title = "Test Recipe";
    private string? _description = "A delicious test recipe";
    private string? _cuisineType = "Test";
    private int? _prepTimeMinutes = 15;
    private int? _cookTimeMinutes = 30;
    private int? _servings = 4;
    private string? _imageUrl;
    private string? _createdBy = TestData.User1DisplayName;
    private Guid? _createdByUserId = null; // Default to null; set explicitly for authorization tests
    private DateTime _createdUtc = DateTime.UtcNow;
    private DateTime? _updatedUtc;
    private bool _isExtracted = false;
    private string? _sourceImageUrl;
    private decimal? _extractionConfidence;
    private readonly List<RecipeIngredient> _ingredients = new();
    private readonly List<RecipeStep> _steps = new();
    private readonly List<RecipeRating> _ratings = new();

    public static RecipeBuilder Create() => new();

    public RecipeBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public RecipeBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public RecipeBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public RecipeBuilder WithCuisineType(string? cuisineType)
    {
        _cuisineType = cuisineType;
        return this;
    }

    public RecipeBuilder WithPrepTime(int? minutes)
    {
        _prepTimeMinutes = minutes;
        return this;
    }

    public RecipeBuilder WithCookTime(int? minutes)
    {
        _cookTimeMinutes = minutes;
        return this;
    }

    public RecipeBuilder WithServings(int? servings)
    {
        _servings = servings;
        return this;
    }

    public RecipeBuilder WithImageUrl(string? imageUrl)
    {
        _imageUrl = imageUrl;
        return this;
    }

    public RecipeBuilder WithCreatedBy(string? createdBy)
    {
        _createdBy = createdBy;
        return this;
    }

    public RecipeBuilder WithCreatedByUserId(Guid? createdByUserId)
    {
        _createdByUserId = createdByUserId;
        return this;
    }

    public RecipeBuilder WithCreatedUtc(DateTime createdUtc)
    {
        _createdUtc = createdUtc;
        return this;
    }

    public RecipeBuilder WithUpdatedUtc(DateTime? updatedUtc)
    {
        _updatedUtc = updatedUtc;
        return this;
    }

    public RecipeBuilder AsExtracted(string? sourceImageUrl = null, decimal? confidence = 0.95m)
    {
        _isExtracted = true;
        _sourceImageUrl = sourceImageUrl;
        _extractionConfidence = confidence;
        return this;
    }

    public RecipeBuilder WithIngredient(string name, decimal? quantity = null, string? unit = null, string? quantityText = null)
    {
        _ingredients.Add(new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeId = _id,
            Name = name,
            QuantityValue = quantity,
            Unit = unit,
            Quantity = quantityText
        });
        return this;
    }

    public RecipeBuilder WithIngredient(Action<IngredientBuilder> configure)
    {
        var builder = new IngredientBuilder().WithRecipeId(_id);
        configure(builder);
        _ingredients.Add(builder.Build());
        return this;
    }

    public RecipeBuilder WithStep(int stepNumber, string instruction)
    {
        _steps.Add(new RecipeStep
        {
            Id = Guid.NewGuid(),
            RecipeId = _id,
            StepNumber = stepNumber,
            Instruction = instruction
        });
        return this;
    }

    public RecipeBuilder WithSteps(params string[] instructions)
    {
        for (int i = 0; i < instructions.Length; i++)
        {
            WithStep(i + 1, instructions[i]);
        }
        return this;
    }

    public RecipeBuilder WithRating(string userId, int rating, string? comments = null, string? frequencyPreference = null)
    {
        _ratings.Add(new RecipeRating
        {
            Id = Guid.NewGuid(),
            RecipeId = _id,
            UserId = userId,
            Rating = rating,
            Comments = comments,
            FrequencyPreference = frequencyPreference,
            RatedUtc = DateTime.UtcNow
        });
        return this;
    }

    public RecipeBuilder WithDefaultIngredients()
    {
        return WithIngredient("Salt", 1, "tsp", "1 tsp")
            .WithIngredient("Pepper", 0.5m, "tsp", "1/2 tsp")
            .WithIngredient("Olive Oil", 2, "tbsp", "2 tbsp");
    }

    public RecipeBuilder WithDefaultSteps()
    {
        return WithSteps(
            "Prepare all ingredients",
            "Cook according to recipe",
            "Serve and enjoy"
        );
    }

    /// <summary>
    /// Creates a complete recipe with ingredients and steps for comprehensive testing.
    /// </summary>
    public RecipeBuilder WithFullDetails()
    {
        return WithDefaultIngredients()
            .WithDefaultSteps();
    }

    public Recipe Build()
    {
        var recipe = new Recipe
        {
            Id = _id,
            Title = _title,
            Description = _description,
            CuisineType = _cuisineType,
            PrepTimeMinutes = _prepTimeMinutes,
            CookTimeMinutes = _cookTimeMinutes,
            Servings = _servings,
            ImageUrl = _imageUrl,
            CreatedBy = _createdBy,
            CreatedByUserId = _createdByUserId,
            CreatedUtc = _createdUtc,
            UpdatedUtc = _updatedUtc,
            IsExtracted = _isExtracted,
            SourceImageUrl = _sourceImageUrl,
            ExtractionConfidence = _extractionConfidence
        };

        foreach (var ingredient in _ingredients)
        {
            ingredient.RecipeId = _id;
            recipe.Ingredients.Add(ingredient);
        }

        foreach (var step in _steps)
        {
            step.RecipeId = _id;
            recipe.Steps.Add(step);
        }

        foreach (var rating in _ratings)
        {
            rating.RecipeId = _id;
            recipe.Ratings.Add(rating);
        }

        return recipe;
    }
}

/// <summary>
/// Fluent builder for creating RecipeIngredient entities in tests.
/// </summary>
public class IngredientBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _recipeId;
    private string _name = "Test Ingredient";
    private decimal? _quantityValue;
    private string? _unit;
    private string? _quantity;

    public static IngredientBuilder Create() => new();

    public IngredientBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public IngredientBuilder WithRecipeId(Guid recipeId)
    {
        _recipeId = recipeId;
        return this;
    }

    public IngredientBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public IngredientBuilder WithQuantity(decimal? value, string? unit = null, string? displayText = null)
    {
        _quantityValue = value;
        _unit = unit;
        _quantity = displayText ?? (value.HasValue && unit != null ? $"{value} {unit}" : null);
        return this;
    }

    public RecipeIngredient Build()
    {
        return new RecipeIngredient
        {
            Id = _id,
            RecipeId = _recipeId,
            Name = _name,
            QuantityValue = _quantityValue,
            Unit = _unit,
            Quantity = _quantity
        };
    }
}

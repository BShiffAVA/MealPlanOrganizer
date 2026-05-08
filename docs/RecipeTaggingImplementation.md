# Recipe Tagging (Hashtag) Feature — Implementation Guide

## Overview

Recipe tags are global, normalized hashtags that help users organize and filter recipes. Tags are shared across all households — there is no per-household ownership. Tags support autocomplete from existing tags, are inferred by AI during recipe extraction, and enable filtering on the recipe list.

## Architecture

### Tag Normalization Rules

Implemented in `TagHelper.Normalize()` (backend) and inline in `RecipeEditorViewModel.AddTag` (mobile):

- Lowercase
- Trim whitespace, strip leading `#`
- Replace spaces with `-`
- Remove all characters except `[a-z0-9-]`
- Truncate to 50 characters max

### Database Entities

**`RecipeTag`** (`src/MealPlanOrganizer.Functions/Data/Entities/RecipeTag.cs`)
- `Id` (Guid, PK)
- `Name` (string, unique, max 50)
- `CreatedUtc` (DateTime)
- No household FK — tags are global

**`RecipeTagAssignment`** (`src/MealPlanOrganizer.Functions/Data/Entities/RecipeTagAssignment.cs`)
- Composite PK: (`RecipeId`, `TagId`)
- Many-to-many join between `Recipe` and `RecipeTag`

**Migration:** `20260508143331_AddRecipeTags` (manually written — `dotnet ef` not available in this environment)

## Backend — Azure Functions

### `GET /tags?search={prefix}`

- File: `src/MealPlanOrganizer.Functions/Functions/GetTags.cs`
- Auth required
- Returns top 10 matching tags, ordered by usage count desc then alphabetically
- Response: `{ "tags": ["italian", "quick", ...] }`

### Create/Update Recipe Tag Handling

**`CreateRecipe`**: Upserts each tag (find by name or create), then creates `RecipeTagAssignment` rows.

**`UpdateRecipe`**: If `Tags` is non-null in the request, deletes all existing assignments with `ExecuteDeleteAsync` then re-adds. Null `Tags` = no change; empty list = remove all tags.

**`GetRecipeById`**: Includes `TagAssignments.RecipeTag`; tags returned in response as `string[]`.

**`ListRecipes`**: Loads entities with `.Include()` for tag access; supports `?tag=` query param to filter to recipes with a specific tag; tags included in card response.

## Mobile — MAUI

### Services

`IRecipeService.GetTagSuggestionsAsync(string prefix)` calls `GET /tags?search={prefix}` and returns `List<string>`.

### DTOs

All DTOs in `IRecipeService.cs` and `Models/UpdateRecipeDto.cs` have been updated with `List<string> Tags`.

### Models

**`RecipeCard`** — `Tags` + `TagsDisplay` (comma-joined); 8th optional constructor param.

**`ExtractedRecipe`** — `List<string> SuggestedTags` for AI-inferred tags.

### ViewModels

**`RecipeEditorViewModel`** (Add/Edit recipe)
- `Tags` — `ObservableCollection<string>` of currently added tags
- `TagInput` — bound to the tag Entry field; `OnTagInputChanged` fires autocomplete on each keystroke
- `TagSuggestions` — autocomplete results; `HasTagSuggestions` bool drives suggestions list visibility
- `AddTagCommand` — normalizes and adds `TagInput` to `Tags`
- `RemoveTagCommand` — removes a tag chip
- `SelectSuggestionCommand` — selects autocomplete suggestion
- `ClearForm` clears all tags

**`ExtractedRecipePreviewViewModel`** (AI import preview)
- `SuggestedTags` — populated from AI-extracted `recipe.SuggestedTags`
- `RemoveSuggestedTagCommand` — remove unwanted AI suggestions before saving
- Tags saved via `Tags = SuggestedTags.ToList()` in `SaveAsync`

**`MainViewModel`** (Recipe list)
- `TagOptions` — list of tags from loaded recipes for the filter Picker
- `SelectedTag` — filter value; `OnSelectedTagChanged` re-applies filters
- `ApplyFilters` — filters `Recipes` to those containing `SelectedTag`

**`RecipeDetailViewModel`** (View recipe)
- `Tags` — `ObservableCollection<string>` populated from recipe
- `HasTags` — bool, drives tag section visibility

### XAML UI

**`MainPage.xaml`** — Tag Picker added as 3rd row spanning both columns in the filter grid.

**`AddRecipePage.xaml` / `EditRecipePage.xaml`** — Tags section before Save button:
- Current tag chips (purple rounded badges) with ✕ tap to remove
- Tag Entry + Add button
- Autocomplete suggestions list (visible only when `HasTagSuggestions`)

**`RecipeDetailPage.xaml`** — Read-only tag chips section after Instructions, visible only when `HasTags`.

## AI Recipe Extraction

The system prompt in `RecipeExtractionService` instructs the LLM to return `suggestedTags` (array of normalized strings) in the extracted JSON. These populate `SuggestedTags` in `ExtractedRecipePreviewViewModel` for user review before saving.

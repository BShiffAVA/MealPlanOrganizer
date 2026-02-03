using System;
using System.Net;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MealPlanOrganizer.Functions.Data;

namespace MealPlanOrganizer.Functions.Functions;

public class GetRecipeById
{
    private readonly ILogger<GetRecipeById> _logger;
    private readonly AppDbContext _context;
    private readonly BlobContainerClient _containerClient;
    private readonly BlobServiceClient _blobServiceClient;

    public GetRecipeById(
        ILogger<GetRecipeById> logger,
        AppDbContext context,
        BlobContainerClient containerClient,
        BlobServiceClient blobServiceClient)
    {
        _logger = logger;
        _context = context;
        _containerClient = containerClient;
        _blobServiceClient = blobServiceClient;
    }

    [Function("GetRecipeById")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/{id:guid}")] HttpRequestData req,
        Guid id)
    {
        _logger.LogInformation("Getting recipe with ID: {RecipeId}", id);

        var recipe = await _context.Recipes
            .Include(r => r.Ingredients.OrderBy(i => i.Id))
            .Include(r => r.Steps.OrderBy(s => s.StepNumber))
            .Include(r => r.Ratings)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (recipe == null)
        {
            _logger.LogWarning("Recipe with ID {RecipeId} not found", id);
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new { message = $"Recipe with ID {id} not found" });
            return notFoundResponse;
        }

        var imageUrl = await NormalizeImageUrlAsync(recipe.ImageUrl);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            id = recipe.Id,
            title = recipe.Title,
            description = recipe.Description,
            cuisineType = recipe.CuisineType,
            prepTimeMinutes = recipe.PrepTimeMinutes,
            cookTimeMinutes = recipe.CookTimeMinutes,
            servings = recipe.Servings,
            imageUrl = imageUrl,
            createdBy = recipe.CreatedBy,
            createdUtc = recipe.CreatedUtc,
            averageRating = recipe.Ratings.Count > 0 ? recipe.Ratings.Average(r => r.Rating) : 0.0,
            ratingCount = recipe.Ratings.Count,
            ratings = recipe.Ratings
                .OrderByDescending(r => r.RatedUtc)
                .Select(r => new
                {
                    userId = r.UserId,
                    rating = r.Rating,
                    comments = r.Comments,
                    ratedUtc = r.RatedUtc
                })
                .ToList(),
            ingredients = recipe.Ingredients.Select(i => new
            {
                name = i.Name,
                quantity = i.Quantity
            }).ToList(),
            steps = recipe.Steps.Select(s => new
            {
                stepNumber = s.StepNumber,
                instruction = s.Instruction
            }).ToList()
        });

        return response;
    }

    private async Task<string?> NormalizeImageUrlAsync(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        var trimmed = imageUrl.Trim().Trim('"');

        // If it already has a SAS token, return as-is
        if (trimmed.Contains("sig=", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        // Try to extract blob name from a full URL
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var blobNameFromUrl = ExtractBlobName(uri);
            if (!string.IsNullOrWhiteSpace(blobNameFromUrl))
            {
                var blobClient = _containerClient.GetBlobClient(blobNameFromUrl);
                return await GenerateSasUrlAsync(blobClient, BlobSasPermissions.Read, TimeSpan.FromDays(30));
            }

            return trimmed;
        }

        // If it's already a blob name, generate SAS
        var blobClientFromName = _containerClient.GetBlobClient(trimmed);
        return await GenerateSasUrlAsync(blobClientFromName, BlobSasPermissions.Read, TimeSpan.FromDays(30));
    }

    private string? ExtractBlobName(Uri uri)
    {
        var path = uri.AbsolutePath.TrimStart('/');
        var containerPrefix = _containerClient.Name + "/";

        if (path.StartsWith(containerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return path.Substring(containerPrefix.Length);
        }

        return null;
    }

    private async Task<string> GenerateSasUrlAsync(BlobClient blobClient, BlobSasPermissions permissions, TimeSpan expiry)
    {
        if (blobClient.CanGenerateSasUri)
        {
            var sasBuilder = new BlobSasBuilder(permissions, DateTimeOffset.UtcNow.Add(expiry))
            {
                BlobContainerName = blobClient.BlobContainerName,
                BlobName = blobClient.Name
            };

            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            return sasUri.ToString();
        }

        // Use user delegation SAS when using Azure AD (managed identity)
        var startsOn = DateTimeOffset.UtcNow.AddMinutes(-5);
        var requestedExpiresOn = DateTimeOffset.UtcNow.Add(expiry);
        var maxExpiresOn = DateTimeOffset.UtcNow.AddDays(7);
        var expiresOn = requestedExpiresOn > maxExpiresOn ? maxExpiresOn : requestedExpiresOn;

        var delegationKey = await _blobServiceClient.GetUserDelegationKeyAsync(startsOn, expiresOn);

        var sasBuilderWithDelegation = new BlobSasBuilder
        {
            BlobContainerName = blobClient.BlobContainerName,
            BlobName = blobClient.Name,
            Resource = "b",
            StartsOn = startsOn,
            ExpiresOn = expiresOn,
            Protocol = SasProtocol.Https
        };

        sasBuilderWithDelegation.SetPermissions(permissions);

        var sasToken = sasBuilderWithDelegation
            .ToSasQueryParameters(delegationKey.Value, _blobServiceClient.AccountName)
            .ToString();

        return $"{blobClient.Uri}?{sasToken}";
    }
}

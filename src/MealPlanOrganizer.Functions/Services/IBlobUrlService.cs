using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Azure.Storage;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Functions.Services;

/// <summary>
/// Service for generating SAS URLs for blob storage access.
/// </summary>
public interface IBlobUrlService
{
    /// <summary>
    /// Normalizes an image URL by generating a SAS token if needed.
    /// </summary>
    /// <param name="imageUrl">The raw image URL stored in the database.</param>
    /// <returns>A URL with SAS token for authorized access, or null if input is empty.</returns>
    Task<string?> NormalizeImageUrlAsync(string? imageUrl);
}

/// <summary>
/// Implementation of IBlobUrlService that generates SAS URLs for Azure Blob Storage and Azurite.
/// </summary>
public class BlobUrlService : IBlobUrlService
{
    private readonly BlobContainerClient _containerClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobUrlService> _logger;

    public BlobUrlService(BlobContainerClient containerClient, BlobServiceClient blobServiceClient, ILogger<BlobUrlService> logger)
    {
        _containerClient = containerClient;
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<string?> NormalizeImageUrlAsync(string? imageUrl)
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
                return await GenerateSasUrlAsync(blobClient, BlobSasPermissions.Read, TimeSpan.FromDays(7));
            }

            return trimmed;
        }

        // If it's already a blob name, generate SAS
        var blobClientFromName = _containerClient.GetBlobClient(trimmed);
        return await GenerateSasUrlAsync(blobClientFromName, BlobSasPermissions.Read, TimeSpan.FromDays(7));
    }

    private string? ExtractBlobName(Uri uri)
    {
        var path = uri.AbsolutePath.TrimStart('/');
        var containerPrefix = _containerClient.Name + "/";

        // Look for the container name anywhere in the path (handles both Azure URLs and Azurite with account name)
        var containerIndex = path.IndexOf(containerPrefix, StringComparison.OrdinalIgnoreCase);
        if (containerIndex >= 0)
        {
            var blobName = path.Substring(containerIndex + containerPrefix.Length);
            _logger.LogDebug("Extracted blob name '{BlobName}' from URI: {Uri}", blobName, uri);
            return blobName;
        }

        _logger.LogWarning("Could not extract blob name from URI: {Uri}, container: {Container}", uri, _containerClient.Name);
        return null;
    }

    private async Task<string> GenerateSasUrlAsync(BlobClient blobClient, BlobSasPermissions permissions, TimeSpan expiry)
    {
        // Try shared key SAS first (works for both Azurite and Azure with connection string)
        if (blobClient.CanGenerateSasUri)
        {
            try
            {
                _logger.LogDebug("BlobClient has SAS generation capability, account: {AccountName}", blobClient.AccountName);
                var sasBuilder = new BlobSasBuilder(permissions, DateTimeOffset.UtcNow.Add(expiry))
                {
                    BlobContainerName = blobClient.BlobContainerName,
                    BlobName = blobClient.Name
                };

                var sasUri = blobClient.GenerateSasUri(sasBuilder);
                _logger.LogInformation("Generated shared key SAS URL for blob: {BlobName}, URL: {SasUrl}", blobClient.Name, sasUri);
                return sasUri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate shared key SAS using CanGenerateSasUri, will try user delegation");
            }
        }
        else
        {
            _logger.LogDebug("BlobClient does not support SAS generation (CanGenerateSasUri=false), account: {AccountName}", blobClient.AccountName);
        }

        // Use user delegation SAS when using Azure AD (managed identity)
        try
        {
            _logger.LogDebug("Attempting to generate user delegation SAS");
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

            var urlWithSas = $"{blobClient.Uri}?{sasToken}";
            _logger.LogInformation("Generated user delegation SAS URL for blob: {BlobName}", blobClient.Name);
            return urlWithSas;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate user delegation SAS for blob: {BlobName}", blobClient.Name);
        }

        // Last resort: return the plain blob URI (will likely fail without public access)
        var plainUrl = blobClient.Uri.ToString();
        _logger.LogWarning("Returning plain URL without SAS for blob: {BlobName}, Uri: {Uri}", blobClient.Name, plainUrl);
        return plainUrl;
    }
}

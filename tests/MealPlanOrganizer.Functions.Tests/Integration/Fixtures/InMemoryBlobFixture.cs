using System.Collections.Concurrent;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MealPlanOrganizer.Functions.Services;
using Xunit;

namespace MealPlanOrganizer.Functions.Tests.Integration.Fixtures;

/// <summary>
/// In-memory blob storage fixture for integration tests.
/// No Docker required - stores blobs in memory using a dictionary.
/// </summary>
public class InMemoryBlobFixture : IAsyncLifetime
{
    public const string ContainerName = "recipe-images";
    
    /// <summary>
    /// In-memory blob storage - maps blob name to (content, contentType).
    /// </summary>
    public ConcurrentDictionary<string, (byte[] Content, string ContentType)> Blobs { get; } = new();
    
    /// <summary>
    /// Gets the in-memory blob service for dependency injection.
    /// </summary>
    public InMemoryBlobService BlobService { get; private set; } = null!;
    
    public Task InitializeAsync()
    {
        BlobService = new InMemoryBlobService(Blobs);
        
        // Seed test images
        SeedTestImages();
        
        return Task.CompletedTask;
    }
    
    public Task DisposeAsync()
    {
        Blobs.Clear();
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Seeds the in-memory storage with sample test images.
    /// </summary>
    private void SeedTestImages()
    {
        // Upload a simple test image (1x1 pixel PNG)
        var testImageBytes = Convert.FromBase64String(TestImages.OnePxPngBase64);
        
        Blobs["test-image-1.png"] = (testImageBytes, "image/png");
        Blobs[$"recipes/{TestData.Recipe1Id}/image.png"] = (testImageBytes, "image/png");
    }
    
    /// <summary>
    /// Clears all blobs and re-seeds with default test data.
    /// </summary>
    public Task ClearContainerAsync()
    {
        Blobs.Clear();
        SeedTestImages();
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Uploads a test image and returns its blob name.
    /// </summary>
    public string UploadTestImage(string? blobName = null)
    {
        blobName ??= $"test-{Guid.NewGuid()}.png";
        var testImageBytes = Convert.FromBase64String(TestImages.OnePxPngBase64);
        Blobs[blobName] = (testImageBytes, "image/png");
        return blobName;
    }
}

/// <summary>
/// In-memory blob service that implements blob storage operations without Azure SDK.
/// </summary>
public class InMemoryBlobService : IBlobUrlService
{
    private readonly ConcurrentDictionary<string, (byte[] Content, string ContentType)> _blobs;
    
    public InMemoryBlobService(ConcurrentDictionary<string, (byte[] Content, string ContentType)> blobs)
    {
        _blobs = blobs;
    }
    
    /// <summary>
    /// Uploads a blob to in-memory storage.
    /// </summary>
    public Task UploadBlobAsync(string blobName, Stream content, string contentType)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        _blobs[blobName] = (ms.ToArray(), contentType);
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Downloads a blob from in-memory storage.
    /// </summary>
    public Task<Stream?> DownloadBlobAsync(string blobName)
    {
        if (_blobs.TryGetValue(blobName, out var blob))
        {
            return Task.FromResult<Stream?>(new MemoryStream(blob.Content));
        }
        return Task.FromResult<Stream?>(null);
    }
    
    /// <summary>
    /// Checks if a blob exists.
    /// </summary>
    public bool BlobExists(string blobName) => _blobs.ContainsKey(blobName);
    
    /// <summary>
    /// Deletes a blob.
    /// </summary>
    public Task DeleteBlobAsync(string blobName)
    {
        _blobs.TryRemove(blobName, out _);
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Lists all blob names.
    /// </summary>
    public IEnumerable<string> ListBlobs() => _blobs.Keys;
    
    // IBlobUrlService implementation
    
    /// <summary>
    /// For in-memory tests, just return the URL as-is or generate a fake SAS URL.
    /// </summary>
    public Task<string?> NormalizeImageUrlAsync(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return Task.FromResult<string?>(null);
        
        // If it's a blob name (not a full URL), generate a fake URL
        if (!imageUrl.StartsWith("http"))
        {
            return Task.FromResult<string?>($"https://test.blob.core.windows.net/{InMemoryBlobFixture.ContainerName}/{imageUrl}?sv=test-sas");
        }
        
        return Task.FromResult<string?>(imageUrl);
    }
    
    /// <summary>
    /// Generates a fake upload URL for testing.
    /// </summary>
    public Task<(string BlobName, string UploadUrl)> GenerateUploadUrlAsync(string fileName, string contentType)
    {
        var blobName = $"recipes/{Guid.NewGuid()}/{fileName}";
        var uploadUrl = $"https://test.blob.core.windows.net/{InMemoryBlobFixture.ContainerName}/{blobName}?sv=test-sas&sig=test";
        return Task.FromResult((blobName, uploadUrl));
    }
}

/// <summary>
/// Test image data for blob storage tests.
/// </summary>
public static class TestImages
{
    /// <summary>
    /// Base64-encoded 1x1 pixel transparent PNG.
    /// </summary>
    public const string OnePxPngBase64 = 
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
    
    /// <summary>
    /// Base64-encoded sample recipe image (small JPEG) for extraction tests.
    /// This is a minimal valid JPEG file.
    /// </summary>
    public const string SampleRecipeJpegBase64 =
        "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/wAALCAABAAEBAREA/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/9oACAEBAAA/AP/Z";
}

/// <summary>
/// xUnit collection definition for tests sharing the blob fixture.
/// </summary>
[CollectionDefinition("Blob")]
public class BlobCollection : ICollectionFixture<InMemoryBlobFixture>
{
}

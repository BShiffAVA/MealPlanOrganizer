using FluentAssertions;
using MealPlanOrganizer.Functions.Services;
using Xunit;

namespace MealPlanOrganizer.Functions.Tests.Unit.Services;

/// <summary>
/// Unit tests for BlobUrlService.
/// 
/// Note: BlobContainerClient and BlobServiceClient are sealed classes from the Azure SDK.
/// Full integration tests require Azurite or Azure Blob Storage.
/// These tests focus on the interface contract and behavior that can be tested in isolation.
/// </summary>
public class BlobUrlServiceTests
{
    #region Interface Contract Tests

    [Fact]
    public async Task NormalizeImageUrlAsync_WithNull_ReturnsNull()
    {
        // Arrange
        var service = CreateServiceWithAzuriteClient();

        // Act
        var result = await service.NormalizeImageUrlAsync(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task NormalizeImageUrlAsync_WithEmptyString_ReturnsNull()
    {
        // Arrange
        var service = CreateServiceWithAzuriteClient();

        // Act
        var result = await service.NormalizeImageUrlAsync("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task NormalizeImageUrlAsync_WithWhitespace_ReturnsNull()
    {
        // Arrange
        var service = CreateServiceWithAzuriteClient();

        // Act
        var result = await service.NormalizeImageUrlAsync("   ");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task NormalizeImageUrlAsync_WithUrlContainingSasToken_ReturnsUrlAsIs()
    {
        // Arrange
        var service = CreateServiceWithAzuriteClient();
        var urlWithSas = "https://account.blob.core.windows.net/container/blob.jpg?sig=abc123&se=2024-01-01";

        // Act
        var result = await service.NormalizeImageUrlAsync(urlWithSas);

        // Assert
        result.Should().Be(urlWithSas);
    }

    [Fact]
    public async Task NormalizeImageUrlAsync_WithUrlContainingSig_ReturnsUrlAsIs()
    {
        // Arrange
        var service = CreateServiceWithAzuriteClient();
        var urlWithSig = "https://example.com/image.png?sig=signature-value";

        // Act
        var result = await service.NormalizeImageUrlAsync(urlWithSig);

        // Assert
        result.Should().Be(urlWithSig);
    }

    [Fact]
    public async Task NormalizeImageUrlAsync_WithQuotedUrl_TrimsQuotes()
    {
        // Arrange
        var service = CreateServiceWithAzuriteClient();
        var quotedUrlWithSas = "\"https://account.blob.core.windows.net/container/blob.jpg?sig=abc123\"";

        // Act
        var result = await service.NormalizeImageUrlAsync(quotedUrlWithSas);

        // Assert
        result.Should().NotContain("\"");
    }

    [Fact]
    public async Task NormalizeImageUrlAsync_WithWhitespaceAroundUrl_TrimsWhitespace()
    {
        // Arrange
        var service = CreateServiceWithAzuriteClient();
        var paddedUrlWithSas = "  https://account.blob.core.windows.net/container/blob.jpg?sig=abc123  ";

        // Act
        var result = await service.NormalizeImageUrlAsync(paddedUrlWithSas);

        // Assert
        result.Should().NotStartWith(" ");
        result.Should().NotEndWith(" ");
    }

    #endregion

    #region Integration Tests (Require Azurite)

    /// <summary>
    /// Integration tests that require Azurite running on default port.
    /// These tests are skipped by default and should be run as part of integration test suite.
    /// </summary>
    [Trait("Category", "Integration")]
    public class BlobUrlServiceIntegrationTests
    {
        private const string AzuriteConnectionString = "UseDevelopmentStorage=true";
        private const string ContainerName = "recipe-images";

        [Fact(Skip = "Requires Azurite running")]
        public async Task NormalizeImageUrlAsync_WithBlobName_GeneratesSasUrl()
        {
            // Arrange - requires Azurite on localhost:10000
            var service = CreateIntegrationService();

            // Act
            var result = await service.NormalizeImageUrlAsync("test-blob.jpg");

            // Assert
            result.Should().Contain("sig=");
            result.Should().Contain("test-blob.jpg");
        }

        [Fact(Skip = "Requires Azurite running")]
        public async Task NormalizeImageUrlAsync_WithAzuriteUrl_GeneratesSasUrl()
        {
            // Arrange
            var service = CreateIntegrationService();
            var azuriteUrl = $"http://127.0.0.1:10000/devstoreaccount1/{ContainerName}/sample-image.jpg";

            // Act
            var result = await service.NormalizeImageUrlAsync(azuriteUrl);

            // Assert
            result.Should().Contain("sig="); // SAS signature present
        }

        [Fact(Skip = "Requires Azurite running")]
        public async Task NormalizeImageUrlAsync_SasUrlExpiresInFuture()
        {
            // Arrange
            var service = CreateIntegrationService();

            // Act
            var result = await service.NormalizeImageUrlAsync("test-image.png");

            // Assert
            result.Should().Contain("se="); // SAS expiry present
            
            // Parse expiry and verify it's in the future
            var uri = new Uri(result!);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var expiry = query["se"];
            expiry.Should().NotBeNullOrEmpty();
            
            // Expiry should be parsable and in the future
            DateTimeOffset.TryParse(expiry, out var expiryDate).Should().BeTrue();
            expiryDate.Should().BeAfter(DateTimeOffset.UtcNow);
        }

        private static BlobUrlService CreateIntegrationService()
        {
            var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(AzuriteConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<BlobUrlService>();
            
            return new BlobUrlService(containerClient, blobServiceClient, logger);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a BlobUrlService using the Azurite development storage connection string.
    /// The service can test null/empty handling without actually connecting.
    /// </summary>
    private static BlobUrlService CreateServiceWithAzuriteClient()
    {
        // Use development storage connection string - service won't connect until a blob operation is attempted
        var connectionString = "UseDevelopmentStorage=true";
        var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient("recipe-images");
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<BlobUrlService>();
        
        return new BlobUrlService(containerClient, blobServiceClient, logger);
    }

    #endregion
}

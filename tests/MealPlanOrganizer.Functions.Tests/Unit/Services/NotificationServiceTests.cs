using FluentAssertions;
using MealPlanOrganizer.Functions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MealPlanOrganizer.Functions.Tests.Unit.Services;

/// <summary>
/// Unit tests for the NotificationService.
/// Tests configuration validation, graceful degradation when not configured,
/// and notification payload generation.
/// </summary>
public class NotificationServiceTests
{
    private readonly Mock<ILogger<NotificationService>> _loggerMock;

    public NotificationServiceTests()
    {
        _loggerMock = new Mock<ILogger<NotificationService>>();
    }

    #region Constructor / Configuration Tests

    [Fact]
    public void Constructor_WithMissingConnectionString_DisablesNotifications()
    {
        // Arrange
        var configuration = CreateConfiguration(connectionString: null, hubName: "test-hub");

        // Act
        var service = new NotificationService(configuration, _loggerMock.Object);

        // Assert - Should not throw, service should be created but disabled
        service.Should().NotBeNull();
        
        // Verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithMissingHubName_DisablesNotifications()
    {
        // Arrange
        var configuration = CreateConfiguration(connectionString: "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test", hubName: null);

        // Act
        var service = new NotificationService(configuration, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithEmptyConnectionString_DisablesNotifications()
    {
        // Arrange
        var configuration = CreateConfiguration(connectionString: "", hubName: "test-hub");

        // Act
        var service = new NotificationService(configuration, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithWhitespaceHubName_DisablesNotifications()
    {
        // Arrange
        var configuration = CreateConfiguration(connectionString: "test-connection", hubName: "   ");

        // Act
        var service = new NotificationService(configuration, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region RegisterDeviceAsync Tests

    [Fact]
    public async Task RegisterDeviceAsync_WhenNotConfigured_ReturnsNull()
    {
        // Arrange
        var configuration = CreateConfiguration(connectionString: null, hubName: null);
        var service = new NotificationService(configuration, _loggerMock.Object);

        // Act
        var result = await service.RegisterDeviceAsync("installationId",Guid.NewGuid(), "ios", "test-token");

        // Assert
        result.Should().BeNull();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("skipping device registration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("ios")]
    [InlineData("android")]
    [InlineData("windows")]
    public async Task RegisterDeviceAsync_WithValidPlatform_WhenNotConfigured_ReturnsNull(string platform)
    {
        // Arrange
        var configuration = CreateConfiguration(connectionString: null, hubName: null);
        var service = new NotificationService(configuration, _loggerMock.Object);

        // Act
        var result = await service.RegisterDeviceAsync("installationIdNOTFOUND", Guid.NewGuid(), platform, "test-token");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region UnregisterDeviceAsync Tests

    [Fact]
    public async Task UnregisterDeviceAsync_WhenNotConfigured_DoesNotThrow()
    {
        // Arrange
        var configuration = CreateConfiguration(connectionString: null, hubName: null);
        var service = new NotificationService(configuration, _loggerMock.Object);

        // Act
        var act = () => service.UnregisterDeviceAsync("test-registration-id");

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region SendToUserAsync Tests

    [Fact]
    public async Task SendToUserAsync_WhenNotConfigured_DoesNotThrow()
    {
        // Arrange
        var configuration = CreateConfiguration(connectionString: null, hubName: null);
        var service = new NotificationService(configuration, _loggerMock.Object);
        var userId = Guid.NewGuid();

        // Act
        var act = () => service.SendToUserAsync(userId, "Test Title", "Test Body");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendToUserAsync_WhenNotConfigured_LogsWarning()
    {
        // Arrange
        var configuration = CreateConfiguration(connectionString: null, hubName: null);
        var service = new NotificationService(configuration, _loggerMock.Object);
        var userId = Guid.NewGuid();

        // Reset logger mock for this test
        _loggerMock.Invocations.Clear();

        // Act
        await service.SendToUserAsync(userId, "Test Title", "Test Body");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("skipping notification to user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendToUserAsync_WithData_WhenNotConfigured_DoesNotThrow()
    {
        // Arrange
        var configuration = CreateConfiguration(connectionString: null, hubName: null);
        var service = new NotificationService(configuration, _loggerMock.Object);
        var userId = Guid.NewGuid();
        var data = new Dictionary<string, string>
        {
            ["action"] = "rate_recipe",
            ["recipeId"] = Guid.NewGuid().ToString()
        };

        // Act
        var act = () => service.SendToUserAsync(userId, "Test Title", "Test Body", data);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region SendToHouseholdAsync Tests

    [Fact]
    public async Task SendToHouseholdAsync_WhenNotConfigured_DoesNotThrow()
    {
        // Arrange
        var configuration = CreateConfiguration(connectionString: null, hubName: null);
        var service = new NotificationService(configuration, _loggerMock.Object);
        var householdId = Guid.NewGuid();

        // Act
        var act = () => service.SendToHouseholdAsync(householdId, "Test Title", "Test Body");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendToHouseholdAsync_WhenNotConfigured_LogsWarning()
    {
        // Arrange
        var configuration = CreateConfiguration(connectionString: null, hubName: null);
        var service = new NotificationService(configuration, _loggerMock.Object);
        var householdId = Guid.NewGuid();

        // Reset logger mock for this test
        _loggerMock.Invocations.Clear();

        // Act
        await service.SendToHouseholdAsync(householdId, "Test Title", "Test Body");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("skipping notification to household")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region INotificationService Interface Tests

    [Fact]
    public void NotificationService_ImplementsInterface()
    {
        // Arrange
        var configuration = CreateConfiguration(connectionString: null, hubName: null);

        // Act
        var service = new NotificationService(configuration, _loggerMock.Object);

        // Assert
        service.Should().BeAssignableTo<INotificationService>();
    }

    #endregion

    #region Helper Methods

    private static IConfiguration CreateConfiguration(string? connectionString, string? hubName)
    {
        var configData = new Dictionary<string, string?>();
        
        if (connectionString != null)
        {
            configData["NotificationHub:ConnectionString"] = connectionString;
        }
        
        if (hubName != null)
        {
            configData["NotificationHub:HubName"] = hubName;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();
    }

    #endregion
}

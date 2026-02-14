using System.Net;
using System.Text.Json;
using MealPlanOrganizer.Functions.Data;
using MealPlanOrganizer.Functions.Data.Entities;
using MealPlanOrganizer.Functions.Functions;
using MealPlanOrganizer.Functions.Services;
using MealPlanOrganizer.Functions.Tests.Integration.Fixtures;
using MealPlanOrganizer.Functions.Tests.Integration.Helpers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MealPlanOrganizer.Functions.Tests.Integration.Endpoints;

/// <summary>
/// Integration tests for Device Registration endpoints.
/// Tests RegisterDevice and UnregisterDevice operations.
/// </summary>
[Collection("Integration")]
public class DeviceRegistrationEndpointsTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private AppDbContext _db = null!;
    private Mock<INotificationService> _notificationServiceMock = null!;

    public DeviceRegistrationEndpointsTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _db = _fixture.TestHost.CreateDbContext();
        await _fixture.TestHost.ResetDatabaseAsync();
        _notificationServiceMock = new Mock<INotificationService>();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    #region RegisterDevice Tests

    [Fact]
    public async Task RegisterDevice_WithValidRequest_RegistersDevice()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<RegisterDevice>();

        _notificationServiceMock
            .Setup(x => x.RegisterDeviceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("test-registration-id");

        var function = new RegisterDevice(logger, db, authHelper, _notificationServiceMock.Object);

        var user = await CreateUserAsync(db);

        var request = new { platform = "ios", pushToken = "test-push-token-12345" };
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify device was saved
        await using var verifyDb = _fixture.TestHost.CreateDbContext();
        var savedDevice = await verifyDb.DeviceRegistrations
            .FirstOrDefaultAsync(d => d.UserId == user.Id && d.Platform == "ios");
        Assert.NotNull(savedDevice);
        Assert.Equal("test-push-token-12345", savedDevice.PushToken);
        Assert.True(savedDevice.IsActive);
    }

    [Theory]
    [InlineData("ios")]
    [InlineData("android")]
    [InlineData("windows")]
    public async Task RegisterDevice_WithAllPlatforms_Succeeds(string platform)
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<RegisterDevice>();

        _notificationServiceMock
            .Setup(x => x.RegisterDeviceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("test-registration-id");

        var function = new RegisterDevice(logger, db, authHelper, _notificationServiceMock.Object);

        var user = await CreateUserAsync(db);

        var request = new { platform = platform, pushToken = $"token-{platform}" };
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDevice_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<RegisterDevice>();

        var function = new RegisterDevice(logger, db, authHelper, _notificationServiceMock.Object);

        var request = new { platform = "ios", pushToken = "test-token" };
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            authHeader: null);

        // Act
        var response = await function.Run(httpRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDevice_WithInvalidPlatform_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<RegisterDevice>();

        var function = new RegisterDevice(logger, db, authHelper, _notificationServiceMock.Object);

        var user = await CreateUserAsync(db);

        var request = new { platform = "invalid-platform", pushToken = "test-token" };
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDevice_WithMissingPushToken_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<RegisterDevice>();

        var function = new RegisterDevice(logger, db, authHelper, _notificationServiceMock.Object);

        var user = await CreateUserAsync(db);

        var request = new { platform = "ios" }; // Missing pushToken
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDevice_WithMissingPlatform_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<RegisterDevice>();

        var function = new RegisterDevice(logger, db, authHelper, _notificationServiceMock.Object);

        var user = await CreateUserAsync(db);

        var request = new { pushToken = "test-token" }; // Missing platform
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDevice_UpdatesExistingDevice()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<RegisterDevice>();

        _notificationServiceMock
            .Setup(x => x.RegisterDeviceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("updated-registration-id");

        var function = new RegisterDevice(logger, db, authHelper, _notificationServiceMock.Object);

        var user = await CreateUserAsync(db);

        // Create existing registration
        var existingDevice = new DeviceRegistration
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "ios",
            PushToken = "old-token",
            NotificationHubRegistrationId = "old-registration-id",
            CreatedUtc = DateTime.UtcNow.AddDays(-1),
            IsActive = true
        };
        db.DeviceRegistrations.Add(existingDevice);
        await db.SaveChangesAsync();

        // Register same device with new token
        var request = new { platform = "ios", pushToken = "old-token" }; // Same token
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verifyDb = _fixture.TestHost.CreateDbContext();
        var updatedDevice = await verifyDb.DeviceRegistrations.FindAsync(existingDevice.Id);
        Assert.NotNull(updatedDevice);
        Assert.Equal("updated-registration-id", updatedDevice.NotificationHubRegistrationId);
    }

    [Fact]
    public async Task RegisterDevice_CallsNotificationService()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<RegisterDevice>();

        _notificationServiceMock
            .Setup(x => x.RegisterDeviceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("test-registration-id");

        var function = new RegisterDevice(logger, db, authHelper, _notificationServiceMock.Object);

        var user = await CreateUserAsync(db);

        var request = new { platform = "android", pushToken = "fcm-token-12345" };
        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(
            HttpMethod.Post,
            JsonSerializer.Serialize(request),
            $"Bearer {token}");

        // Act
        await function.Run(httpRequest);

        // Assert
        _notificationServiceMock.Verify(
            x => x.RegisterDeviceAsync(user.Id, "android", "fcm-token-12345"),
            Times.Once);
    }

    #endregion

    #region UnregisterDevice Tests

    [Fact]
    public async Task UnregisterDevice_WithValidRequest_UnregistersDevice()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<UnregisterDevice>();

        var function = new UnregisterDevice(logger, db, authHelper, _notificationServiceMock.Object);

        var user = await CreateUserAsync(db);

        // Create existing registration
        var device = new DeviceRegistration
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "ios",
            PushToken = "test-token",
            NotificationHubRegistrationId = "hub-registration-id",
            CreatedUtc = DateTime.UtcNow,
            IsActive = true
        };
        db.DeviceRegistrations.Add(device);
        await db.SaveChangesAsync();

        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Delete, authHeader: $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest, "ios");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verifyDb = _fixture.TestHost.CreateDbContext();
        var updatedDevice = await verifyDb.DeviceRegistrations.FindAsync(device.Id);
        Assert.NotNull(updatedDevice);
        Assert.False(updatedDevice.IsActive);
    }

    [Fact]
    public async Task UnregisterDevice_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<UnregisterDevice>();

        var function = new UnregisterDevice(logger, db, authHelper, _notificationServiceMock.Object);

        var httpRequest = CreateMockHttpRequest(HttpMethod.Delete, authHeader: null);

        // Act
        var response = await function.Run(httpRequest, "ios");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnregisterDevice_NoRegistrations_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<UnregisterDevice>();

        var function = new UnregisterDevice(logger, db, authHelper, _notificationServiceMock.Object);

        var user = await CreateUserAsync(db);

        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Delete, authHeader: $"Bearer {token}");

        // Act
        var response = await function.Run(httpRequest, "ios");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnregisterDevice_CallsNotificationService()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<UnregisterDevice>();

        var function = new UnregisterDevice(logger, db, authHelper, _notificationServiceMock.Object);

        var user = await CreateUserAsync(db);

        // Create existing registration
        var device = new DeviceRegistration
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "android",
            PushToken = "test-token",
            NotificationHubRegistrationId = "hub-reg-to-unregister",
            CreatedUtc = DateTime.UtcNow,
            IsActive = true
        };
        db.DeviceRegistrations.Add(device);
        await db.SaveChangesAsync();

        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Delete, authHeader: $"Bearer {token}");

        // Act
        await function.Run(httpRequest, "android");

        // Assert
        _notificationServiceMock.Verify(
            x => x.UnregisterDeviceAsync("hub-reg-to-unregister"),
            Times.Once);
    }

    [Fact]
    public async Task UnregisterDevice_UnregistersOnlySpecifiedPlatform()
    {
        // Arrange
        using var scope = _fixture.TestHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authHelper = scope.ServiceProvider.GetRequiredService<AuthenticationHelper>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<UnregisterDevice>();

        var function = new UnregisterDevice(logger, db, authHelper, _notificationServiceMock.Object);

        var user = await CreateUserAsync(db);

        // Create iOS registration
        var iosDevice = new DeviceRegistration
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "ios",
            PushToken = "ios-token",
            CreatedUtc = DateTime.UtcNow,
            IsActive = true
        };
        
        // Create Android registration
        var androidDevice = new DeviceRegistration
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "android",
            PushToken = "android-token",
            CreatedUtc = DateTime.UtcNow,
            IsActive = true
        };

        db.DeviceRegistrations.AddRange(iosDevice, androidDevice);
        await db.SaveChangesAsync();

        var token = TestAuthHandler.CreateToken(user.ExternalIdObjectId);
        var httpRequest = CreateMockHttpRequest(HttpMethod.Delete, authHeader: $"Bearer {token}");

        // Act - Unregister only iOS
        var response = await function.Run(httpRequest, "ios");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verifyDb = _fixture.TestHost.CreateDbContext();
        var iosUpdated = await verifyDb.DeviceRegistrations.FindAsync(iosDevice.Id);
        var androidUpdated = await verifyDb.DeviceRegistrations.FindAsync(androidDevice.Id);

        Assert.NotNull(iosUpdated);
        Assert.False(iosUpdated.IsActive); // iOS should be inactive

        Assert.NotNull(androidUpdated);
        Assert.True(androidUpdated.IsActive); // Android should still be active
    }

    #endregion

    #region Helper Methods

    private static HttpRequestData CreateMockHttpRequest(
        HttpMethod method,
        string? body = null,
        string? authHeader = null)
    {
        return MockHttpFactory.CreateRequest(method, "http://localhost/api/devices", body, authHeader);
    }

    private static async Task<T?> ReadResponseBody<T>(HttpResponseData response)
    {
        return await MockHttpFactory.ReadResponseBodyAsync<T>(response);
    }

    private async Task<User> CreateUserAsync(AppDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalIdObjectId = Guid.NewGuid().ToString(),
            Email = $"test-{Guid.NewGuid()}@test.com",
            DisplayName = "Test User"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    #endregion
}

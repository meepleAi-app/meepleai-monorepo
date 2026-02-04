using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Unit tests for GetUserDevicesQueryHandler.
/// Issue #3340: Login device tracking and management.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class GetUserDevicesQueryHandlerTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly GetUserDevicesQueryHandler _handler;
    private readonly Guid _testUserId = Guid.NewGuid();

    public GetUserDevicesQueryHandlerTests()
    {
        _mockSessionRepository = new Mock<ISessionRepository>();
        _handler = new GetUserDevicesQueryHandler(_mockSessionRepository.Object);
    }

    private Session CreateTestSession(
        Guid userId,
        string? userAgent = null,
        string? ipAddress = null,
        DateTime? lastSeenAt = null)
    {
        var sessionToken = SessionToken.Generate();
        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken,
            lifetime: TimeSpan.FromDays(30),
            ipAddress: ipAddress,
            userAgent: userAgent
        );

        if (lastSeenAt.HasValue)
        {
            session.UpdateLastSeen(TimeProvider.System);
        }

        return session;
    }

    [Fact]
    public async Task Handle_UserWithSessions_ReturnsDeviceList()
    {
        // Arrange
        var sessions = new List<Session>
        {
            CreateTestSession(_testUserId, "Mozilla/5.0 (Windows NT 10.0) Chrome/120.0.0.0", "192.168.1.1"),
            CreateTestSession(_testUserId, "Mozilla/5.0 (iPhone) Safari/605.1.15", "10.0.0.1")
        };

        _mockSessionRepository
            .Setup(r => r.GetActiveSessionsByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetUserDevicesQuery(_testUserId);

        // Act
        var devices = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, devices.Count);
        Assert.Contains(devices, d => d.Browser == "Chrome" && d.OperatingSystem == "Windows");
        Assert.Contains(devices, d => d.Browser == "Safari" && d.DeviceType == "Phone");
    }

    [Fact]
    public async Task Handle_UserWithNoSessions_ReturnsEmptyList()
    {
        // Arrange
        _mockSessionRepository
            .Setup(r => r.GetActiveSessionsByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Session>());

        var query = new GetUserDevicesQuery(_testUserId);

        // Act
        var devices = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Empty(devices);
    }

    [Fact]
    public async Task Handle_WithCurrentSessionId_MarksCurrentDevice()
    {
        // Arrange
        var currentSession = CreateTestSession(_testUserId, "Mozilla/5.0 Chrome/120.0.0.0", "192.168.1.1");
        var otherSession = CreateTestSession(_testUserId, "Mozilla/5.0 Safari/605.1.15", "10.0.0.1");

        var sessions = new List<Session> { currentSession, otherSession };

        _mockSessionRepository
            .Setup(r => r.GetActiveSessionsByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetUserDevicesQuery(_testUserId, CurrentSessionId: currentSession.Id);

        // Act
        var devices = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, devices.Count);
        var currentDevice = devices.First(d => d.Id == currentSession.Id);
        var otherDevice = devices.First(d => d.Id == otherSession.Id);

        Assert.True(currentDevice.IsCurrentDevice);
        Assert.False(otherDevice.IsCurrentDevice);
    }

    [Fact]
    public async Task Handle_CurrentDeviceIsListedFirst()
    {
        // Arrange
        var currentSession = CreateTestSession(_testUserId, "Mozilla/5.0 Current", "192.168.1.1");
        var olderSession = CreateTestSession(_testUserId, "Mozilla/5.0 Older", "10.0.0.1");

        var sessions = new List<Session> { olderSession, currentSession };

        _mockSessionRepository
            .Setup(r => r.GetActiveSessionsByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetUserDevicesQuery(_testUserId, CurrentSessionId: currentSession.Id);

        // Act
        var devices = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(currentSession.Id, devices.First().Id);
        Assert.True(devices.First().IsCurrentDevice);
    }

    [Fact]
    public async Task Handle_ParsesDeviceInfoCorrectly()
    {
        // Arrange
        var chromeUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        var session = CreateTestSession(_testUserId, chromeUserAgent, "192.168.1.100");

        _mockSessionRepository
            .Setup(r => r.GetActiveSessionsByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Session> { session });

        var query = new GetUserDevicesQuery(_testUserId);

        // Act
        var devices = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var device = devices.Single();
        Assert.Equal("Desktop", device.DeviceType);
        Assert.Equal("Chrome", device.Browser);
        Assert.StartsWith("120", device.BrowserVersion);
        Assert.Equal("Windows", device.OperatingSystem);
        Assert.Equal("10/11", device.OsVersion);
        Assert.False(device.IsMobile);
        Assert.Equal("192.168.1.100", device.IpAddress);
        Assert.Contains("Chrome", device.DisplayName);
        Assert.Contains("Windows", device.DisplayName);
    }

    [Fact]
    public async Task Handle_NullUserAgent_ReturnsUnknownDevice()
    {
        // Arrange
        var session = CreateTestSession(_testUserId, null, "192.168.1.1");

        _mockSessionRepository
            .Setup(r => r.GetActiveSessionsByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Session> { session });

        var query = new GetUserDevicesQuery(_testUserId);

        // Act
        var devices = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var device = devices.Single();
        Assert.Equal("Unknown", device.DeviceType);
        Assert.Equal("Unknown", device.Browser);
        Assert.Equal("Unknown", device.OperatingSystem);
    }

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public void Constructor_NullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GetUserDevicesQueryHandler(null!));
    }
}

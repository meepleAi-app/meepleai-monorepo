using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Comprehensive tests for GetUserActivityQueryHandler.
/// Tests user activity retrieval with filtering, pagination, and edge cases.
/// Week 8 Part 2: 27 tests for simple handler without Value Object complexity.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetUserActivityQueryHandlerTests
{
    private readonly Mock<IAuditLogRepository> _auditLogRepositoryMock;
    private readonly Mock<ILogger<GetUserActivityQueryHandler>> _loggerMock;
    private readonly GetUserActivityQueryHandler _handler;

    public GetUserActivityQueryHandlerTests()
    {
        _auditLogRepositoryMock = new Mock<IAuditLogRepository>();
        _loggerMock = new Mock<ILogger<GetUserActivityQueryHandler>>();
        _handler = new GetUserActivityQueryHandler(
            _auditLogRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithRecentActivity_ReturnsActivities()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "login", "Authentication", "success", null, null, "192.168.1.1", null),
            new(Guid.NewGuid(), userId, "view_game", "Games", "success", "game-123", null, "192.168.1.1", null)
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Activities.Count);
        Assert.Equal("login", result.Activities[0].Action);
        Assert.Equal("view_game", result.Activities[1].Action);
    }

    [Fact]
    public async Task Handle_WithEmptyActivity_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLog>());

        var query = new GetUserActivityQuery(UserId: userId, Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Activities);
    }

    [Fact]
    public async Task Handle_WithActionFilter_ReturnsFilteredActivities()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "login", "Auth", "success"),
            new(Guid.NewGuid(), userId, "logout", "Auth", "success"),
            new(Guid.NewGuid(), userId, "view_game", "Games", "success", "game-1")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, ActionFilter: "login,logout", Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Activities.Count);
        Assert.All(result.Activities, a => Assert.True(a.Action == "login" || a.Action == "logout"));
    }

    [Fact]
    public async Task Handle_WithResourceFilter_ReturnsFilteredActivities()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "create", "Games", "success", "game-1"),
            new(Guid.NewGuid(), userId, "update", "Games", "success", "game-2"),
            new(Guid.NewGuid(), userId, "view", "Users", "success", "user-1")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, ResourceFilter: "Games", Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Activities.Count);
        Assert.All(result.Activities, a => Assert.Equal("Games", a.Resource));
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));

        _auditLogRepositoryMock.Verify(
            r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithLimit_RespectsMaximumLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = Enumerable.Range(1, 600)
            .Select(i => new AuditLog(Guid.NewGuid(), userId, $"action_{i}", "Resource", "success"))
            .ToList();

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, Limit: 1000);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(600, result.TotalCount);
        Assert.Equal(500, result.Activities.Count);
    }

    [Fact]
    public async Task Handle_WithZeroLimit_UsesDefaultLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = Enumerable.Range(1, 150)
            .Select(i => new AuditLog(Guid.NewGuid(), userId, $"action_{i}", "Resource", "success"))
            .ToList();

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, Limit: 0);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(150, result.TotalCount);
        Assert.Equal(100, result.Activities.Count);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLog>());

        var query = new GetUserActivityQuery(UserId: userId, Limit: 10);

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _auditLogRepositoryMock.Verify(
            r => r.GetByUserIdAsync(userId, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsActivityRetrieval()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "test", "Resource", "success")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, Limit: 10);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Fetching user activity")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PreservesAuditLogDetails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var logId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(logId, userId, "create_game", "Games", "success", "game-123", "{\"name\":\"Chess\"}", "192.168.1.100", null)
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result.Activities);
        var activity = result.Activities[0];
        Assert.Equal(logId, activity.Id);
        Assert.Equal("create_game", activity.Action);
        Assert.Equal("Games", activity.Resource);
        Assert.Equal("game-123", activity.ResourceId);
        Assert.Equal("success", activity.Result);
        Assert.Equal("{\"name\":\"Chess\"}", activity.Details);
        Assert.Equal("192.168.1.100", activity.IpAddress);
    }

    [Fact]
    public async Task Handle_WithCaseInsensitiveActionFilter_MatchesActions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "LOGIN", "Auth", "success"),
            new(Guid.NewGuid(), userId, "logout", "Auth", "success"),
            new(Guid.NewGuid(), userId, "View_Game", "Games", "success", "g1")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, ActionFilter: "login,LOGOUT", Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Activities.Count);
    }

    [Fact]
    public async Task Handle_WithCaseInsensitiveResourceFilter_MatchesResource()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "action1", "GAMES", "success", "g1"),
            new(Guid.NewGuid(), userId, "action2", "games", "success", "g2"),
            new(Guid.NewGuid(), userId, "action3", "Auth", "success")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, ResourceFilter: "Games", Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Activities.Count);
    }

    [Fact]
    public async Task Handle_WithWhitespaceInActionFilter_TrimsAndFilters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "login", "Auth", "success"),
            new(Guid.NewGuid(), userId, "logout", "Auth", "success")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, ActionFilter: " login , logout ", Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Activities.Count);
    }

    [Fact]
    public async Task Handle_WithSingleActionFilter_ReturnsMatchingAction()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "login", "Auth", "success"),
            new(Guid.NewGuid(), userId, "view", "Games", "success", "g1")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, ActionFilter: "login", Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result.Activities);
        Assert.Equal("login", result.Activities[0].Action);
    }

    [Fact]
    public async Task Handle_WithSmallLimit_ReturnsLimitedResults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = Enumerable.Range(1, 20)
            .Select(i => new AuditLog(Guid.NewGuid(), userId, $"action_{i}", "Resource", "success"))
            .ToList();

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, Limit: 5);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(20, result.TotalCount);
        Assert.Equal(5, result.Activities.Count);
    }

    [Fact]
    public async Task Handle_WithNoMatchingActionFilter_ReturnsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "login", "Auth", "success"),
            new(Guid.NewGuid(), userId, "logout", "Auth", "success")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, ActionFilter: "nonexistent", Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Activities);
    }

    [Fact]
    public async Task Handle_WithNoMatchingResourceFilter_ReturnsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "action1", "Games", "success", "g1"),
            new(Guid.NewGuid(), userId, "action2", "Auth", "success")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, ResourceFilter: "NonexistentResource", Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Activities);
    }

    [Fact]
    public async Task Handle_WithEmptyActionFilter_ReturnsAllActivities()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "login", "Auth", "success"),
            new(Guid.NewGuid(), userId, "view", "Games", "success", "g1")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, ActionFilter: "", Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Activities.Count);
    }

    [Fact]
    public async Task Handle_WithEmptyResourceFilter_ReturnsAllActivities()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "action1", "Games", "success", "g1"),
            new(Guid.NewGuid(), userId, "action2", "Auth", "success")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, ResourceFilter: "", Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Activities.Count);
    }

    [Fact]
    public async Task Handle_WithNullResourceId_HandlesCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "login", "Auth", "success"),
            new(Guid.NewGuid(), userId, "view_game", "Games", "success", "game-123")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Activities.Count);
        Assert.Null(result.Activities[0].ResourceId);
        Assert.Equal("game-123", result.Activities[1].ResourceId);
    }

    [Fact]
    public async Task Handle_WithNullDetails_HandlesCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "simple_action", "Resource", "success"),
            new(Guid.NewGuid(), userId, "detailed_action", "Resource", "success", null, "{\"key\":\"value\"}")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Activities.Count);
        Assert.Null(result.Activities[0].Details);
        Assert.Equal("{\"key\":\"value\"}", result.Activities[1].Details);
    }

    [Fact]
    public async Task Handle_WithFailedResults_IncludesFailedActivities()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "login", "Auth", "success"),
            new(Guid.NewGuid(), userId, "access_denied", "Resource", "failure", "r1")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Activities.Count);
        Assert.Equal("success", result.Activities[0].Result);
        Assert.Equal("failure", result.Activities[1].Result);
    }

    [Fact]
    public async Task Handle_WithMultipleActions_CorrectlyCountsTotal()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = Enumerable.Range(1, 50)
            .Select(i => new AuditLog(Guid.NewGuid(), userId, $"action_{i}", "Resource", "success"))
            .ToList();

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(50, result.TotalCount);
        Assert.Equal(10, result.Activities.Count);
    }

    [Fact]
    public async Task Handle_WithMixedResources_ReturnsAllResources()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "login", "Auth", "success"),
            new(Guid.NewGuid(), userId, "view", "Games", "success"),
            new(Guid.NewGuid(), userId, "create", "Documents", "success"),
            new(Guid.NewGuid(), userId, "update", "Users", "success")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(4, result.Activities.Count);
    }

    [Fact]
    public async Task Handle_WithDifferentIpAddresses_PreservesIpAddress()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "login", "Auth", "success", null, null, "192.168.1.1"),
            new(Guid.NewGuid(), userId, "logout", "Auth", "success", null, null, "10.0.0.1")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Activities.Count);
        Assert.Equal("192.168.1.1", result.Activities[0].IpAddress);
        Assert.Equal("10.0.0.1", result.Activities[1].IpAddress);
    }

    [Fact]
    public async Task Handle_WithMultipleActions_OnSameResource_GroupsByResource()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "create", "Games", "success", "g1"),
            new(Guid.NewGuid(), userId, "update", "Games", "success", "g1"),
            new(Guid.NewGuid(), userId, "delete", "Games", "success", "g1")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, ResourceFilter: "Games", Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Activities.Count);
        Assert.All(result.Activities, a => Assert.Equal("Games", a.Resource));
    }

    [Fact]
    public async Task Handle_WithDifferentResults_PreservesResultStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "action1", "Resource", "success"),
            new(Guid.NewGuid(), userId, "action2", "Resource", "failure"),
            new(Guid.NewGuid(), userId, "action3", "Resource", "error")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Activities.Count);
        Assert.Equal("success", result.Activities[0].Result);
        Assert.Equal("failure", result.Activities[1].Result);
        Assert.Equal("error", result.Activities[2].Result);
    }

    [Fact]
    public async Task Handle_WithActionFilterAndResourceFilter_CombinesFilters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new(Guid.NewGuid(), userId, "view", "Games", "success", "g1"),
            new(Guid.NewGuid(), userId, "create", "Games", "success", "g2"),
            new(Guid.NewGuid(), userId, "view", "Users", "success", "u1"),
            new(Guid.NewGuid(), userId, "delete", "Games", "success", "g3")
        };

        _auditLogRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLogs);

        var query = new GetUserActivityQuery(UserId: userId, ActionFilter: "view,create", ResourceFilter: "Games", Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Activities.Count);
        Assert.All(result.Activities, a =>
        {
            Assert.Equal("Games", a.Resource);
            Assert.True(a.Action == "view" || a.Action == "create");
        });
    }
}

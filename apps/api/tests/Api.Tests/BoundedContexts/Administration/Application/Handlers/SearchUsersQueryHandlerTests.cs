using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Tests.BoundedContexts.Administration.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Comprehensive tests for SearchUsersQueryHandler.
/// Tests user search for autocomplete scenarios with minimum query length validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public class SearchUsersQueryHandlerTests
{
    private readonly Mock<IUserProfileRepository> _userProfileRepositoryMock;
    private readonly Mock<ILogger<SearchUsersQueryHandler>> _loggerMock;
    private readonly SearchUsersQueryHandler _handler;

    public SearchUsersQueryHandlerTests()
    {
        _userProfileRepositoryMock = new Mock<IUserProfileRepository>();
        _loggerMock = new Mock<ILogger<SearchUsersQueryHandler>>();
        _handler = new SearchUsersQueryHandler(
            _userProfileRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidQuery_ReturnsMatchingUsers()
    {
        // Arrange
        var users = new List<UserProfile>
        {
            new UserProfileBuilder().WithEmail("john@example.com").WithDisplayName("John Doe").Build(),
            new UserProfileBuilder().WithEmail("jane@example.com").WithDisplayName("Jane Smith").Build()
        };

        _userProfileRepositoryMock
            .Setup(r => r.SearchAsync("john", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(users.Where(u => u.DisplayName!.Contains("John", StringComparison.OrdinalIgnoreCase)).ToList());

        var query = new SearchUsersQuery(
            SearchQuery: "john",
            MaxResults: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainSingle();
        result[0].DisplayName.Should().Be("John Doe");
        result[0].Email.Should().Be("john@example.com");

        _userProfileRepositoryMock.Verify(
            r => r.SearchAsync("john", It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithMultipleMatches_ReturnsAllMatches()
    {
        // Arrange
        var users = new List<UserProfile>
        {
            new UserProfileBuilder().WithEmail("admin1@example.com").WithDisplayName("Admin One").WithRole("admin").Build(),
            new UserProfileBuilder().WithEmail("admin2@example.com").WithDisplayName("Admin Two").WithRole("admin").Build(),
            new UserProfileBuilder().WithEmail("admin3@example.com").WithDisplayName("Admin Three").WithRole("admin").Build()
        };

        _userProfileRepositoryMock
            .Setup(r => r.SearchAsync("admin", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var query = new SearchUsersQuery(
            SearchQuery: "admin",
            MaxResults: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Count.Should().Be(3);
        result.Should().AllSatisfy(r => r.DisplayName.Should().Contain("Admin"));
    }

    [Fact]
    public async Task Handle_WithMaxResults_RespectsLimit()
    {
        // Arrange
        var users = new List<UserProfile>
        {
            new UserProfileBuilder().WithDisplayName("User 1").Build(),
            new UserProfileBuilder().WithDisplayName("User 2").Build(),
            new UserProfileBuilder().WithDisplayName("User 3").Build()
        };

        _userProfileRepositoryMock
            .Setup(r => r.SearchAsync("user", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users.Take(2).ToList());

        var query = new SearchUsersQuery(
            SearchQuery: "user",
            MaxResults: 2);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert — repository applies MaxResults limit
        result.Count.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithEmptyQuery_ReturnsEmptyList()
    {
        // Arrange
        var query = new SearchUsersQuery(
            SearchQuery: "",
            MaxResults: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        // Verify repository was NOT called
        _userProfileRepositoryMock.Verify(
            r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ReturnsEmptyList()
    {
        // Arrange
        var query = new SearchUsersQuery(
            SearchQuery: null!,
            MaxResults: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        // Verify repository was NOT called
        _userProfileRepositoryMock.Verify(
            r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithSingleCharacterQuery_ReturnsEmptyList()
    {
        // Arrange - Query too short (minimum is 2 characters)
        var query = new SearchUsersQuery(
            SearchQuery: "a",
            MaxResults: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        // Verify repository was NOT called (query too short)
        _userProfileRepositoryMock.Verify(
            r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithTwoCharacterQuery_CallsRepository()
    {
        // Arrange - Minimum valid query length
        _userProfileRepositoryMock
            .Setup(r => r.SearchAsync("ab", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile>());

        var query = new SearchUsersQuery(
            SearchQuery: "ab",
            MaxResults: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();

        // Verify repository WAS called (query meets minimum length)
        _userProfileRepositoryMock.Verify(
            r => r.SearchAsync("ab", It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoMatches_ReturnsEmptyList()
    {
        // Arrange
        _userProfileRepositoryMock
            .Setup(r => r.SearchAsync("nonexistent", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile>());

        var query = new SearchUsersQuery(
            SearchQuery: "nonexistent",
            MaxResults: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RepositoryThrowsException_ReturnsEmptyListAndLogsError()
    {
        // Arrange
        var exception = new Exception("Database error");

        _userProfileRepositoryMock
            .Setup(r => r.SearchAsync("error", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var query = new SearchUsersQuery(
            SearchQuery: "error",
            MaxResults: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty(); // Handler catches exception and returns empty list

        // Verify error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error searching users")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        _userProfileRepositoryMock
            .Setup(r => r.SearchAsync("test", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile>());

        var query = new SearchUsersQuery(
            SearchQuery: "test",
            MaxResults: 10);

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _userProfileRepositoryMock.Verify(
            r => r.SearchAsync("test", It.IsAny<int?>(), cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithResults_LogsResultCount()
    {
        // Arrange
        var users = new List<UserProfile>
        {
            new UserProfileBuilder().Build(),
            new UserProfileBuilder().Build()
        };

        _userProfileRepositoryMock
            .Setup(r => r.SearchAsync("test", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var query = new SearchUsersQuery(
            SearchQuery: "test",
            MaxResults: 10);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert - Verify information log
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Found 2 users")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Comprehensive tests for SearchUsersQueryHandler.
/// Tests user search for autocomplete scenarios with minimum query length validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SearchUsersQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ILogger<SearchUsersQueryHandler>> _loggerMock;
    private readonly SearchUsersQueryHandler _handler;

    public SearchUsersQueryHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _loggerMock = new Mock<ILogger<SearchUsersQueryHandler>>();
        _handler = new SearchUsersQueryHandler(
            _userRepositoryMock.Object,
            _loggerMock.Object);
    }
    [Fact]
    public async Task Handle_WithValidQuery_ReturnsMatchingUsers()
    {
        // Arrange
        var users = new List<User>
        {
            new UserBuilder()
                .WithEmail("john@example.com")
                .WithDisplayName("John Doe")
                .Build(),
            new UserBuilder()
                .WithEmail("jane@example.com")
                .WithDisplayName("Jane Smith")
                .Build()
        };

        _userRepositoryMock
            .Setup(r => r.SearchAsync("john", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(users.Where(u => u.DisplayName.Contains("John", StringComparison.OrdinalIgnoreCase)).ToList());

        var query = new SearchUsersQuery(
            SearchQuery: "john",
            MaxResults: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("John Doe", result[0].DisplayName);
        Assert.Equal("john@example.com", result[0].Email);

        _userRepositoryMock.Verify(
            r => r.SearchAsync("john", 10, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithMultipleMatches_ReturnsAllMatches()
    {
        // Arrange
        var users = new List<User>
        {
            TestDataFactory.CreateAdmin("admin1@example.com"),
            TestDataFactory.CreateAdmin("admin2@example.com"),
            TestDataFactory.CreateAdmin("admin3@example.com")
        };

        _userRepositoryMock
            .Setup(r => r.SearchAsync(Role.Admin.Value, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var query = new SearchUsersQuery(
            SearchQuery: Role.Admin.Value,
            MaxResults: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.Contains("Admin", r.DisplayName));
    }

    [Fact]
    public async Task Handle_WithMaxResults_RespectsLimit()
    {
        // Arrange
        var users = new List<User>
        {
            TestDataFactory.CreateUser(displayName: "User 1"),
            TestDataFactory.CreateUser(displayName: "User 2"),
            TestDataFactory.CreateUser(displayName: "User 3")
        };

        _userRepositoryMock
            .Setup(r => r.SearchAsync(Role.User.Value, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users.Take(2).ToList());

        var query = new SearchUsersQuery(
            SearchQuery: Role.User.Value,
            MaxResults: 2);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        _userRepositoryMock.Verify(
            r => r.SearchAsync(Role.User.Value, 2, It.IsAny<CancellationToken>()),
            Times.Once);
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
        Assert.NotNull(result);
        Assert.Empty(result);

        // Verify repository was NOT called
        _userRepositoryMock.Verify(
            r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
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
        Assert.NotNull(result);
        Assert.Empty(result);

        // Verify repository was NOT called
        _userRepositoryMock.Verify(
            r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
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
        Assert.NotNull(result);
        Assert.Empty(result);

        // Verify repository was NOT called (query too short)
        _userRepositoryMock.Verify(
            r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithTwoCharacterQuery_CallsRepository()
    {
        // Arrange - Minimum valid query length
        _userRepositoryMock
            .Setup(r => r.SearchAsync("ab", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        var query = new SearchUsersQuery(
            SearchQuery: "ab",
            MaxResults: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);

        // Verify repository WAS called (query meets minimum length)
        _userRepositoryMock.Verify(
            r => r.SearchAsync("ab", It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoMatches_ReturnsEmptyList()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.SearchAsync("nonexistent", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        var query = new SearchUsersQuery(
            SearchQuery: "nonexistent",
            MaxResults: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
    [Fact]
    public async Task Handle_RepositoryThrowsException_ReturnsEmptyListAndLogsError()
    {
        // Arrange
        var exception = new Exception("Database error");

        _userRepositoryMock
            .Setup(r => r.SearchAsync("error", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var query = new SearchUsersQuery(
            SearchQuery: "error",
            MaxResults: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result); // Handler catches exception and returns empty list

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
        _userRepositoryMock
            .Setup(r => r.SearchAsync("test", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        var query = new SearchUsersQuery(
            SearchQuery: "test",
            MaxResults: 10);

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _userRepositoryMock.Verify(
            r => r.SearchAsync("test", 10, cancellationToken),
            Times.Once);
    }
    [Fact]
    public async Task Handle_WithResults_LogsResultCount()
    {
        // Arrange
        var users = new List<User>
        {
            new UserBuilder().Build(),
            new UserBuilder().Build()
        };

        _userRepositoryMock
            .Setup(r => r.SearchAsync("test", It.IsAny<int>(), It.IsAny<CancellationToken>()))
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


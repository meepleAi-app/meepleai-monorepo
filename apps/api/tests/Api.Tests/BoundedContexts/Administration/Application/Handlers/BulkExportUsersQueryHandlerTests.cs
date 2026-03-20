using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

[Trait("Category", TestCategories.Unit)]

public class BulkExportUsersQueryHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ILogger<BulkExportUsersQueryHandler>> _mockLogger;
    private readonly BulkExportUsersQueryHandler _handler;

    public BulkExportUsersQueryHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockLogger = new Mock<ILogger<BulkExportUsersQueryHandler>>();
        _handler = new BulkExportUsersQueryHandler(
            _mockUserRepository.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithNoFilters_ShouldExportAllUsers()
    {
        // Arrange
        var users = new List<User>
        {
            CreateTestUser(Guid.NewGuid(), "user1@test.com", "User One", Role.User),
            CreateTestUser(Guid.NewGuid(), "admin@test.com", "Admin User", Role.Admin),
            CreateTestUser(Guid.NewGuid(), "editor@test.com", "Editor User", Role.Editor)
        };

        _mockUserRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var query = new BulkExportUsersQuery();

        // Act
        var csv = await _handler.Handle(query, CancellationToken.None);

        // Assert
        csv.Should().NotBeNullOrEmpty();
        csv.Should().StartWith("email,displayName,role,createdAt");
        csv.Split('\n').Length.Should().Be(5); // Header + 3 users + trailing newline
        csv.Should().Contain("user1@test.com");
        csv.Should().Contain("admin@test.com");
        csv.Should().Contain("editor@test.com");
    }

    [Fact]
    public async Task Handle_WithRoleFilter_ShouldExportOnlyMatchingUsers()
    {
        // Arrange
        var users = new List<User>
        {
            CreateTestUser(Guid.NewGuid(), "user1@test.com", "User One", Role.User),
            CreateTestUser(Guid.NewGuid(), "admin@test.com", "Admin User", Role.Admin),
            CreateTestUser(Guid.NewGuid(), "user2@test.com", "User Two", Role.User)
        };

        _mockUserRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var query = new BulkExportUsersQuery(Role: "user");

        // Act
        var csv = await _handler.Handle(query, CancellationToken.None);

        // Assert
        csv.Should().Contain("user1@test.com");
        csv.Should().Contain("user2@test.com");
        csv.Should().NotContain("admin@test.com");
        csv.Split('\n').Length.Should().Be(4); // Header + 2 users + trailing newline
    }

    [Fact]
    public async Task Handle_WithSearchFilter_ShouldExportMatchingUsers()
    {
        // Arrange
        var users = new List<User>
        {
            CreateTestUser(Guid.NewGuid(), "john@test.com", "John Doe", Role.User),
            CreateTestUser(Guid.NewGuid(), "jane@test.com", "Jane Smith", Role.User),
            CreateTestUser(Guid.NewGuid(), "johnny@test.com", "Johnny Walker", Role.User)
        };

        _mockUserRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var query = new BulkExportUsersQuery(SearchTerm: "john");

        // Act
        var csv = await _handler.Handle(query, CancellationToken.None);

        // Assert
        csv.Should().Contain("john@test.com");
        csv.Should().Contain("johnny@test.com");
        csv.Should().NotContain("jane@test.com");
    }

    [Fact]
    public async Task Handle_WithRoleAndSearchFilters_ShouldApplyBothFilters()
    {
        // Arrange
        var users = new List<User>
        {
            CreateTestUser(Guid.NewGuid(), "admin1@test.com", "Admin One", Role.Admin),
            CreateTestUser(Guid.NewGuid(), "admin2@test.com", "Admin Two", Role.Admin),
            CreateTestUser(Guid.NewGuid(), "user1@test.com", "User One", Role.User)
        };

        _mockUserRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var query = new BulkExportUsersQuery(Role: "admin", SearchTerm: "One");

        // Act
        var csv = await _handler.Handle(query, CancellationToken.None);

        // Assert
        csv.Should().Contain("admin1@test.com");
        csv.Should().NotContain("admin2@test.com");
        csv.Should().NotContain("user1@test.com");
        csv.Split('\n').Length.Should().Be(3); // Header + 1 user + trailing newline
    }

    [Fact]
    public async Task Handle_WithNoMatchingUsers_ShouldReturnHeaderOnly()
    {
        // Arrange
        _mockUserRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        var query = new BulkExportUsersQuery();

        // Act
        var csv = await _handler.Handle(query, CancellationToken.None);

        // Assert - Use Environment.NewLine for platform independence
        csv.Should().Be($"email,displayName,role,createdAt{Environment.NewLine}");
    }

    [Fact]
    public async Task Handle_WithSpecialCharactersInFields_ShouldEscapeProperly()
    {
        // Arrange
        var users = new List<User>
        {
            CreateTestUser(Guid.NewGuid(), "user@test.com", "User, With Comma", Role.User)
        };

        _mockUserRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var query = new BulkExportUsersQuery();

        // Act
        var csv = await _handler.Handle(query, CancellationToken.None);

        // Assert
        csv.Should().Contain("\"User, With Comma\"");
    }

    [Fact]
    public async Task Handle_ShouldIncludeCreatedAtTimestamp()
    {
        // Arrange
        var user = CreateTestUser(Guid.NewGuid(), "user@test.com", "Test User", Role.User);
        var users = new List<User> { user };

        _mockUserRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var query = new BulkExportUsersQuery();

        // Act
        var csv = await _handler.Handle(query, CancellationToken.None);

        // Assert
        csv.Should().Contain(user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    private static User CreateTestUser(Guid id, string email, string displayName, Role role)
    {
        return new User(
            id: id,
            email: new Email(email),
            displayName: displayName,
            passwordHash: PasswordHash.Create("Password123!"),
            role: role
        );
    }
}
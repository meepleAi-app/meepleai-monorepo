using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Tests.BoundedContexts.Administration.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public class BulkExportUsersQueryHandlerTests
{
    private readonly Mock<IUserProfileRepository> _mockUserProfileRepository;
    private readonly Mock<ILogger<BulkExportUsersQueryHandler>> _mockLogger;
    private readonly BulkExportUsersQueryHandler _handler;

    public BulkExportUsersQueryHandlerTests()
    {
        _mockUserProfileRepository = new Mock<IUserProfileRepository>();
        _mockLogger = new Mock<ILogger<BulkExportUsersQueryHandler>>();
        _handler = new BulkExportUsersQueryHandler(
            _mockUserProfileRepository.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithNoFilters_ShouldExportAllUsers()
    {
        // Arrange
        var users = new List<UserProfile>
        {
            new UserProfileBuilder().WithEmail("user1@test.com").WithDisplayName("User One").WithRole("user").Build(),
            new UserProfileBuilder().WithEmail("admin@test.com").WithDisplayName("Admin User").WithRole("admin").Build(),
            new UserProfileBuilder().WithEmail("editor@test.com").WithDisplayName("Editor User").WithRole("editor").Build()
        };

        _mockUserProfileRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
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
        var users = new List<UserProfile>
        {
            new UserProfileBuilder().WithEmail("user1@test.com").WithDisplayName("User One").WithRole("user").Build(),
            new UserProfileBuilder().WithEmail("admin@test.com").WithDisplayName("Admin User").WithRole("admin").Build(),
            new UserProfileBuilder().WithEmail("user2@test.com").WithDisplayName("User Two").WithRole("user").Build()
        };

        _mockUserProfileRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
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
        var users = new List<UserProfile>
        {
            new UserProfileBuilder().WithEmail("john@test.com").WithDisplayName("John Doe").Build(),
            new UserProfileBuilder().WithEmail("jane@test.com").WithDisplayName("Jane Smith").Build(),
            new UserProfileBuilder().WithEmail("johnny@test.com").WithDisplayName("Johnny Walker").Build()
        };

        _mockUserProfileRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
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
        var users = new List<UserProfile>
        {
            new UserProfileBuilder().WithEmail("admin1@test.com").WithDisplayName("Admin One").WithRole("admin").Build(),
            new UserProfileBuilder().WithEmail("admin2@test.com").WithDisplayName("Admin Two").WithRole("admin").Build(),
            new UserProfileBuilder().WithEmail("user1@test.com").WithDisplayName("User One").WithRole("user").Build()
        };

        _mockUserProfileRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
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
        _mockUserProfileRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile>());

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
        var users = new List<UserProfile>
        {
            new UserProfileBuilder().WithEmail("user@test.com").WithDisplayName("User, With Comma").Build()
        };

        _mockUserProfileRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
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
        var createdAt = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var user = new UserProfileBuilder()
            .WithEmail("user@test.com")
            .WithDisplayName("Test User")
            .WithCreatedAt(createdAt)
            .Build();

        _mockUserProfileRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile> { user });

        var query = new BulkExportUsersQuery();

        // Act
        var csv = await _handler.Handle(query, CancellationToken.None);

        // Assert
        csv.Should().Contain(createdAt.ToString("yyyy-MM-dd HH:mm:ss"));
    }
}

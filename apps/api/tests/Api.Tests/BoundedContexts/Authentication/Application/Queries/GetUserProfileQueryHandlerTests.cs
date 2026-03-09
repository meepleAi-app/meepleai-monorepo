using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2644")]
public class GetUserProfileQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly GetUserProfileQueryHandler _handler;

    public GetUserProfileQueryHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _handler = new GetUserProfileQueryHandler(_userRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidUserId_ReturnsUserProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "test@example.com", "Test User");

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var query = new GetUserProfileQuery { UserId = userId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.Id);
        Assert.Equal("test@example.com", result.Email);
        Assert.Equal("Test User", result.DisplayName);
        Assert.Equal("user", result.Role); // Role enum value is lowercase
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var query = new GetUserProfileQuery { UserId = userId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_MapsUserPreferences_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "test@example.com", "Test User");
        user.UpdatePreferences("it", "dark", true, 90);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var query = new GetUserProfileQuery { UserId = userId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("it", result.Language);
        Assert.Equal("dark", result.Theme);
        Assert.True(result.EmailNotifications);
        Assert.Equal(90, result.DataRetentionDays);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None)
        );
    }

    private static User CreateTestUser(Guid id, string email, string displayName)
    {
        return new User(
            id: id,
            email: new Email(email),
            displayName: displayName,
            passwordHash: PasswordHash.Create("Password123!"),
            role: Role.User
        );
    }
}

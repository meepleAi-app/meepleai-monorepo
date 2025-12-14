using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Comprehensive tests for GetUserByIdQueryHandler.
/// Tests user retrieval by ID and DTO mapping.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetUserByIdQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly GetUserByIdQueryHandler _handler;

    public GetUserByIdQueryHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _handler = new GetUserByIdQueryHandler(_userRepositoryMock.Object);
    }
    [Fact]
    public async Task Handle_WithExistingUser_ReturnsUserDto()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var query = new GetUserByIdQuery(user.Id);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal("user@example.com", result.Email);
        Assert.Equal(user.DisplayName, result.DisplayName);
        Assert.Equal(Role.User.Value, result.Role);
        Assert.False(result.IsTwoFactorEnabled);
        Assert.Null(result.TwoFactorEnabledAt);

        _userRepositoryMock.Verify(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAdminUser_ReturnsAdminRole()
    {
        // Arrange
        var user = new User(
            id: Guid.NewGuid(),
            email: new Email("admin@example.com"),
            displayName: "Admin User",
            passwordHash: PasswordHash.Create("SecurePassword123!"),
            role: Role.Admin
        );

        var query = new GetUserByIdQuery(user.Id);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Role.Admin.Value, result.Role);
    }

    [Fact]
    public async Task Handle_WithEditorUser_ReturnsEditorRole()
    {
        // Arrange
        var user = new User(
            id: Guid.NewGuid(),
            email: new Email("editor@example.com"),
            displayName: "Editor User",
            passwordHash: PasswordHash.Create("SecurePassword123!"),
            role: Role.Editor
        );

        var query = new GetUserByIdQuery(user.Id);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Role.Editor.Value, result.Role);
    }

    [Fact]
    public async Task Handle_With2FAEnabled_Returns2FAFields()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        user.Enable2FA(TotpSecret.FromEncrypted("encrypted_secret_base64"));

        var query = new GetUserByIdQuery(user.Id);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsTwoFactorEnabled);
        Assert.NotNull(result.TwoFactorEnabledAt);
    }
    [Fact]
    public async Task Handle_WithNonExistentUser_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserByIdQuery(userId);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);

        _userRepositoryMock.Verify(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task Handle_MapsDtoCorrectly()
    {
        // Arrange
        var user = CreateTestUser("test@example.com");
        var query = new GetUserByIdQuery(user.Id);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal("test@example.com", result.Email);
        Assert.Equal(user.DisplayName, result.DisplayName);
        Assert.Equal(Role.User.Value, result.Role);
        Assert.Equal(user.CreatedAt, result.CreatedAt);
        Assert.False(result.IsTwoFactorEnabled);
        Assert.Null(result.TwoFactorEnabledAt);
    }

    [Fact]
    public async Task Handle_IncludesCreatedAt()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var query = new GetUserByIdQuery(user.Id);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.CreatedAt, result.CreatedAt);
        Assert.True(result.CreatedAt <= DateTime.UtcNow);
    }
    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var query = new GetUserByIdQuery(user.Id);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, token))
            .ReturnsAsync(user);

        // Act
        await _handler.Handle(query, token);

        // Assert
        _userRepositoryMock.Verify(x => x.GetByIdAsync(user.Id, token), Times.Once);
    }

    [Fact]
    public async Task Handle_DoesNotModifyRepository()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var query = new GetUserByIdQuery(user.Id);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert - Query handlers are read-only
        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    private User CreateTestUser(string email)
    {
        return new User(
            id: Guid.NewGuid(),
            email: new Email(email),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("SecurePassword123!"),
            role: Role.User
        );
    }
}

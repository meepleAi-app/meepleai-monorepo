using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Commands.UserProfile;

/// <summary>
/// Tests for UpdatePreferencesCommandHandler - Issue #2308 Week 4 Phase 3.
/// Tests preference updates with branch coverage for valid updates, user not found.
/// Covers: Language/theme/notifications/retention updates, user not found, null command.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2308")]
public class UpdatePreferencesCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly UpdatePreferencesCommandHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();

    public UpdatePreferencesCommandHandlerTests()
    {
        _mockRepository = new Mock<IUserRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new UpdatePreferencesCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidPreferences_ShouldUpdateUser()
    {
        // Arrange
        var user = new User(
            id: TestUserId,
            email: new Email("user@test.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("Password123!"),
            role: Role.User,
            tier: UserTier.Free
        );

        var command = new UpdatePreferencesCommand
        {
            UserId = TestUserId,
            Language = "it",
            Theme = "dark",
            EmailNotifications = true,
            DataRetentionDays = 90
        };

        _mockRepository.Setup(r => r.GetByIdAsync(TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Language.Should().Be("it");
        result.Theme.Should().Be("dark");
        result.EmailNotifications.Should().BeTrue();
        result.DataRetentionDays.Should().Be(90);

        _mockRepository.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingUser_ShouldThrowDomainException()
    {
        // Arrange
        var command = new UpdatePreferencesCommand
        {
            UserId = Guid.NewGuid(),
            Language = "en",
            Theme = "light",
            EmailNotifications = false,
            DataRetentionDays = 30
        };

        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Arrange
        UpdatePreferencesCommand? command = null;

        // Act & Assert
        var act = async () => await _handler.Handle(command!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();

        _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

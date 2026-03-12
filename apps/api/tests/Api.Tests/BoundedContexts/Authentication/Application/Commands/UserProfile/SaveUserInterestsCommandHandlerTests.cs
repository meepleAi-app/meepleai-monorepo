using Api.BoundedContexts.Authentication.Application.Commands.UserProfile;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.UserProfile;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class SaveUserInterestsCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<SaveUserInterestsCommandHandler>> _loggerMock = new();
    private readonly SaveUserInterestsCommandHandler _handler;

    public SaveUserInterestsCommandHandlerTests()
    {
        _handler = new SaveUserInterestsCommandHandler(
            _userRepoMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_SavesInterests()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(
            userId,
            new Email("test@example.com"),
            "TestUser",
            PasswordHash.Create("Password1!"),
            Role.User);

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var interests = new List<string> { "Strategy", "Cooperative", "Card" };
        var command = new SaveUserInterestsCommand(userId, interests);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        user.Interests.Should().BeEquivalentTo(interests);
        _userRepoMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new SaveUserInterestsCommand(userId, new List<string> { "Strategy" });

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void Validator_RejectsUnknownCategories()
    {
        // Arrange
        var validator = new SaveUserInterestsCommandValidator();
        var command = new SaveUserInterestsCommand(Guid.NewGuid(), new List<string> { "Strategy", "InvalidCategory" });

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("not valid", StringComparison.OrdinalIgnoreCase));
    }
}

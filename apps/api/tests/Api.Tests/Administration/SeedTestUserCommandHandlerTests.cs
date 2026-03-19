using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Administration.AutoConfiguration;

[Trait("Category", TestCategories.Unit)]

public sealed class SeedTestUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<SeedTestUserCommandHandler>> _loggerMock;
    private readonly SeedTestUserCommandHandler _handler;

    public SeedTestUserCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<SeedTestUserCommandHandler>>();

        _handler = new SeedTestUserCommandHandler(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Handle_WhenTestUserExists_ShouldSkipSeed()
    {
        // Arrange
        var testEmail = new Email("Test@meepleai.com");
        var existingUser = CreateTestUser();

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.Is<Email>(e => e.Value == testEmail.Value), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var command = new SeedTestUserCommand();

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _userRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never
        );

        _unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_WhenNoTestUserExists_ShouldCreateTestUser()
    {
        // Arrange
        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new SeedTestUserCommand();

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        // Note: Email value object normalizes to lowercase
        _userRepositoryMock.Verify(
            x => x.AddAsync(It.Is<User>(u =>
                u.Email.Value == "test@meepleai.com" &&
                u.DisplayName == "Test User" &&
                u.Role == Role.User
            ), It.IsAny<CancellationToken>()),
            Times.Once
        );

        _unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    private static User CreateTestUser()
    {
        var testEmail = new Email("Test@meepleai.com");
        var passwordHash = PasswordHash.Create("Demo123!");

        return new User(
            Guid.NewGuid(),
            testEmail,
            "Test User",
            passwordHash,
            Role.User
        );
    }
}

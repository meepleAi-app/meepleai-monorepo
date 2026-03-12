using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Administration.AutoConfiguration;

[Trait("Category", TestCategories.Unit)]

public sealed class SeedAdminUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<SeedAdminUserCommandHandler>> _loggerMock;
    private readonly SeedAdminUserCommandHandler _handler;

    public SeedAdminUserCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<SeedAdminUserCommandHandler>>();

        _handler = new SeedAdminUserCommandHandler(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _configurationMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Handle_WhenAdminUserExists_ShouldSkipSeed()
    {
        // Arrange
        _userRepositoryMock
            .Setup(x => x.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new SeedAdminUserCommand();

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
    public async Task Handle_WhenNoAdminExists_ShouldCreateAdminUser()
    {
        // Arrange
        _userRepositoryMock
            .Setup(x => x.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _configurationMock.Setup(x => x["INITIAL_ADMIN_EMAIL"]).Returns("admin@test.com");
        _configurationMock.Setup(x => x["ADMIN_PASSWORD"]).Returns("Admin123!");
        _configurationMock.Setup(x => x["INITIAL_ADMIN_DISPLAY_NAME"]).Returns("Test Admin");

        var command = new SeedAdminUserCommand();

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _userRepositoryMock.Verify(
            x => x.AddAsync(It.Is<User>(u =>
                u.Email.Value == "admin@test.com" &&
                u.DisplayName == "Test Admin" &&
                u.Role == Role.SuperAdmin
            ), It.IsAny<CancellationToken>()),
            Times.Once
        );

        _unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_WhenEmailNotConfigured_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _userRepositoryMock
            .Setup(x => x.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _configurationMock.Setup(x => x["INITIAL_ADMIN_EMAIL"]).Returns((string?)null);

        var command = new SeedAdminUserCommand();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _handler.Handle(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Handle_WhenPasswordTooShort_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _userRepositoryMock
            .Setup(x => x.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _configurationMock.Setup(x => x["INITIAL_ADMIN_EMAIL"]).Returns("admin@test.com");
        _configurationMock.Setup(x => x["ADMIN_PASSWORD"]).Returns("Short1");

        var command = new SeedAdminUserCommand();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _handler.Handle(command, CancellationToken.None)
        );

        exception.Message.Should().Contain("at least 8 characters");
    }

    [Fact]
    public async Task Handle_WhenPasswordMissingUppercase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _userRepositoryMock
            .Setup(x => x.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _configurationMock.Setup(x => x["INITIAL_ADMIN_EMAIL"]).Returns("admin@test.com");
        _configurationMock.Setup(x => x["ADMIN_PASSWORD"]).Returns("lowercase123");

        var command = new SeedAdminUserCommand();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _handler.Handle(command, CancellationToken.None)
        );

        exception.Message.Should().Contain("uppercase letter");
    }

    [Fact]
    public async Task Handle_WhenPasswordMissingDigit_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _userRepositoryMock
            .Setup(x => x.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _configurationMock.Setup(x => x["INITIAL_ADMIN_EMAIL"]).Returns("admin@test.com");
        _configurationMock.Setup(x => x["ADMIN_PASSWORD"]).Returns("NoDigitPass");

        var command = new SeedAdminUserCommand();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _handler.Handle(command, CancellationToken.None)
        );

        exception.Message.Should().Contain("digit");
    }
}

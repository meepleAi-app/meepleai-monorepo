using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Administration.AutoConfiguration;

[Trait("Category", TestCategories.Unit)]
public sealed class SeedTestUserCommandHandlerTests
{
    private const string TestPassword = "TestSeed2026!xK";

    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<SeedTestUserCommandHandler>> _loggerMock;
    private readonly SeedTestUserCommandHandler _handler;

    public SeedTestUserCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<SeedTestUserCommandHandler>>();
        _configurationMock = new Mock<IConfiguration>();
        _configurationMock.Setup(c => c["SEED_TEST_PASSWORD"]).Returns(TestPassword);

        _handler = new SeedTestUserCommandHandler(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _configurationMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Handle_WhenTestUserExists_ShouldSkipSeed()
    {
        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestUser());

        await _handler.Handle(new SeedTestUserCommand(), CancellationToken.None);

        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNoTestUserExists_ShouldCreateTestUser()
    {
        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await _handler.Handle(new SeedTestUserCommand(), CancellationToken.None);

        _userRepositoryMock.Verify(
            x => x.AddAsync(It.Is<User>(u =>
                u.Email.Value == "test@meepleai.com" &&
                u.DisplayName == "Test User" &&
                u.Role == Role.User
            ), It.IsAny<CancellationToken>()),
            Times.Once
        );
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPasswordNotConfigured_ShouldSkipSeed()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["SEED_TEST_PASSWORD"]).Returns((string?)null);

        var handler = new SeedTestUserCommandHandler(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            configMock.Object,
            _loggerMock.Object
        );

        await handler.Handle(new SeedTestUserCommand(), CancellationToken.None);

        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPasswordTooShort_ShouldSkipSeed()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["SEED_TEST_PASSWORD"]).Returns("short");

        var handler = new SeedTestUserCommandHandler(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            configMock.Object,
            _loggerMock.Object
        );

        await handler.Handle(new SeedTestUserCommand(), CancellationToken.None);

        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static User CreateTestUser()
    {
        return new User(
            Guid.NewGuid(),
            new Email("Test@meepleai.com"),
            "Test User",
            PasswordHash.Create(TestPassword),
            Role.User
        );
    }
}

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
public sealed class SeedBadswormUserCommandHandlerTests
{
    private const string BadswormPassword = "BadswormSeed2026!xK"; // gitguardian:ignore

    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<SeedBadswormUserCommandHandler>> _loggerMock;
    private readonly SeedBadswormUserCommandHandler _handler;

    public SeedBadswormUserCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<SeedBadswormUserCommandHandler>>();
        _configurationMock = new Mock<IConfiguration>();
        _configurationMock.Setup(c => c["SEED_BADSWORM_PASSWORD"]).Returns(BadswormPassword);

        _handler = new SeedBadswormUserCommandHandler(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _configurationMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Handle_WhenBadswormUserExists_ShouldSkipSeed()
    {
        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateBadswormUser());

        await _handler.Handle(new SeedBadswormUserCommand(), CancellationToken.None);

        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNoBadswormUserExists_ShouldCreateUser()
    {
        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await _handler.Handle(new SeedBadswormUserCommand(), CancellationToken.None);

        _userRepositoryMock.Verify(
            x => x.AddAsync(It.Is<User>(u =>
                u.Email.Value == "badsworm@alice.it" &&
                u.DisplayName == "Badsworm" &&
                u.Role == Role.User &&
                u.EmailVerified
            ), It.IsAny<CancellationToken>()),
            Times.Once
        );
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPasswordNotConfigured_ShouldSkipSeed()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["SEED_BADSWORM_PASSWORD"]).Returns((string?)null);

        var handler = new SeedBadswormUserCommandHandler(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            configMock.Object,
            _loggerMock.Object
        );

        await handler.Handle(new SeedBadswormUserCommand(), CancellationToken.None);

        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPasswordTooShort_ShouldSkipSeed()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["SEED_BADSWORM_PASSWORD"]).Returns("short");

        var handler = new SeedBadswormUserCommandHandler(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            configMock.Object,
            _loggerMock.Object
        );

        await handler.Handle(new SeedBadswormUserCommand(), CancellationToken.None);

        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static User CreateBadswormUser()
    {
        return new User(
            Guid.NewGuid(),
            new Email("badsworm@alice.it"),
            "Badsworm",
            PasswordHash.Create(BadswormPassword),
            Role.User
        );
    }
}

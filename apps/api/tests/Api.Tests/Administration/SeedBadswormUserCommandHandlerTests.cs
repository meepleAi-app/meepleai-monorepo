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
    public async Task Handle_WhenBadswormUserExistsWithMatchingPassword_ShouldSkipUpdate()
    {
        // Existing user has a password that matches the configured secret —
        // no DB write should happen.
        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateBadswormUser(BadswormPassword));

        await _handler.Handle(new SeedBadswormUserCommand(), CancellationToken.None);

        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenBadswormUserExistsWithDifferentPassword_ShouldUpdatePassword()
    {
        // Issue #870: secret has rotated since user creation (or the local DB
        // was seeded with a stale value) — handler must update the password
        // so the secret remains the source of truth.
        const string staleStoredPassword = "OldPasswordRotated2025!"; // gitguardian:ignore
        var existingUser = CreateBadswormUser(staleStoredPassword);

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        await _handler.Handle(new SeedBadswormUserCommand(), CancellationToken.None);

        // Repository AddAsync should NOT be called — user already exists.
        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        // UpdateAsync called once with the same existing user instance.
        _userRepositoryMock.Verify(
            x => x.UpdateAsync(It.Is<User>(u => u.Id == existingUser.Id), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        // Domain side-effect: VerifyPassword should now succeed against the
        // configured secret (not the stale stored password).
        existingUser.VerifyPassword(BadswormPassword).Should().BeTrue();
        existingUser.VerifyPassword(staleStoredPassword).Should().BeFalse();
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
                u.Email.Value == "badsworm@gmail.com" &&
                u.DisplayName == "Badsworm" &&
                u.Role == Role.SuperAdmin &&
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

    private static User CreateBadswormUser(string password = BadswormPassword)
    {
        return new User(
            Guid.NewGuid(),
            new Email("badsworm@gmail.com"),
            "Badsworm",
            PasswordHash.Create(password),
            Role.User
        );
    }
}

using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public class ImpersonateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IAuditLogRepository> _mockAuditLogRepository;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<ImpersonateUserCommandHandler>> _mockLogger;
    private readonly ImpersonateUserCommandHandler _handler;

    public ImpersonateUserCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockAuditLogRepository = new Mock<IAuditLogRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<ImpersonateUserCommandHandler>>();

        _handler = new ImpersonateUserCommandHandler(
            _mockUserRepository.Object,
            _mockAuditLogRepository.Object,
            _mockMediator.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesSessionAndAuditLogs()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var reason = "Investigating user-reported display bug in library view";

        var adminUser = CreateTestUser(adminId, "admin@test.com", "admin");
        var targetUser = CreateTestUser(targetId, "user@test.com", "user");

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        var sessionToken = "fake-session-token-for-test";
        var expiresAt = DateTime.UtcNow.AddHours(24);
        var fakeUserDto = new UserDto(
            Id: targetId,
            Email: "user@test.com",
            DisplayName: "Test User",
            Role: "user",
            Tier: "free",
            CreatedAt: DateTime.UtcNow,
            IsTwoFactorEnabled: false,
            TwoFactorEnabledAt: null,
            Level: 1,
            ExperiencePoints: 0
        );
        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResponse(fakeUserDto, sessionToken, expiresAt));

        var command = new ImpersonateUserCommand(targetId, adminId, reason);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SessionToken.Should().Be(sessionToken);
        result.ImpersonatedUserId.Should().Be(targetId);
        result.ExpiresAt.Should().Be(expiresAt);

        // Verify 2 audit logs are created (admin entry + target user entry)
        _mockAuditLogRepository.Verify(
            r => r.AddAsync(It.IsAny<Api.BoundedContexts.Administration.Domain.Entities.AuditLog>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TargetIsAdmin_ThrowsForbiddenException()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var adminUser = CreateTestUser(adminId, "admin@test.com", "admin");
        var targetAdmin = CreateTestUser(targetId, "admin2@test.com", "admin");

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetAdmin);

        var command = new ImpersonateUserCommand(targetId, adminId, "Attempting to impersonate another admin");

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Cannot impersonate admin or SuperAdmin users*");
    }

    [Fact]
    public async Task Handle_TargetIsSuperAdmin_ThrowsForbiddenException()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var adminUser = CreateTestUser(adminId, "admin@test.com", "admin");
        var targetSuperAdmin = CreateTestUser(targetId, "superadmin@test.com", "superadmin");

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetSuperAdmin);

        var command = new ImpersonateUserCommand(targetId, adminId, "Attempting to impersonate superadmin");

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Cannot impersonate admin or SuperAdmin users*");
    }

    [Fact]
    public async Task Handle_TargetIsSuspended_ThrowsConflictException()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var adminUser = CreateTestUser(adminId, "admin@test.com", "admin");
        var suspendedUser = CreateTestUser(targetId, "suspended@test.com", "user");
        suspendedUser.Suspend("Suspended for testing");

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspendedUser);

        var command = new ImpersonateUserCommand(targetId, adminId, "Debugging suspended user account");

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"*{targetId}*");
    }

    [Fact]
    public async Task Handle_TargetUserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var adminUser = CreateTestUser(adminId, "admin@test.com", "admin");

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new ImpersonateUserCommand(targetId, adminId, "Debugging user session issue");

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{targetId}*");
    }

    private static User CreateTestUser(Guid id, string email, string role = "user")
    {
        var userRole = role.ToLowerInvariant() switch
        {
            "admin" => Role.Admin,
            "superadmin" => Role.SuperAdmin,
            _ => Role.User
        };

        return new User(
            id: id,
            email: new Email(email),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("Password123!"),
            role: userRole
        );
    }
}

using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using AuthRole = Api.SharedKernel.Domain.ValueObjects.Role;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class DeleteOwnAccountCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<DeleteOwnAccountCommandHandler>> _mockLogger;
    private readonly DeleteOwnAccountCommandHandler _handler;

    public DeleteOwnAccountCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockSessionRepository = new Mock<ISessionRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<DeleteOwnAccountCommandHandler>>();

        _handler = new DeleteOwnAccountCommandHandler(
            _mockUserRepository.Object,
            _mockSessionRepository.Object,
            _mockMediator.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidUser_DeletesAccountAndReturnsResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new DeleteOwnAccountCommand(userId);
        var user = new UserBuilder().WithId(userId).Build();

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockSessionRepository
            .Setup(r => r.GetActiveSessionsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Session>().AsReadOnly());

        _mockMediator
            .Setup(m => m.Send(It.IsAny<DeleteUserLlmDataCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteUserLlmDataResult(5, 3, true, DateTime.UtcNow));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.SessionsRevoked.Should().Be(0);
        result.LlmRequestLogsDeleted.Should().Be(5);
        result.ConversationMemoriesDeleted.Should().Be(3);
        result.RedisKeysCleared.Should().BeTrue();

        _mockSessionRepository.Verify(r => r.RevokeAllUserSessionsAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockUserRepository.Verify(r => r.DeleteAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new DeleteOwnAccountCommand(userId);

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var act = () =>
            _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();

        _mockUserRepository.Verify(r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_LastAdmin_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new DeleteOwnAccountCommand(userId);
        var user = new UserBuilder().WithId(userId).AsAdmin().Build();

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockUserRepository
            .Setup(r => r.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act & Assert
        var act = () =>
            _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();

        _mockUserRepository.Verify(r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AdminWithOtherAdmins_DeletesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new DeleteOwnAccountCommand(userId);
        var user = new UserBuilder().WithId(userId).AsAdmin().Build();

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockUserRepository
            .Setup(r => r.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        _mockSessionRepository
            .Setup(r => r.GetActiveSessionsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Session>().AsReadOnly());

        _mockMediator
            .Setup(m => m.Send(It.IsAny<DeleteUserLlmDataCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteUserLlmDataResult(0, 0, true, DateTime.UtcNow));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockUserRepository.Verify(r => r.DeleteAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DelegatesLlmDataDeletion_ViaMediator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new DeleteOwnAccountCommand(userId);
        var user = new UserBuilder().WithId(userId).Build();

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockSessionRepository
            .Setup(r => r.GetActiveSessionsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Session>().AsReadOnly());

        _mockMediator
            .Setup(m => m.Send(It.IsAny<DeleteUserLlmDataCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteUserLlmDataResult(10, 5, true, DateTime.UtcNow));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — verify correct command sent to mediator
        _mockMediator.Verify(m => m.Send(
            It.Is<DeleteUserLlmDataCommand>(c =>
                c.UserId == userId &&
                c.RequestedByUserId == userId &&
!c.IsAdminRequest),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsDeletedAtTimestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new DeleteOwnAccountCommand(userId);
        var user = new UserBuilder().WithId(userId).Build();
        var before = DateTime.UtcNow;

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockSessionRepository
            .Setup(r => r.GetActiveSessionsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Session>().AsReadOnly());

        _mockMediator
            .Setup(m => m.Send(It.IsAny<DeleteUserLlmDataCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteUserLlmDataResult(0, 0, false, DateTime.UtcNow));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        (result.DeletedAt >= before).Should().BeTrue();
        (result.DeletedAt <= DateTime.UtcNow).Should().BeTrue();
    }
    #region #3873 — PROTEGGE: the last-privileged-account guard must include superadmin

    /// <summary>
    /// Arranges everything downstream of the guard so that a guard which does NOT fire completes
    /// the deletion cleanly. Without this the unguarded case would die on a NullReferenceException
    /// (Moq returns a null session list) and hide the real signal behind an unrelated crash.
    /// </summary>
    private void ArrangeDeletionDownstreamSucceeds(Guid userId)
    {
        _mockSessionRepository
            .Setup(r => r.GetActiveSessionsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Session>().AsReadOnly());

        _mockMediator
            .Setup(m => m.Send(It.IsAny<DeleteUserLlmDataCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteUserLlmDataResult(0, 0, true, DateTime.UtcNow));
    }

    /// <summary>
    /// #3873 DoD 5 — superadmin as the subject. Trigger and counter disagree: the trigger at
    /// DeleteOwnAccountCommandHandler.cs:46 is <c>Role.IsAdmin()</c> (exact match on "admin"),
    /// while <c>CountAdminsAsync</c> counts <c>admin OR superadmin</c>. So a lone superadmin is
    /// counted as the last privileged account by the counter but never reaches it.
    /// </summary>
    [Theory]
    [InlineData("admin")]       // guarded today
    [InlineData("superadmin")]  // #3873: NOT guarded today — the trigger is an exact match
    public async Task Handle_LastPrivilegedAccount_ThrowsConflictException(string roleValue)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new DeleteOwnAccountCommand(userId);
        var user = new UserBuilder().WithId(userId).WithRole(AuthRole.Parse(roleValue)).Build();

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // CountAdminsAsync counts admin OR superadmin: 1 means this account IS the last one.
        _mockUserRepository
            .Setup(r => r.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        ArrangeDeletionDownstreamSucceeds(userId);

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();

        _mockUserRepository.Verify(r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The good direction, which must stay green: with a second privileged account left, the guard
    /// must not fire. Pins the boundary (<c>adminCount &lt;= 1</c>) so widening the trigger to
    /// superadmin does not become an over-block.
    /// </summary>
    [Theory]
    [InlineData("admin")]
    [InlineData("superadmin")]
    public async Task Handle_PrivilegedAccount_WithAnotherPrivilegedAccountLeft_DeletesSuccessfully(string roleValue)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new DeleteOwnAccountCommand(userId);
        var user = new UserBuilder().WithId(userId).WithRole(AuthRole.Parse(roleValue)).Build();

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockUserRepository
            .Setup(r => r.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        ArrangeDeletionDownstreamSucceeds(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _mockUserRepository.Verify(r => r.DeleteAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Anti-widening net: the guard protects privileged accounts only. An unprivileged account is
    /// deletable even when a single admin remains in the system.
    /// </summary>
    [Theory]
    [InlineData("user")]
    [InlineData("editor")]
    [InlineData("creator")]
    public async Task Handle_UnprivilegedAccount_IsNotProtectedByTheLastAdminGuard(string roleValue)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new DeleteOwnAccountCommand(userId);
        var user = new UserBuilder().WithId(userId).WithRole(AuthRole.Parse(roleValue)).Build();

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Even with a single admin left in the system, this account is not the one being protected.
        _mockUserRepository
            .Setup(r => r.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        ArrangeDeletionDownstreamSucceeds(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _mockUserRepository.Verify(r => r.DeleteAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}

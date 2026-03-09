using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Commands.Sessions;

/// <summary>
/// Tests for CreateSessionCommandHandler - Issue #2308 Week 4 Phase 3.
/// Tests session creation with branch coverage for user lookup, session creation.
/// Covers: Valid session creation, user not found, null command.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2308")]
public class CreateSessionCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CreateSessionCommandHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();

    public CreateSessionCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockSessionRepository = new Mock<ISessionRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new CreateSessionCommandHandler(
            _mockUserRepository.Object,
            _mockSessionRepository.Object,
            _mockUnitOfWork.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidUser_ShouldCreateSession()
    {
        // Arrange
        var command = new CreateSessionCommand(
            UserId: TestUserId,
            IpAddress: "192.168.1.100",
            UserAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
        );

        var user = new User(
            id: TestUserId,
            email: new Email("user@test.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("Password123!"),
            role: Role.User,
            tier: UserTier.Free
        );

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        Session? capturedSession = null;
        _mockSessionRepository
            .Setup(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback<Session, CancellationToken>((session, _) => capturedSession = session)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SessionToken.Should().NotBeNullOrEmpty();
        result.User.Should().NotBeNull();
        result.User.Email.Should().Be("user@test.com");

        capturedSession.Should().NotBeNull();
        capturedSession!.UserId.Should().Be(TestUserId);
        capturedSession.UserAgent.Should().Be("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        capturedSession.IpAddress.Should().Be("192.168.1.100");

        _mockSessionRepository.Verify(
            r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_WithNonExistingUser_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var command = new CreateSessionCommand(
            UserId: Guid.NewGuid(),
            IpAddress: "127.0.0.1",
            UserAgent: "Mozilla/5.0"
        );

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();

        _mockSessionRepository.Verify(
            r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Arrange
        CreateSessionCommand? command = null;

        // Act & Assert
        var act = async () => await _handler.Handle(command!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();

        _mockUserRepository.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}

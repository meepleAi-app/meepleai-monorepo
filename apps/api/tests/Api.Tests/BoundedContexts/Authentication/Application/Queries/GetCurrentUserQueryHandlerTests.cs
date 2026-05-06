using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Tests for GetCurrentUserQueryHandler.
/// Validates the CQRS query that materializes the active user DTO from a session ID.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class GetCurrentUserQueryHandlerTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly GetCurrentUserQueryHandler _handler;

    public GetCurrentUserQueryHandlerTests()
    {
        _mockSessionRepository = new Mock<ISessionRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _handler = new GetCurrentUserQueryHandler(
            _mockSessionRepository.Object,
            _mockUserRepository.Object);
    }

    [Fact]
    public async Task Handle_ValidSessionId_ReturnsUserDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithId(userId)
            .WithEmail("active@example.com")
            .WithDisplayName("Active User")
            .Build();

        var session = new Session(
            id: sessionId,
            userId: userId,
            token: SessionToken.Generate());

        _mockSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(new GetCurrentUserQuery(sessionId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
        result.Email.Should().Be("active@example.com");
        result.DisplayName.Should().Be("Active User");
        result.Role.Should().Be(Role.User.Value);
    }

    [Fact]
    public async Task Handle_NonExistentSession_ReturnsNull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        _mockSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        // Act
        var result = await _handler.Handle(new GetCurrentUserQuery(sessionId), CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _mockUserRepository.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "session lookup failed; user lookup must be skipped");
    }

    [Fact]
    public async Task Handle_SessionFoundButUserMissing_ReturnsNull()
    {
        // Arrange (defensive case: session exists but the user was deleted)
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = new Session(
            id: sessionId,
            userId: userId,
            token: SessionToken.Generate());

        _mockSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(new GetCurrentUserQuery(sessionId), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

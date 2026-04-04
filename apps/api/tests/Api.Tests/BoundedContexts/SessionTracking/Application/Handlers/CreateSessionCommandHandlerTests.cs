using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class CreateSessionCommandHandlerTests : IDisposable
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ISessionQuotaService> _quotaServiceMock = new();
    private readonly MeepleAiDbContext _db;
    private readonly Mock<ILogger<CreateSessionCommandHandler>> _loggerMock = new();
    private readonly CreateSessionCommandHandler _handler;

    public CreateSessionCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _handler = new CreateSessionCommandHandler(
            _sessionRepoMock.Object,
            _unitOfWorkMock.Object,
            _quotaServiceMock.Object,
            _db,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_NullSessionRepository_ThrowsArgumentNullException()
    {
        var act = () => new CreateSessionCommandHandler(
            null!,
            _unitOfWorkMock.Object,
            _quotaServiceMock.Object,
            _db,
            _loggerMock.Object);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_NullUnitOfWork_ThrowsArgumentNullException()
    {
        var act = () => new CreateSessionCommandHandler(
            _sessionRepoMock.Object,
            null!,
            _quotaServiceMock.Object,
            _db,
            _loggerMock.Object);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_NullQuotaService_ThrowsArgumentNullException()
    {
        var act = () => new CreateSessionCommandHandler(
            _sessionRepoMock.Object,
            _unitOfWorkMock.Object,
            null!,
            _db,
            _loggerMock.Object);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_NullDbContext_ThrowsArgumentNullException()
    {
        var act = () => new CreateSessionCommandHandler(
            _sessionRepoMock.Object,
            _unitOfWorkMock.Object,
            _quotaServiceMock.Object,
            null!,
            _loggerMock.Object);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new CreateSessionCommandHandler(
            _sessionRepoMock.Object,
            _unitOfWorkMock.Object,
            _quotaServiceMock.Object,
            _db,
            null!);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsDomainException()
    {
        // Arrange — no user seeded in DB
        var command = new CreateSessionCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Generic",
            null,
            null,
            [new ParticipantDto { DisplayName = "Owner", IsOwner = true }]);

        // Act & Assert
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.DomainException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_QuotaExceeded_ThrowsSessionQuotaExceededException()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _db.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            Role = "user",
            Tier = "free"
        });
        await _db.SaveChangesAsync();

        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(
                userId,
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.UserTier>(),
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.Role>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Denied("Limit reached", 3, 3));

        var command = new CreateSessionCommand(
            userId,
            Guid.NewGuid(),
            "Generic",
            null,
            null,
            [new ParticipantDto { DisplayName = "Owner", IsOwner = true }]);

        // Act & Assert
        await Assert.ThrowsAsync<Api.BoundedContexts.SessionTracking.Domain.Exceptions.SessionQuotaExceededException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsCreateSessionResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _db.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            Role = "user",
            Tier = "free"
        });
        await _db.SaveChangesAsync();

        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(
                userId,
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.UserTier>(),
                It.IsAny<Api.SharedKernel.Domain.ValueObjects.Role>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Allowed(0, 3));

        var command = new CreateSessionCommand(
            userId,
            gameId,
            "Generic",
            null,
            null,
            [new ParticipantDto { DisplayName = "Owner", IsOwner = true, UserId = userId }]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.SessionId);
        Assert.NotEmpty(result.SessionCode);
        Assert.NotEmpty(result.Participants);

        _sessionRepoMock.Verify(r => r.AddAsync(It.IsAny<Api.BoundedContexts.SessionTracking.Domain.Entities.Session>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}

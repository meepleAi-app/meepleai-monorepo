using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DatabaseSync.Application.Commands;

/// <summary>
/// Unit tests for ApplyMigrationsHandler.
/// Tests constructor null guards and pure logic paths that do not require
/// a live database connection.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DatabaseSync")]
public sealed class ApplyMigrationsCommandHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IRemoteDatabaseConnector> _remoteConnector = new();
    private readonly Mock<ILogger<ApplyMigrationsHandler>> _logger = new();

    public ApplyMigrationsCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ApplyMigrationsTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(options, Mock.Of<IMediator>(), Mock.Of<IDomainEventCollector>());
    }

    // ── Constructor null guards ────────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenDbContextIsNull()
    {
        var act = () => new ApplyMigrationsHandler(null!, _remoteConnector.Object, _logger.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("dbContext");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenRemoteConnectorIsNull()
    {
        var act = () => new ApplyMigrationsHandler(_dbContext, null!, _logger.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("remoteConnector");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        var act = () => new ApplyMigrationsHandler(_dbContext, _remoteConnector.Object, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // ── Handle: pure logic paths ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenDirectionIsStagingToLocal_ReturnsFailureWithoutContactingRemote()
    {
        // Arrange
        var handler = new ApplyMigrationsHandler(_dbContext, _remoteConnector.Object, _logger.Object);
        var command = new ApplyMigrationsCommand(
            Direction: SyncDirection.StagingToLocal,
            Confirmation: "anything",
            AdminUserId: Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — should short-circuit before opening any remote connection
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not supported");
        _remoteConnector.Verify(c => c.OpenConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDirectionIsLocalToStaging_AttemptsToOpenRemoteConnection()
    {
        // Arrange — remote connector throws to stop execution before any real DB work
        _remoteConnector
            .Setup(c => c.OpenConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Tunnel not open"));

        var handler = new ApplyMigrationsHandler(_dbContext, _remoteConnector.Object, _logger.Object);
        var command = new ApplyMigrationsCommand(
            Direction: SyncDirection.LocalToStaging,
            Confirmation: "APPLY 0 MIGRATIONS TO STAGING",
            AdminUserId: Guid.NewGuid());

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert — handler must delegate to the remote connector for LocalToStaging
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tunnel not open");
        _remoteConnector.Verify(c => c.OpenConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

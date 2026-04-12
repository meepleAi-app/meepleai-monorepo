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
/// Unit tests for SyncTableDataHandler.
/// Tests constructor null guards and the confirmation-mismatch early-exit path
/// that does not require a live database connection.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DatabaseSync")]
public sealed class SyncTableDataCommandHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IRemoteDatabaseConnector> _remoteConnector = new();
    private readonly Mock<ILogger<SyncTableDataHandler>> _logger = new();

    public SyncTableDataCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"SyncTableDataTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(options, Mock.Of<IMediator>(), Mock.Of<IDomainEventCollector>());
    }

    // ── Constructor null guards ────────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenDbContextIsNull()
    {
        var act = () => new SyncTableDataHandler(null!, _remoteConnector.Object, _logger.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("dbContext");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenRemoteConnectorIsNull()
    {
        var act = () => new SyncTableDataHandler(_dbContext, null!, _logger.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("remoteConnector");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        var act = () => new SyncTableDataHandler(_dbContext, _remoteConnector.Object, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // ── Handle: confirmation mismatch early-exit ───────────────────────────────

    [Fact]
    public async Task Handle_LocalToStaging_WithCorrectConfirmation_ProceedsPastConfirmationCheck()
    {
        // Arrange — remote connector throws to stop execution after passing confirmation check.
        // Note: the handler opens the local DB connection (EF Core) before the remote connector,
        // so with InMemory the exception comes from EF Core's relational guard rather than
        // the connector. Either way, the test verifies the code passes the confirmation gate.
        _remoteConnector
            .Setup(c => c.OpenConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Tunnel not open"));

        var handler = new SyncTableDataHandler(_dbContext, _remoteConnector.Object, _logger.Object);
        var command = new SyncTableDataCommand(
            TableName: "games",
            Direction: SyncDirection.LocalToStaging,
            Confirmation: "SYNC games TO STAGING",
            AdminUserId: Guid.NewGuid());

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert — handler proceeds past the confirmation gate and throws during infrastructure work
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_StagingToLocal_WithCorrectConfirmation_ProceedsPastConfirmationCheck()
    {
        // Arrange — remote connector throws to stop execution after passing confirmation check.
        // Note: the handler opens the local DB connection before the remote one, so with InMemory
        // the exception comes from EF Core's relational guard rather than the connector.
        // Either way, the test verifies the handler does NOT return a Confirmation mismatch result.
        _remoteConnector
            .Setup(c => c.OpenConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Tunnel not open"));

        var handler = new SyncTableDataHandler(_dbContext, _remoteConnector.Object, _logger.Object);
        var command = new SyncTableDataCommand(
            TableName: "users",
            Direction: SyncDirection.StagingToLocal,
            Confirmation: "SYNC users TO LOCAL",
            AdminUserId: Guid.NewGuid());

        // Act — must throw (InMemory relational guard or connector error), not return gracefully
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert — handler proceeds past the confirmation gate and throws during infrastructure work
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_WithWrongConfirmation_ReturnsFailureWithoutContactingRemote()
    {
        // Arrange
        var handler = new SyncTableDataHandler(_dbContext, _remoteConnector.Object, _logger.Object);
        var command = new SyncTableDataCommand(
            TableName: "games",
            Direction: SyncDirection.LocalToStaging,
            Confirmation: "WRONG CONFIRMATION",
            AdminUserId: Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — must short-circuit before opening any connection
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Confirmation mismatch");
        _remoteConnector.Verify(c => c.OpenConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithCaseSensitiveWrongConfirmation_ReturnsFailure()
    {
        // Arrange — lowercase confirmation should NOT match the expected uppercase form
        var handler = new SyncTableDataHandler(_dbContext, _remoteConnector.Object, _logger.Object);
        var command = new SyncTableDataCommand(
            TableName: "users",
            Direction: SyncDirection.StagingToLocal,
            Confirmation: "sync users to local",
            AdminUserId: Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Confirmation mismatch");
        _remoteConnector.Verify(c => c.OpenConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyConfirmation_ReturnsFailureWithoutContactingRemote()
    {
        // Arrange
        var handler = new SyncTableDataHandler(_dbContext, _remoteConnector.Object, _logger.Object);
        var command = new SyncTableDataCommand(
            TableName: "games",
            Direction: SyncDirection.LocalToStaging,
            Confirmation: "",
            AdminUserId: Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Confirmation mismatch");
        _remoteConnector.Verify(c => c.OpenConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ConfirmationMismatch_ErrorMessageIncludesExpectedConfirmation()
    {
        // Arrange
        var handler = new SyncTableDataHandler(_dbContext, _remoteConnector.Object, _logger.Object);
        var command = new SyncTableDataCommand(
            TableName: "games",
            Direction: SyncDirection.LocalToStaging,
            Confirmation: "wrong",
            AdminUserId: Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — error message must expose the expected confirmation string
        result.ErrorMessage.Should().Contain("SYNC games TO STAGING");
    }
}

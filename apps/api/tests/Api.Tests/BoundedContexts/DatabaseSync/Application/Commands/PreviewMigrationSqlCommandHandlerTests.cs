using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DatabaseSync.Application.Commands;

/// <summary>
/// Unit tests for PreviewMigrationSqlHandler.
/// Tests constructor null guards and verifies that Handle delegates
/// to the remote connector as its first infrastructure call.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DatabaseSync")]
public sealed class PreviewMigrationSqlCommandHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IRemoteDatabaseConnector> _remoteConnector = new();

    public PreviewMigrationSqlCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"PreviewMigrationSqlTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(options, Mock.Of<IMediator>(), Mock.Of<IDomainEventCollector>());
    }

    // ── Constructor null guards ────────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenDbContextIsNull()
    {
        var act = () => new PreviewMigrationSqlHandler(null!, _remoteConnector.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("dbContext");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenRemoteConnectorIsNull()
    {
        var act = () => new PreviewMigrationSqlHandler(_dbContext, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("remoteConnector");
    }

    // ── Handle: infrastructure delegation ─────────────────────────────────────

    [Fact]
    public async Task Handle_AlwaysOpensRemoteConnection_BeforeQueryingMigrations()
    {
        // Arrange — remote connector throws to stop execution before accessing MigrationInfo
        _remoteConnector
            .Setup(c => c.OpenConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Tunnel not open"));

        var handler = new PreviewMigrationSqlHandler(_dbContext, _remoteConnector.Object);

        // Act
        var act = async () => await handler.Handle(new PreviewMigrationSqlCommand(), CancellationToken.None);

        // Assert — handler must call OpenConnectionAsync exactly once
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tunnel not open");
        _remoteConnector.Verify(c => c.OpenConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PassesCancellationToken_ToRemoteConnector()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _remoteConnector
            .Setup(c => c.OpenConnectionAsync(cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        var handler = new PreviewMigrationSqlHandler(_dbContext, _remoteConnector.Object);

        // Act
        var act = async () => await handler.Handle(new PreviewMigrationSqlCommand(), cts.Token);

        // Assert — the specific token must be forwarded
        await act.Should().ThrowAsync<OperationCanceledException>();
        _remoteConnector.Verify(c => c.OpenConnectionAsync(cts.Token), Times.Once);
    }
}

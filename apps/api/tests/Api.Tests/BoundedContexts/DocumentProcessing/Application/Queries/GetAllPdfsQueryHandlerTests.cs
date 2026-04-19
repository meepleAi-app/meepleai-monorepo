using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Unit tests for GetAllPdfsQueryHandler.
/// Task 5 (PDF SharedGameId migration): verifies that the handler
/// - maps ProcessingState "Ready" to Status "completed" (lowercase, API contract)
/// - filters by SharedGameId (not the legacy, now-removed GameId)
/// - joins SharedGames to populate GameTitle
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class GetAllPdfsQueryHandlerTests
{
    private static MeepleAiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    [Fact]
    public async Task Handle_WhenPdfIsReady_MapsStatusToCompleted_AndFiltersBySharedGameId()
    {
        // Arrange
        using var ctx = CreateDbContext();
        var sharedGameId = Guid.NewGuid();
        ctx.SharedGames.Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Test Game"
        });
        ctx.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            FileName = "test.pdf",
            FilePath = "/tmp/test.pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready"
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetAllPdfsQueryHandler(ctx);

        // Act
        var result = await handler.Handle(
            new GetAllPdfsQuery(
                Status: "completed",
                GameId: sharedGameId,
                PageSize: 10,
                Page: 1),
            TestContext.Current.CancellationToken);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].ProcessingState.Should().Be("Ready");
        result.Items[0].GameTitle.Should().Be("Test Game");
        result.Items[0].GameId.Should().Be(sharedGameId);
    }

    [Fact]
    public async Task Handle_WhenStatusCompletedFilter_ExcludesNonReadyPdfs()
    {
        // Arrange
        using var ctx = CreateDbContext();
        var sharedGameId = Guid.NewGuid();
        ctx.SharedGames.Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Catan"
        });

        ctx.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            FileName = "ready.pdf",
            FilePath = "/tmp/ready.pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready"
        });
        ctx.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            FileName = "pending.pdf",
            FilePath = "/tmp/pending.pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Pending"
        });
        ctx.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            FileName = "failed.pdf",
            FilePath = "/tmp/failed.pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Failed"
        });

        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetAllPdfsQueryHandler(ctx);

        // Act
        var result = await handler.Handle(
            new GetAllPdfsQuery(Status: "completed", PageSize: 10, Page: 1),
            TestContext.Current.CancellationToken);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].FileName.Should().Be("ready.pdf");
    }

    [Fact]
    public async Task Handle_WhenGameIdFilter_OnlyReturnsPdfsForThatSharedGame()
    {
        // Arrange
        using var ctx = CreateDbContext();
        var sharedGameA = Guid.NewGuid();
        var sharedGameB = Guid.NewGuid();
        ctx.SharedGames.Add(new SharedGameEntity { Id = sharedGameA, Title = "Game A" });
        ctx.SharedGames.Add(new SharedGameEntity { Id = sharedGameB, Title = "Game B" });

        ctx.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameA,
            FileName = "a.pdf",
            FilePath = "/tmp/a.pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready"
        });
        ctx.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameB,
            FileName = "b.pdf",
            FilePath = "/tmp/b.pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready"
        });

        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetAllPdfsQueryHandler(ctx);

        // Act
        var result = await handler.Handle(
            new GetAllPdfsQuery(GameId: sharedGameA, PageSize: 10, Page: 1),
            TestContext.Current.CancellationToken);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].FileName.Should().Be("a.pdf");
        result.Items[0].GameTitle.Should().Be("Game A");
    }

    [Fact]
    public async Task Handle_WhenSharedGameMissing_GameTitleIsNull()
    {
        // Arrange: PDF without SharedGameId (private PDF)
        using var ctx = CreateDbContext();
        ctx.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = null,
            PrivateGameId = Guid.NewGuid(),
            FileName = "private.pdf",
            FilePath = "/tmp/private.pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready"
        });

        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetAllPdfsQueryHandler(ctx);

        // Act
        var result = await handler.Handle(
            new GetAllPdfsQuery(PageSize: 10, Page: 1),
            TestContext.Current.CancellationToken);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].GameTitle.Should().BeNull();
        result.Items[0].GameId.Should().BeNull();
    }
}

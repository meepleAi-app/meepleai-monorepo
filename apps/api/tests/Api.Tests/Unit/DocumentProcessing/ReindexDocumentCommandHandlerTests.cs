using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1673")]
public sealed class ReindexDocumentCommandHandlerTests : IAsyncLifetime
{
    private MeepleAiDbContext _db = default!;
    private Mock<IMediator> _mediator = default!;

    public ValueTask InitializeAsync()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext($"reindex_{Guid.NewGuid():N}");
        _mediator = new Mock<IMediator>(MockBehavior.Strict);
        _mediator.Setup(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<PdfDocumentEntity> SeedPdfAsync(string state = "Ready", string? indexerVersion = null)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "test.pdf",
            FilePath = "/tmp/test.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = state,
            IndexerVersion = indexerVersion,
        };
        _db.PdfDocuments.Add(pdf);
        await _db.SaveChangesAsync();
        return pdf;
    }

    private ReindexDocumentCommandHandler CreateHandler() =>
        new(_db, _mediator.Object, NullLogger<ReindexDocumentCommandHandler>.Instance);

    [Fact]
    public async Task Handle_NoExplicitVersion_NoStoredVersion_UsesCurrent()
    {
        var pdf = await SeedPdfAsync();
        var handler = CreateHandler();

        await handler.Handle(new ReindexDocumentCommand(pdf.Id), CancellationToken.None);

        var reloaded = await _db.PdfDocuments.FirstAsync(p => p.Id == pdf.Id);
        // Asserted dynamically against Current so the SP3 #3269 bump (v1.0 -> v1.1) — and any
        // future bump — keeps this "uses Current" test honest instead of pinning a stale literal.
        reloaded.IndexerVersion.Should().Be(IndexerVersionRegistry.Current.Version);
    }

    [Fact]
    public async Task Handle_NoExplicitVersion_StoredVersionPresent_UsesStored()
    {
        var pdf = await SeedPdfAsync(indexerVersion: "v1.0");
        var handler = CreateHandler();

        await handler.Handle(new ReindexDocumentCommand(pdf.Id), CancellationToken.None);

        var reloaded = await _db.PdfDocuments.FirstAsync(p => p.Id == pdf.Id);
        reloaded.IndexerVersion.Should().Be("v1.0");
    }

    [Fact]
    public async Task Handle_ExplicitVersionOverridesStored()
    {
        var pdf = await SeedPdfAsync(indexerVersion: "v0");
        var handler = CreateHandler();

        await handler.Handle(new ReindexDocumentCommand(pdf.Id, "v1.0"), CancellationToken.None);

        var reloaded = await _db.PdfDocuments.FirstAsync(p => p.Id == pdf.Id);
        reloaded.IndexerVersion.Should().Be("v1.0");
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Uploading")]
    [InlineData("Extracting")]
    [InlineData("Chunking")]
    [InlineData("Embedding")]
    [InlineData("Indexing")]
    public async Task Handle_DocInFlight_ThrowsConflictException(string state)
    {
        var pdf = await SeedPdfAsync(state: state);
        var handler = CreateHandler();

        var act = () => handler.Handle(new ReindexDocumentCommand(pdf.Id), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.WithMessage($"*currently being processed*state={state}*");
    }

    [Theory]
    [InlineData("Ready")]
    [InlineData("Failed")]
    public async Task Handle_DocTerminalState_AllowsReindex(string state)
    {
        var pdf = await SeedPdfAsync(state: state);
        var handler = CreateHandler();

        await handler.Handle(new ReindexDocumentCommand(pdf.Id), CancellationToken.None);

        var reloaded = await _db.PdfDocuments.FirstAsync(p => p.Id == pdf.Id);
        reloaded.ProcessingState.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_PdfNotFound_ThrowsNotFoundException()
    {
        var handler = CreateHandler();
        var act = () => handler.Handle(new ReindexDocumentCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_Success_EnqueuesPdfForProcessing()
    {
        var pdf = await SeedPdfAsync();
        var handler = CreateHandler();

        await handler.Handle(new ReindexDocumentCommand(pdf.Id), CancellationToken.None);

        _mediator.Verify(m => m.Send(
            It.Is<EnqueuePdfCommand>(c => c.PdfDocumentId == pdf.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EnqueueThrowsConflict_RethrowsInsteadOfPhantomSuccess()
    {
        // B10 (#3269): the old handler committed the destructive reset and then SWALLOWED any
        // enqueue failure (queue full / transient) in a broad catch — a phantom success that left
        // the PDF reset-to-Pending with its chunks gone but no job to reprocess it. The handler
        // must now surface the failure (rolled back) so the caller gets a retryable 409.
        // NOTE: this asserts only the THROW. Proving the reset is actually rolled back needs a real
        // transaction — the InMemory provider has none — so the rollback is covered by
        // ReindexDocumentPersistsResetIntegrationTests (queue-saturation scenario).
        var pdf = await SeedPdfAsync(indexerVersion: "v1.0");

        _mediator.Setup(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("Queue is full. Maximum 100 jobs allowed."));

        var handler = CreateHandler();

        var act = () => handler.Handle(new ReindexDocumentCommand(pdf.Id, "v1.1"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*Queue is full*");
    }
}

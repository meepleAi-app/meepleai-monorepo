using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class IndexPdfIfIdleCommandHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext();
    private readonly Mock<IMediator> _mediator = new();
    private readonly IndexPdfIfIdleCommandHandler _sut;

    public IndexPdfIfIdleCommandHandlerTests()
    {
        _sut = new IndexPdfIfIdleCommandHandler(_db, _mediator.Object, Mock.Of<ILogger<IndexPdfIfIdleCommandHandler>>());
    }

    public void Dispose() => _db.Dispose();

    // Initializer minimo copiato da IndexPdfCommandHandlerTests.CreatePdfDocument (privato e con firma diversa).
    private async Task<Guid> SeedPdfAsync(string state)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "test.pdf",
            FilePath = "/uploads/test.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingState = state,
            ExtractedText = "text",
        };
        _db.PdfDocuments.Add(pdf);
        await _db.SaveChangesAsync();
        return pdf.Id;
    }

    [Fact]
    public async Task Handle_UnknownPdf_NotFound()
    {
        var result = await _sut.Handle(new IndexPdfIfIdleCommand(Guid.NewGuid()), CancellationToken.None);

        result.Outcome.Should().Be(IndexPdfIfIdleOutcome.NotFound);
        _mediator.Verify(m => m.Send(It.IsAny<IndexPdfCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Uploading")]
    [InlineData("Extracting")]
    [InlineData("Indexing")]
    public async Task Handle_PipelineState_PipelineBusyWithoutIndexing(string state)
    {
        var id = await SeedPdfAsync(state);

        var result = await _sut.Handle(new IndexPdfIfIdleCommand(id), CancellationToken.None);

        result.Outcome.Should().Be(IndexPdfIfIdleOutcome.PipelineBusy);
        _mediator.Verify(m => m.Send(It.IsAny<IndexPdfCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ActiveProcessingJob_PipelineBusy()
    {
        var id = await SeedPdfAsync("Failed");
        _db.ProcessingJobs.Add(new ProcessingJobEntity { Id = Guid.NewGuid(), PdfDocumentId = id, Status = "Queued" });
        await _db.SaveChangesAsync();

        var result = await _sut.Handle(new IndexPdfIfIdleCommand(id), CancellationToken.None);

        result.Outcome.Should().Be(IndexPdfIfIdleOutcome.PipelineBusy);
    }

    [Fact]
    public async Task Handle_ReadyWithVectors_AlreadyIndexed()
    {
        var id = await SeedPdfAsync("Ready");
        _db.VectorDocuments.Add(new VectorDocumentEntity { Id = Guid.NewGuid(), PdfDocumentId = id, IndexingStatus = "completed" });
        await _db.SaveChangesAsync();

        var result = await _sut.Handle(new IndexPdfIfIdleCommand(id), CancellationToken.None);

        result.Outcome.Should().Be(IndexPdfIfIdleOutcome.AlreadyIndexed);
        _mediator.Verify(m => m.Send(It.IsAny<IndexPdfCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Ready")]
    public async Task Handle_IdleWithoutVectors_SendsIndexPdfCommand(string state)
    {
        var id = await SeedPdfAsync(state);
        _mediator.Setup(m => m.Send(It.Is<IndexPdfCommand>(c => c.PdfId == id.ToString()), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IndexingResultDto.CreateSuccess("vec-1", 3, DateTime.UtcNow));

        var result = await _sut.Handle(new IndexPdfIfIdleCommand(id), CancellationToken.None);

        result.Outcome.Should().Be(IndexPdfIfIdleOutcome.Indexed);
        result.Indexing!.ChunkCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_IndexingFails_Failed()
    {
        var id = await SeedPdfAsync("Failed");
        _mediator.Setup(m => m.Send(It.IsAny<IndexPdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexingResultDto { Success = false, ErrorCode = PdfIndexingErrorCode.EmbeddingFailed, ErrorMessage = "boom" });

        var result = await _sut.Handle(new IndexPdfIfIdleCommand(id), CancellationToken.None);

        result.Outcome.Should().Be(IndexPdfIfIdleOutcome.Failed);
        result.Indexing!.ErrorMessage.Should().Be("boom");
    }
}

using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Regression for #3288: <see cref="CompleteChunkedUploadCommandHandler.TriggerPdfProcessingAsync"/>
/// loads the PdfDocument via a bare <c>db.PdfDocuments.FindAsync(...)</c>. The DbContext defaults to
/// <c>QueryTrackingBehavior.NoTracking</c> (PERF-06) and — as the sibling
/// <c>UploadPdfCommandHandler.Processing.cs</c> documents — <c>FindAsync</c> does NOT override the
/// per-DbContext default, so the entity is returned untracked. Every subsequent
/// <c>pdfDoc.ProcessingState</c> write is therefore a silent no-op at <c>SaveChangesAsync</c>: a PDF
/// whose text extraction fails never records the <c>Failed</c> state and is stuck in-flight forever.
///
/// This runs against a real Postgres (NoTracking default) with a fresh background-task scope, so it
/// exercises the true EF pipeline. The existing Category=Unit
/// <c>CompleteChunkedUploadCommandHandlerZeroChunkTests</c> misses this because it pre-Adds the entity
/// to an in-memory tracked context and shares that same context with the handler's scope, masking the
/// detached behavior.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3288")]
public sealed class CompleteChunkedUploadNoTrackingPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"complete_chunked_notracking_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;

    public CompleteChunkedUploadNoTrackingPersistenceIntegrationTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(conn);
        _factory = IntegrationWebApplicationFactory.Create(conn);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory != null) await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact(DisplayName = "#3288: TriggerPdfProcessingAsync persists the Failed state when extraction fails (NoTracking silent no-op)")]
    public async Task TriggerPdfProcessing_WhenExtractionFails_PersistsFailedState()
    {
        var pdfId = Guid.NewGuid();

        // Arrange — seed a user (required FK) and a PDF stuck in Extracting, via one scope.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var userId = Guid.NewGuid();
            db.Users.Add(new UserEntity
            {
                Id = userId,
                Email = $"test-{userId:N}@example.com",
                DisplayName = "Test User",
                Role = "user",
                CreatedAt = DateTime.UtcNow
            });
            db.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = pdfId,
                FileName = "test.pdf",
                FilePath = $"pdfs/{pdfId:N}/test.pdf",
                FileSizeBytes = 1024,
                ContentType = "application/pdf",
                UploadedByUserId = userId,
                UploadedAt = DateTime.UtcNow,
                ProcessingState = nameof(PdfProcessingState.Extracting),
                Language = "en"
            });
            await db.SaveChangesAsync();
        }

        // The handler's background scope resolves a *fresh* NoTracking MeepleAiDbContext from the
        // real IServiceScopeFactory — so FindAsync must query the DB and returns the entity untracked.
        var scopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();

        // Extraction fails so the handler takes the failure path (sets ProcessingState = Failed).
        var extractorMock = new Mock<IPdfTextExtractor>();
        extractorMock
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedTextExtractionResult.CreateFailure("simulated extraction failure"));

        using var ctorScope = _factory.Services.CreateScope();
        var ctorDb = ctorScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var handler = new CompleteChunkedUploadCommandHandler(
            Mock.Of<IChunkedUploadSessionRepository>(),
            ctorDb, // ctor dependency only; TriggerPdfProcessingAsync uses the scope-resolved context
            Mock.Of<IBlobStorageService>(),
            Mock.Of<IBackgroundTaskService>(),
            NullLogger<CompleteChunkedUploadCommandHandler>.Instance,
            scopeFactory,
            extractorMock.Object,
            Mock.Of<IPdfTableExtractor>(),
            Mock.Of<IMediator>(),
            Mock.Of<IPdfDeduplicationService>(),
            TimeProvider.System);

        // The method opens the file with a real FileStream, so a real temp file must exist.
        var tmp = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmp, "dummy pdf bytes");

        // Act
        try
        {
            await handler.TriggerPdfProcessingAsync(pdfId.ToString(), tmp, CancellationToken.None);
        }
        finally
        {
            File.Delete(tmp);
        }

        // Assert — read with a fresh NoTracking context; the Failed write must have persisted.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var entity = await db.PdfDocuments.AsNoTracking().FirstAsync(x => x.Id == pdfId);
            entity.ProcessingState.Should().Be(nameof(PdfProcessingState.Failed),
                "TriggerPdfProcessingAsync must load pdfDoc with .AsTracking() so the Failed state write is persisted (#3288 — NoTracking silent no-op)");
            entity.ProcessingError.Should().NotBeNullOrEmpty(
                "the extraction error message must be recorded alongside the Failed state");
        }
    }
}

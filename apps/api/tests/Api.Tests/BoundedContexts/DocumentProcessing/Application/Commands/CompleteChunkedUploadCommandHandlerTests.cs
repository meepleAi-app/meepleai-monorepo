using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Infrastructure;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// #4442: il caricamento a pezzi elaborava il PDF con una propria copia della pipeline, che apriva
/// <c>BlobStorageResult.FilePath</c> come file locale. Con lo storage S3 quel valore è la chiave
/// dell'oggetto (<c>pdfs/&lt;gioco&gt;/&lt;fileId&gt;_&lt;nome&gt;.pdf</c>), quindi ogni caricamento a pezzi finiva
/// <c>Failed</c> con «Could not find a part of the path '/app/pdfs/…'». Ora il completamento salva il
/// documento e lo affida alla coda, la cui pipeline legge dallo storage.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4442")]
public class CompleteChunkedUploadCommandHandlerTests
{
    [Fact]
    public async Task Complete_WithAnObjectKeyNotOnDisk_DoesNotProcessTheFileItself_ItEnqueuesIt()
    {
        // Arrange: lo storage restituisce una chiave S3, che non esiste come file locale.
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var fileId = Guid.NewGuid().ToString("N");
        var objectKey = $"pdfs/{gameId}/{fileId}_rules.pdf";
        var background = new InlineBackgroundTaskService();
        var mediator = new Mock<IMediator>();

        using var upload = TempChunkedUpload.Create(gameId, userId);
        var handler = CreateHandler(db, upload.Session, objectKey, fileId, background, mediator.Object);

        // Act
        var result = await handler.Handle(new CompleteChunkedUploadCommand(upload.Session.Id, userId), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue(result.ErrorMessage);
        var pdf = await db.PdfDocuments.AsNoTracking().SingleAsync();
        pdf.FilePath.Should().Be(objectKey);
        pdf.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending),
            "il completamento non elabora più il file: lo fa la pipeline della coda, che legge dallo storage");
        pdf.ProcessingError.Should().BeNull();

        mediator.Verify(m => m.Send(
                It.Is<EnqueuePdfCommand>(c => c.PdfDocumentId == pdf.Id && c.UserId == userId),
                It.IsAny<CancellationToken>()),
            Times.Once, "il documento va affidato alla coda");
        background.TaskIds.Should().NotContain(fileId, "nessuna elaborazione in background a nome del file");
    }

    [Fact]
    public async Task Complete_EnqueuesWithTheInteractivePriority_AndBypassesTheQueueCap()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var fileId = Guid.NewGuid().ToString("N");
        var mediator = new Mock<IMediator>();
        EnqueuePdfCommand? sent = null;
        mediator.Setup(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Guid>, CancellationToken>((c, _) => sent = (EnqueuePdfCommand)c)
            .ReturnsAsync(Guid.NewGuid());

        using var upload = TempChunkedUpload.Create(gameId, userId);
        var handler = CreateHandler(db, upload.Session, $"pdfs/{gameId}/{fileId}_rules.pdf", fileId,
            new InlineBackgroundTaskService(), mediator.Object);

        // Act
        var result = await handler.Handle(new CompleteChunkedUploadCommand(upload.Session.Id, userId), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue(result.ErrorMessage);
        sent.Should().NotBeNull();
        sent!.Priority.Should().Be(CompleteChunkedUploadCommandHandler.InteractiveUploadPriority,
            "stessa priorità del «Normale» del pannello di upload");
        sent.BypassQueueCap.Should().BeTrue(
            "un upload interattivo di un admin non va rifiutato perché una reindicizzazione ha riempito la coda (decisione 1a)");
    }

    /// <summary>#4443 R2: «Urgente» del pannello vale anche per il caricamento a pezzi (prima la priorità era fissa).</summary>
    [Fact]
    [Trait("Issue", "4443")]
    public async Task Complete_WithUrgentPriority_EnqueuesAtUrgent()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var fileId = Guid.NewGuid().ToString("N");
        var mediator = new Mock<IMediator>();
        EnqueuePdfCommand? sent = null;
        mediator.Setup(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Guid>, CancellationToken>((c, _) => sent = (EnqueuePdfCommand)c)
            .ReturnsAsync(Guid.NewGuid());

        using var upload = TempChunkedUpload.Create(gameId, userId);
        var handler = CreateHandler(db, upload.Session, $"pdfs/{gameId}/{fileId}_rules.pdf", fileId,
            new InlineBackgroundTaskService(), mediator.Object);

        var result = await handler.Handle(
            new CompleteChunkedUploadCommand(upload.Session.Id, userId, Priority: "urgent"), CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        sent!.Priority.Should().Be((int)ProcessingPriority.Urgent);
    }

    [Fact]
    public async Task Complete_WhenTheEnqueueFails_ReportsTheFailure()
    {
        // Arrange: la coda rifiuta il job. (Che record e job si annullino insieme lo prova il test di integrazione su Postgres: InMemory ignora le transazioni.)
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var fileId = Guid.NewGuid().ToString("N");
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated enqueue failure"));

        using var upload = TempChunkedUpload.Create(gameId, userId);
        var handler = CreateHandler(db, upload.Session, $"pdfs/{gameId}/{fileId}_rules.pdf", fileId,
            new InlineBackgroundTaskService(), mediator.Object);

        // Act
        var result = await handler.Handle(new CompleteChunkedUploadCommand(upload.Session.Id, userId), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse("un documento senza job resterebbe Pending per sempre");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Complete_CreatesTheRecordLinkedToItsGame(bool sharedGame)
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var fileId = Guid.NewGuid().ToString("N");

        using var upload = TempChunkedUpload.Create(sharedGame ? gameId : null, userId, privateGameId: sharedGame ? null : gameId);
        var handler = CreateHandler(db, upload.Session, $"pdfs/{gameId}/{fileId}_rules.pdf", fileId,
            new InlineBackgroundTaskService(), Mock.Of<IMediator>());

        // Act
        var result = await handler.Handle(new CompleteChunkedUploadCommand(upload.Session.Id, userId), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue(result.ErrorMessage);
        var pdf = await db.PdfDocuments.AsNoTracking().SingleAsync();
        if (sharedGame)
        {
            pdf.SharedGameId.Should().Be(gameId, "il recupero trova i documenti di un gioco del catalogo per SharedGameId (#4395)");
            pdf.PrivateGameId.Should().BeNull();
        }
        else
        {
            pdf.PrivateGameId.Should().Be(gameId);
            pdf.SharedGameId.Should().BeNull();
        }
    }

    private static CompleteChunkedUploadCommandHandler CreateHandler(
        MeepleAiDbContext db,
        ChunkedUploadSession session,
        string filePath,
        string fileId,
        IBackgroundTaskService background,
        IMediator mediator)
    {
        var sessions = new Mock<IChunkedUploadSessionRepository>();
        sessions.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var blobs = new Mock<IBlobStorageService>();
        blobs.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, fileId, filePath, 14));

        var dedup = new Mock<IPdfDeduplicationService>();
        dedup.Setup(d => d.EvaluateAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDedupResult(PdfDedupDecision.NewUpload, null, "h"));

        return new CompleteChunkedUploadCommandHandler(
            sessions.Object,
            db,
            blobs.Object,
            background,
            NullLogger<CompleteChunkedUploadCommandHandler>.Instance,
            mediator,
            dedup.Object,
            TimeProvider.System);
    }

    /// <summary>Esegue subito ciò che riceve e ne ricorda gli identificativi.</summary>
    private sealed class InlineBackgroundTaskService : IBackgroundTaskService
    {
        public List<string> TaskIds { get; } = [];

        public void Execute(Func<Task> task) => task().GetAwaiter().GetResult();

        public void ExecuteWithCancellation(string taskId, Func<CancellationToken, Task> taskFactory)
        {
            TaskIds.Add(taskId);
            taskFactory(CancellationToken.None).GetAwaiter().GetResult();
        }

        public bool CancelTask(string taskId) => false;
    }

    /// <summary>Una sessione di caricamento a pezzi completa, con il suo pezzo su disco.</summary>
    private sealed class TempChunkedUpload : IDisposable
    {
        private TempChunkedUpload(ChunkedUploadSession session) => Session = session;

        public ChunkedUploadSession Session { get; }

        public static TempChunkedUpload Create(Guid? gameId, Guid userId, Guid? privateGameId = null)
        {
            var sessionId = Guid.NewGuid();
            var tempDir = Path.Combine(Path.GetTempPath(), "meepleai_uploads", sessionId.ToString());
            Directory.CreateDirectory(tempDir);
            var session = new ChunkedUploadSession(sessionId, gameId, userId, "rules.pdf", totalFileSize: 14,
                tempDirectory: tempDir, privateGameId: privateGameId);
            File.WriteAllText(session.GetChunkFilePath(0), "dummy pdf data");
            session.MarkChunkReceived(0);
            return new TempChunkedUpload(session);
        }

        public void Dispose()
        {
            if (Directory.Exists(Session.TempDirectory))
            {
                Directory.Delete(Session.TempDirectory, recursive: true);
            }
        }
    }
}

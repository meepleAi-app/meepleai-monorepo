using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Channels;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundServices;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.BackgroundServices;

/// <summary>
/// #4660 — <c>KbReindexJob.FailureReason</c> lo serve <c>GET /games/{id}/kb/reindex/{jobId}/status</c> a chi ha
/// avviato il re-index, che può essere un utente semplice. Porta un testo fisso, mai il messaggio dell'eccezione
/// (un errore di database nomina un host e una porta, un errore del servizio di embedding il suo indirizzo). Il
/// dettaglio sta nel log, dove il processore lo scrive già con l'eccezione.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4660")]
public sealed class KbReindexProcessorServiceFailureTextTests : IDisposable
{
    private const string Secret = "Failed to connect to secret-host.internal:5432";

    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext();
    private readonly Mock<IKbReindexJobRepository> _jobs = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly RecordingLogger<KbReindexProcessorService> _log = new();

    public void Dispose() => _db.Dispose();

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_jobs.Object);
        services.AddSingleton(Mock.Of<IUnitOfWork>());
        services.AddSingleton(_mediator.Object);
        services.AddSingleton(_db);
        return services.BuildServiceProvider();
    }

    private async Task<KbReindexJob> RunAsync(KbReindexJob job)
    {
        using var provider = BuildProvider();
        var channel = new KbReindexChannel();
        var sut = new KbReindexProcessorService(channel, provider.GetRequiredService<IServiceScopeFactory>(), _log);
        await sut.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await channel.Writer.WriteAsync(new KbReindexJobRequest(job.Id, _gameId, _userId), TestContext.Current.CancellationToken);

            // Si aspetta lo stato osservabile, non un tempo: il processore gira su un suo thread.
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (job.Status != "failed" && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20, TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }

        return job;
    }

    private Guid SeedIndexablePdf()
    {
        var pdfId = Guid.NewGuid();
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = _gameId,
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 100,
            ContentType = "application/pdf",
            UploadedByUserId = _userId,
            ExtractedText = "some extracted text",
            ProcessingState = "Ready",
        });
        _db.SaveChanges();
        return pdfId;
    }

    [Fact]
    public async Task WhenAPdfFailsToReindex_TheJobCarriesTheFixedText_AndTheLogTheDetail()
    {
        var pdfId = SeedIndexablePdf();
        var job = KbReindexJob.Create(_gameId, _userId, totalPdfs: 1);
        _jobs.Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _mediator.Setup(m => m.Send(It.IsAny<IndexPdfCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(Secret));

        await RunAsync(job);

        job.Status.Should().Be("failed");
        job.FailureReason.Should().Be(KbReindexProcessorService.PdfFailureText(pdfId));
        job.FailureReason.Should().NotContain("secret-host");
        _log.Entries.Should().Contain(l => l.StartsWith("Error:", StringComparison.Ordinal) && l.Contains(Secret, StringComparison.Ordinal),
            "the detail is in the log, as an error, with the exception");
    }

    [Fact]
    public async Task WhenTheWorkerItselfFails_TheJobCarriesTheFixedText_AndTheLogTheDetail()
    {
        var job = KbReindexJob.Create(_gameId, _userId, totalPdfs: 1);
        _jobs.SetupSequence(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(Secret))
            .ReturnsAsync(job);

        await RunAsync(job);

        job.Status.Should().Be("failed");
        job.FailureReason.Should().Be(KbReindexProcessorService.WorkerFailureText);
        job.FailureReason.Should().NotContain("secret-host");
        _log.Entries.Should().Contain(l => l.StartsWith("Error:", StringComparison.Ordinal) && l.Contains(Secret, StringComparison.Ordinal));
    }
}

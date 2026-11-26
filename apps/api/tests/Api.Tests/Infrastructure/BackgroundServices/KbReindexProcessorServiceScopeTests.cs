using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.BackgroundServices;

/// <summary>
/// #5162 — <c>IndexPdfCommandHandler</c> leaves the text chunks and the embeddings of its PDF tracked. The re-index
/// job sent every PDF through the mediator of its own scope, so the tracker grew PDF after PDF, and the transaction
/// of each later PDF photographed all of it for the rewind of a retry. Every PDF now gets a scope of its own.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5162")]
public sealed class KbReindexProcessorServiceScopeTests : IDisposable
{
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task EveryPdfIsIndexedInAScopeOfItsOwn()
    {
        SeedIndexablePdf();
        SeedIndexablePdf();
        var job = KbReindexJob.Create(_gameId, _userId, totalPdfs: 2);
        var jobs = new Mock<IKbReindexJobRepository>();
        jobs.Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);

        // The scope a mediator was resolved from, for every IndexPdfCommand it sent.
        var sendingScopes = new List<IServiceProvider>();
        var services = new ServiceCollection();
        services.AddSingleton(jobs.Object);
        services.AddSingleton(Mock.Of<IUnitOfWork>());
        services.AddSingleton(_db);
        services.AddScoped<IMediator>(scope =>
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<IndexPdfCommand>(), It.IsAny<CancellationToken>()))
                .Callback(() => { lock (sendingScopes) sendingScopes.Add(scope); })
                .ReturnsAsync(new IndexingResultDto { Success = true });
            return mediator.Object;
        });
        await using var provider = services.BuildServiceProvider();

        var channel = new KbReindexChannel();
        var sut = new KbReindexProcessorService(
            channel, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<KbReindexProcessorService>.Instance);
        await sut.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await channel.Writer.WriteAsync(new KbReindexJobRequest(job.Id, _gameId, _userId), TestContext.Current.CancellationToken);

            // The observable state, not a delay: the processor runs on a thread of its own.
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (job.Status != "completed" && DateTime.UtcNow < deadline)
                await Task.Delay(20, TestContext.Current.CancellationToken);
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }

        job.Status.Should().Be("completed");
        sendingScopes.Should().HaveCount(2);
        sendingScopes.Distinct(ReferenceEqualityComparer.Instance).Should().HaveCount(2, "each PDF is indexed in a scope of its own");
    }

    private void SeedIndexablePdf()
    {
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
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
    }
}

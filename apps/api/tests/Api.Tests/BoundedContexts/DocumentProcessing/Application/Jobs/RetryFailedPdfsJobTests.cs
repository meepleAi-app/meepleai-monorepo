using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Jobs;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// Quali PDF <c>Failed</c> il job di retry automatico riprende. La regola è una lista di categorie
/// ritentabili (Network, Service, Unknown, più le righe senza categoria): quello che non ci sta dentro non
/// viene ritentato. #4610: un PDF annullato dall'utente (<see cref="ErrorCategory.Cancelled"/>) ci stava
/// dentro per sbaglio, perché finiva <c>Unknown</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4610")]
public sealed class RetryFailedPdfsJobTests
{
    private static PdfDocumentEntity FailedPdf(Guid id, string? errorCategory, int retryCount = 0) =>
        new()
        {
            Id = id,
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow.AddMinutes(-30),
            ProcessingState = "Failed",
            ProcessingError = "boom",
            ErrorCategory = errorCategory,
            RetryCount = retryCount,
            Language = "en",
            Tags = new List<string>(),
        };

    private static IJobExecutionContext FakeContext()
    {
        var ctx = new Mock<IJobExecutionContext>();
        ctx.SetupGet(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        ctx.SetupProperty(c => c.Result);
        return ctx.Object;
    }

    private static async Task<IReadOnlyList<Guid>> RunJobAsync(params PdfDocumentEntity[] pdfs)
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.PdfDocuments.AddRange(pdfs);
        await db.SaveChangesAsync();

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<RetryPdfProcessingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetryPdfProcessingResult(true, "Pending", 1));

        await new RetryFailedPdfsJob(db, mediator.Object, NullLogger<RetryFailedPdfsJob>.Instance).Execute(FakeContext());

        return mediator.Invocations
            .Select(i => i.Arguments[0])
            .OfType<RetryPdfProcessingCommand>()
            .Select(c => c.PdfId)
            .ToList();
    }

    [Fact]
    public async Task Execute_PdfCancelledByTheUser_IsNotRetried()
    {
        var cancelled = Guid.NewGuid();

        var retried = await RunJobAsync(FailedPdf(cancelled, nameof(ErrorCategory.Cancelled)));

        retried.Should().BeEmpty("the user asked to stop it; a manual retry stays possible, an automatic one does not");
    }

    [Theory]
    [InlineData(nameof(ErrorCategory.Network))]
    [InlineData(nameof(ErrorCategory.Service))]
    [InlineData(nameof(ErrorCategory.Unknown))]
    [InlineData(null)]
    public async Task Execute_TransientOrUncategorisedFailure_IsRetried(string? category)
    {
        var failed = Guid.NewGuid();

        var retried = await RunJobAsync(FailedPdf(failed, category));

        retried.Should().ContainSingle().Which.Should().Be(failed);
    }
}

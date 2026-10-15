using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4408: a failed PDF is finished, not "still being processed". It used to block every question on
/// its game, with a message that promised a processing that would never come.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4408")]
public sealed class DocumentReadinessTests
{
    private static PdfDocument Ready() => new PdfDocumentBuilder().ThatIsCompleted().Build();
    private static PdfDocument Failed() => new PdfDocumentBuilder().ThatIsFailed().Build();
    private static PdfDocument InProgress() => new PdfDocumentBuilder().ThatIsProcessing().Build();
    private static PdfDocument Pending() => new PdfDocumentBuilder().Build();

    [Fact]
    public void AReadyPdf_AndAFailedOne_CanBeAnswered()
    {
        var readiness = DocumentReadiness.Of([Ready(), Failed()]);

        readiness.CanAnswer.Should().BeTrue();
        readiness.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void APdfInProgress_StopsTheQuestion_AndOnlyItIsCounted()
    {
        var readiness = DocumentReadiness.Of([Ready(), Failed(), InProgress()]);

        readiness.ErrorCode.Should().Be(DocumentReadiness.ProcessingErrorCode);
        readiness.ErrorMessage.Should().StartWith("1 of 3 documents are still being processed");
    }

    [Fact]
    public void APendingPdf_IsStillToBeProcessed()
    {
        DocumentReadiness.Of([Ready(), Pending()]).ErrorCode.Should().Be(DocumentReadiness.ProcessingErrorCode);
    }

    [Fact]
    public void OnlyFailedPdfs_SayTheyFailed()
    {
        var readiness = DocumentReadiness.Of([Failed(), Failed()]);

        readiness.ErrorCode.Should().Be(DocumentReadiness.FailedErrorCode);
        readiness.ErrorMessage.Should().NotContain("still being processed");
    }

    [Fact]
    public void FailedPdfs_AndOneInProgress_AreStillWaitingForIt()
    {
        // The one in progress may still give the game content to answer from.
        DocumentReadiness.Of([Failed(), InProgress()]).ErrorCode.Should().Be(DocumentReadiness.ProcessingErrorCode);
    }

    [Fact]
    public void AGameWithoutPdfs_IsNotBlockedHere()
    {
        DocumentReadiness.Of([]).CanAnswer.Should().BeTrue();
        DocumentReadiness.Of(null).CanAnswer.Should().BeTrue();
    }
}

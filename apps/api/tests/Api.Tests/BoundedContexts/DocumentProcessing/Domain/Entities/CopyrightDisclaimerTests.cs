using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// Unit tests for copyright disclaimer and RAG toggle (Issue #5446).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CopyrightDisclaimerTests
{
    #region AcceptCopyrightDisclaimer

    [Fact]
    public void AcceptCopyrightDisclaimer_WithValidUserId_SetsFields()
    {
        var document = CreateDocument();
        var userId = Guid.NewGuid();

        document.AcceptCopyrightDisclaimer(userId);

        Assert.NotNull(document.CopyrightDisclaimerAcceptedAt);
        Assert.Equal(userId, document.CopyrightDisclaimerAcceptedBy);
        Assert.True(document.HasAcceptedDisclaimer);
    }

    [Fact]
    public void AcceptCopyrightDisclaimer_WithEmptyGuid_ThrowsArgumentException()
    {
        var document = CreateDocument();

        Assert.Throws<ArgumentException>(() => document.AcceptCopyrightDisclaimer(Guid.Empty));
    }

    [Fact]
    public void HasAcceptedDisclaimer_DefaultIsFalse()
    {
        var document = CreateDocument();

        Assert.False(document.HasAcceptedDisclaimer);
        Assert.Null(document.CopyrightDisclaimerAcceptedAt);
        Assert.Null(document.CopyrightDisclaimerAcceptedBy);
    }

    [Fact]
    public void AcceptCopyrightDisclaimer_CalledTwice_UpdatesTimestamp()
    {
        var document = CreateDocument();
        var userId = Guid.NewGuid();

        document.AcceptCopyrightDisclaimer(userId);
        var firstAcceptedAt = document.CopyrightDisclaimerAcceptedAt;

        document.AcceptCopyrightDisclaimer(userId);

        Assert.True(document.HasAcceptedDisclaimer);
        Assert.True(document.CopyrightDisclaimerAcceptedAt >= firstAcceptedAt);
    }

    #endregion

    #region SetActiveForRag

    [Fact]
    public void IsActiveForRag_DefaultIsTrue()
    {
        var document = CreateDocument();

        Assert.True(document.IsActiveForRag);
    }

    [Fact]
    public void SetActiveForRag_ToFalse_DeactivatesDocument()
    {
        var document = CreateDocument();

        document.SetActiveForRag(false);

        Assert.False(document.IsActiveForRag);
    }

    [Fact]
    public void SetActiveForRag_ToTrue_ActivatesDocument()
    {
        var document = CreateDocument();
        document.SetActiveForRag(false);

        document.SetActiveForRag(true);

        Assert.True(document.IsActiveForRag);
    }

    [Fact]
    public void SetActiveForRag_ToggleMultipleTimes_MaintainsLastValue()
    {
        var document = CreateDocument();

        document.SetActiveForRag(false);
        document.SetActiveForRag(true);
        document.SetActiveForRag(false);

        Assert.False(document.IsActiveForRag);
    }

    #endregion

    #region Reconstitute

    [Fact]
    public void Reconstitute_WithDisclaimerFields_PreservesValues()
    {
        var acceptedAt = DateTime.UtcNow.AddHours(-1);
        var acceptedBy = Guid.NewGuid();

        var document = PdfDocument.Reconstitute(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            fileName: new FileName("test.pdf"),
            filePath: "/path/test.pdf",
            fileSize: new FileSize(1024),
            uploadedByUserId: Guid.NewGuid(),
            uploadedAt: DateTime.UtcNow,
            processingStatus: "pending",
            processedAt: null,
            pageCount: null,
            processingError: null,
            language: LanguageCode.English,
            copyrightDisclaimerAcceptedAt: acceptedAt,
            copyrightDisclaimerAcceptedBy: acceptedBy,
            isActiveForRag: false);

        Assert.Equal(acceptedAt, document.CopyrightDisclaimerAcceptedAt);
        Assert.Equal(acceptedBy, document.CopyrightDisclaimerAcceptedBy);
        Assert.True(document.HasAcceptedDisclaimer);
        Assert.False(document.IsActiveForRag);
    }

    [Fact]
    public void Reconstitute_WithDefaults_HasCorrectDisclaimerState()
    {
        var document = PdfDocument.Reconstitute(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            fileName: new FileName("test.pdf"),
            filePath: "/path/test.pdf",
            fileSize: new FileSize(1024),
            uploadedByUserId: Guid.NewGuid(),
            uploadedAt: DateTime.UtcNow,
            processingStatus: "pending",
            processedAt: null,
            pageCount: null,
            processingError: null,
            language: LanguageCode.English);

        Assert.False(document.HasAcceptedDisclaimer);
        Assert.True(document.IsActiveForRag);
    }

    #endregion

    private static PdfDocument CreateDocument()
    {
        return new PdfDocument(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new FileName("test.pdf"),
            "/path/to/test.pdf",
            new FileSize(1024),
            Guid.NewGuid());
    }
}

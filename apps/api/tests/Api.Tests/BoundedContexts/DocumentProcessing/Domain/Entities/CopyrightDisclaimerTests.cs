using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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

        document.CopyrightDisclaimerAcceptedAt.Should().NotBeNull();
        document.CopyrightDisclaimerAcceptedBy.Should().Be(userId);
        document.HasAcceptedDisclaimer.Should().BeTrue();
    }

    [Fact]
    public void AcceptCopyrightDisclaimer_WithEmptyGuid_ThrowsArgumentException()
    {
        var document = CreateDocument();

        ((Action)(() => document.AcceptCopyrightDisclaimer(Guid.Empty))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HasAcceptedDisclaimer_DefaultIsFalse()
    {
        var document = CreateDocument();

        document.HasAcceptedDisclaimer.Should().BeFalse();
        document.CopyrightDisclaimerAcceptedAt.Should().BeNull();
        document.CopyrightDisclaimerAcceptedBy.Should().BeNull();
    }

    [Fact]
    public void AcceptCopyrightDisclaimer_CalledTwice_ThrowsInvalidOperationException()
    {
        // Issue #5446: Accepting the disclaimer twice must be rejected (use ConflictException at app layer).
        var document = CreateDocument();
        var userId = Guid.NewGuid();

        document.AcceptCopyrightDisclaimer(userId);

        ((Action)(() => document.AcceptCopyrightDisclaimer(userId))).Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region SetActiveForRag

    [Fact]
    public void IsActiveForRag_DefaultIsTrue()
    {
        var document = CreateDocument();

        document.IsActiveForRag.Should().BeTrue();
    }

    [Fact]
    public void SetActiveForRag_ToFalse_DeactivatesDocument()
    {
        var document = CreateDocument();

        document.SetActiveForRag(false);

        document.IsActiveForRag.Should().BeFalse();
    }

    [Fact]
    public void SetActiveForRag_ToTrue_ActivatesDocument()
    {
        var document = CreateDocument();
        document.SetActiveForRag(false);

        document.SetActiveForRag(true);

        document.IsActiveForRag.Should().BeTrue();
    }

    [Fact]
    public void SetActiveForRag_ToggleMultipleTimes_MaintainsLastValue()
    {
        var document = CreateDocument();

        document.SetActiveForRag(false);
        document.SetActiveForRag(true);
        document.SetActiveForRag(false);

        document.IsActiveForRag.Should().BeFalse();
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
            processedAt: null,
            pageCount: null,
            processingError: null,
            language: LanguageCode.English,
            copyrightDisclaimerAcceptedAt: acceptedAt,
            copyrightDisclaimerAcceptedBy: acceptedBy,
            isActiveForRag: false);

        document.CopyrightDisclaimerAcceptedAt.Should().Be(acceptedAt);
        document.CopyrightDisclaimerAcceptedBy.Should().Be(acceptedBy);
        document.HasAcceptedDisclaimer.Should().BeTrue();
        document.IsActiveForRag.Should().BeFalse();
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
            processedAt: null,
            pageCount: null,
            processingError: null,
            language: LanguageCode.English);

        document.HasAcceptedDisclaimer.Should().BeFalse();
        document.IsActiveForRag.Should().BeTrue();
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

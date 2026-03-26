using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Unit.KnowledgeBase;

/// <summary>
/// Unit tests for the PDF-KB link flow DTOs and data integrity.
/// Covers ExistingKbInfoDto, PdfUploadResult, and LinkKbResultDto
/// as produced by Tasks 1-7 of the PDF-KB detect-link-chat-select feature.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class PdfKbLinkFlowTests
{
    [Fact]
    public void ExistingKbInfoDto_UserSource_ShouldBeValid()
    {
        var dto = new ExistingKbInfoDto(
            PdfDocumentId: Guid.NewGuid(),
            Source: "user",
            FileName: "rules.pdf",
            ProcessingState: "Ready",
            TotalChunks: 50,
            OriginalGameName: "My Game",
            SharedGameId: null);

        dto.Source.Should().Be("user");
        dto.SharedGameId.Should().BeNull();
        dto.TotalChunks.Should().Be(50);
    }

    [Fact]
    public void ExistingKbInfoDto_SharedSource_ShouldHaveSharedGameId()
    {
        var sharedId = Guid.NewGuid();
        var dto = new ExistingKbInfoDto(
            PdfDocumentId: Guid.NewGuid(),
            Source: "shared",
            FileName: "shared-rules.pdf",
            ProcessingState: "Embedding",
            TotalChunks: null,
            OriginalGameName: "Community Game",
            SharedGameId: sharedId);

        dto.Source.Should().Be("shared");
        dto.SharedGameId.Should().Be(sharedId);
        dto.TotalChunks.Should().BeNull();
    }

    [Fact]
    public void PdfUploadResult_WithExistingKb_ShouldHaveKbData()
    {
        var kbInfo = new ExistingKbInfoDto(
            PdfDocumentId: Guid.NewGuid(),
            Source: "shared",
            FileName: "catan-rules.pdf",
            ProcessingState: "Ready",
            TotalChunks: 108,
            OriginalGameName: "Catan",
            SharedGameId: Guid.NewGuid());

        var result = new PdfUploadResult(true, "Existing KB found", null, kbInfo);

        result.Success.Should().BeTrue();
        result.ExistingKb.Should().NotBeNull();
        result.ExistingKb.Source.Should().Be("shared");
        result.ExistingKb.TotalChunks.Should().Be(108);
    }

    [Fact]
    public void PdfUploadResult_WithoutExistingKb_ShouldHaveNullKbInfo()
    {
        var result = new PdfUploadResult(true, "Upload OK", null);
        result.ExistingKb.Should().BeNull();
    }

    [Fact]
    public void LinkKbResultDto_Linked_ShouldHaveCorrectStatus()
    {
        var result = new LinkKbResultDto(
            VectorDocumentId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            PdfDocumentId: Guid.NewGuid(),
            Status: "linked");

        result.Status.Should().Be("linked");
        result.VectorDocumentId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void LinkKbResultDto_Pending_ShouldIndicateProcessing()
    {
        var result = new LinkKbResultDto(
            VectorDocumentId: Guid.Empty,
            GameId: Guid.NewGuid(),
            PdfDocumentId: Guid.NewGuid(),
            Status: "pending");

        result.Status.Should().Be("pending");
        result.VectorDocumentId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void ExistingKbInfoDto_RoundTrip_ShouldPreserveData()
    {
        var original = new ExistingKbInfoDto(
            PdfDocumentId: Guid.NewGuid(),
            Source: "shared",
            FileName: "test.pdf",
            ProcessingState: "Ready",
            TotalChunks: 42,
            OriginalGameName: "Test Game",
            SharedGameId: Guid.NewGuid());

        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<ExistingKbInfoDto>(json);

        deserialized.Should().NotBeNull();
        deserialized!.PdfDocumentId.Should().Be(original.PdfDocumentId);
        deserialized.Source.Should().Be(original.Source);
        deserialized.TotalChunks.Should().Be(original.TotalChunks);
    }
}

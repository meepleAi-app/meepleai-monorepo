using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Tests.Constants;
using Xunit;

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

        Assert.Equal("user", dto.Source);
        Assert.Null(dto.SharedGameId);
        Assert.Equal(50, dto.TotalChunks);
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

        Assert.Equal("shared", dto.Source);
        Assert.Equal(sharedId, dto.SharedGameId);
        Assert.Null(dto.TotalChunks);
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

        Assert.True(result.Success);
        Assert.NotNull(result.ExistingKb);
        Assert.Equal("shared", result.ExistingKb.Source);
        Assert.Equal(108, result.ExistingKb.TotalChunks);
    }

    [Fact]
    public void PdfUploadResult_WithoutExistingKb_ShouldHaveNullKbInfo()
    {
        var result = new PdfUploadResult(true, "Upload OK", null);
        Assert.Null(result.ExistingKb);
    }

    [Fact]
    public void LinkKbResultDto_Linked_ShouldHaveCorrectStatus()
    {
        var result = new LinkKbResultDto(
            VectorDocumentId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            PdfDocumentId: Guid.NewGuid(),
            Status: "linked");

        Assert.Equal("linked", result.Status);
        Assert.NotEqual(Guid.Empty, result.VectorDocumentId);
    }

    [Fact]
    public void LinkKbResultDto_Pending_ShouldIndicateProcessing()
    {
        var result = new LinkKbResultDto(
            VectorDocumentId: Guid.Empty,
            GameId: Guid.NewGuid(),
            PdfDocumentId: Guid.NewGuid(),
            Status: "pending");

        Assert.Equal("pending", result.Status);
        Assert.Equal(Guid.Empty, result.VectorDocumentId);
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

        Assert.NotNull(deserialized);
        Assert.Equal(original.PdfDocumentId, deserialized!.PdfDocumentId);
        Assert.Equal(original.Source, deserialized.Source);
        Assert.Equal(original.TotalChunks, deserialized.TotalChunks);
    }
}

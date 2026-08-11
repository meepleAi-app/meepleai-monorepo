using System;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public class PdfStorageKeyTests
{
    [Fact]
    public void ForPdf_UsesPdfIdNotGameId()
    {
        var pdfId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        PdfStorageKey.ForPdf(pdfId).Should().Be("11111111111111111111111111111111");
    }

    // -------------------------------------------------------------------
    // FileIdFromPath (Issue #2671): recover the RANDOM fileId StoreAsync
    // embeds in the persisted FilePath. Helper moved here from PdfSeeder.
    // -------------------------------------------------------------------

    [Theory]
    // Happy paths — S3 forward-slash + Windows backslash layouts
    [InlineData("pdf_uploads/gameId/abc123_rulebook.pdf", "abc123")]
    [InlineData("pdf_uploads\\gameId\\abc123_rulebook.pdf", "abc123")]
    [InlineData("C:\\storage\\pdf_uploads\\gameId\\fedcba_doc.pdf", "fedcba")]
    // Production S3 shape — hyphen-less GUID-N resourceKey dir + GUID-N fileId
    [InlineData("pdfs/abcdef01234567890123456789abcdef/00112233445566778899aabbccddeeff_file", "00112233445566778899aabbccddeeff")]
    // resourceKey dir WITH hyphens (canonical uuid) — fileId still after last '/', before first '_'
    [InlineData("pdfs/11111111-1111-1111-1111-111111111111/deadbeefcafe_file.pdf", "deadbeefcafe")]
    // Filename contains underscores — only the FIRST underscore in the last segment splits fileId/filename
    [InlineData("pdf_uploads/gameId/abc123_my_file_v2.pdf", "abc123")]
    public void FileIdFromPath_ValidPaths_ReturnsFileId(string filePath, string expectedFileId)
    {
        PdfStorageKey.FileIdFromPath(filePath).Should().Be(expectedFileId);
    }

    [Theory]
    // Null / empty
    [InlineData(null)]
    [InlineData("")]
    // No path separator → can't isolate last segment
    [InlineData("singleSegment_file.pdf")]
    [InlineData("noSeparatorNorUnderscore")]
    // Trailing separator → no filename after last separator
    [InlineData("pdfs/gameId/")]
    [InlineData("pdfs\\gameId\\")]
    // Last segment has no underscore → can't isolate fileId from filename
    [InlineData("pdfs/gameId/justAFile.pdf")]
    [InlineData("pdfs/gameId/no-underscore-here")]
    // Leading underscore in last segment → fileId would be empty
    [InlineData("pdfs/gameId/_leadingUnderscore.pdf")]
    public void FileIdFromPath_InvalidPaths_ReturnsNull(string? filePath)
    {
        PdfStorageKey.FileIdFromPath(filePath).Should().BeNull();
    }

    [Fact]
    public void FileIdFromPath_LegacyEmptyPath_CoalescesToForPdf()
    {
        // Caller pattern in the READ path: FileIdFromPath(FilePath) ?? ForPdf(Id).
        var pdfId = Guid.NewGuid();
        var fileId = PdfStorageKey.FileIdFromPath(string.Empty) ?? PdfStorageKey.ForPdf(pdfId);
        fileId.Should().Be(PdfStorageKey.ForPdf(pdfId));
    }
}

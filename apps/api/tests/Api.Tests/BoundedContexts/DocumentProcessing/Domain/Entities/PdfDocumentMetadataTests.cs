using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// Issue #1687 Task 2 — unit tests for the editable metadata mutators on
/// <see cref="PdfDocument"/>. Each mutator validates input, records the
/// audit columns (<c>UpdatedAt</c>, <c>UpdatedBy</c>), and (where the
/// existing aggregate already had a setter) reuses the existing path.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class PdfDocumentMetadataTests
{
    private static readonly Guid EditorId = Guid.Parse("c0ffee00-0000-4000-8000-000000000001");

    // ── SetTitle ──────────────────────────────────────────────────────────────

    [Fact]
    public void SetTitle_NullOrEmpty_ClearsTitle_UpdatesAudit()
    {
        var doc = CreateTestDocument();

        doc.SetTitle(null, EditorId);
        doc.Title.Should().BeNull("D-4 partial-update: explicit empty/null clears the value");
        doc.UpdatedAt.Should().NotBeNull("audit must record the touch even when clearing");
        doc.UpdatedBy.Should().Be(EditorId);

        doc.SetTitle("   ", EditorId);
        doc.Title.Should().BeNull("whitespace-only is treated as 'clear' to match the FluentValidation behavior");
    }

    [Fact]
    public void SetTitle_ValidString_TrimsAndStores_UpdatesAudit()
    {
        var doc = CreateTestDocument();

        doc.SetTitle("  Catan 5th Edition  ", EditorId);

        doc.Title.Should().Be("Catan 5th Edition", "leading/trailing whitespace must be trimmed");
        doc.UpdatedAt.Should().NotBeNull();
        doc.UpdatedBy.Should().Be(EditorId);
    }

    [Fact]
    public void SetTitle_Over200Chars_ThrowsArgumentException()
    {
        var doc = CreateTestDocument();
        var tooLong = new string('a', 201);

        Action act = () => doc.SetTitle(tooLong, EditorId);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*200*");
    }

    [Fact]
    public void SetTitle_EmptyEditorId_ThrowsArgumentException()
    {
        var doc = CreateTestDocument();

        Action act = () => doc.SetTitle("Some title", Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    // ── SetTags ───────────────────────────────────────────────────────────────

    [Fact]
    public void SetTags_EmptyEnumerable_ClearsTags_UpdatesAudit()
    {
        var doc = CreateTestDocument();
        doc.SetTags(new[] { "strategy" }, EditorId);

        doc.SetTags(Array.Empty<string>(), EditorId);

        doc.Tags.Should().BeEmpty("explicit empty array means clear all tags (D-4)");
        doc.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void SetTags_Mixed_DedupedLowercasedSortedStored()
    {
        var doc = CreateTestDocument();

        doc.SetTags(new[] { "Strategy", "family ", "strategy", " FAMILY", "Eurogame" }, EditorId);

        doc.Tags.Should().HaveCount(3, "Strategy/strategy and FAMILY/family must dedupe after normalization");
        doc.Tags.Should().BeInAscendingOrder(StringComparer.Ordinal,
            "tags must be sorted invariantly so Set-comparisons are deterministic (D-8)");
        doc.Tags.Should().BeEquivalentTo(new[] { "eurogame", "family", "strategy" });
    }

    [Fact]
    public void SetTags_MoreThan20_ThrowsArgumentException()
    {
        var doc = CreateTestDocument();
        var tooMany = Enumerable.Range(0, 21).Select(i => $"tag-{i}").ToArray();

        Action act = () => doc.SetTags(tooMany, EditorId);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*20*");
    }

    [Fact]
    public void SetTags_TagOver50Chars_ThrowsArgumentException()
    {
        var doc = CreateTestDocument();
        var tooLongTag = new string('a', 51);

        Action act = () => doc.SetTags(new[] { "ok", tooLongTag }, EditorId);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*50*");
    }

    [Fact]
    public void SetTags_WhitespaceOnlyTagsFiltered()
    {
        var doc = CreateTestDocument();

        doc.SetTags(new[] { "strategy", "  ", string.Empty, "family" }, EditorId);

        doc.Tags.Should().HaveCount(2);
        doc.Tags.Should().Contain("strategy");
        doc.Tags.Should().Contain("family");
    }

    // ── SetCategory ────────────────────────────────────────────────────────────

    [Fact]
    public void SetCategory_ValidEnum_UpdatesAudit_DoesNotChangeBaseDocumentIdOrVersionLabel()
    {
        var doc = CreateTestDocument();
        // Seed the legacy Reclassify state to detect that SetCategory does NOT mutate it.
        doc.Reclassify(DocumentCategory.Errata, baseDocumentId: Guid.NewGuid(), versionLabel: "v1.3");
        var seededBaseId = doc.BaseDocumentId;
        var seededVersionLabel = doc.VersionLabel;

        doc.SetCategory(DocumentCategory.QuickStart, EditorId);

        doc.DocumentCategory.Should().Be(DocumentCategory.QuickStart);
        doc.BaseDocumentId.Should().Be(seededBaseId,
            "SetCategory is SRP-narrow and must NOT clobber the base-document link (D-6 / Task 4 alt)");
        doc.VersionLabel.Should().Be(seededVersionLabel,
            "SetCategory must NOT clobber the version label either");
        doc.UpdatedAt.Should().NotBeNull();
        doc.UpdatedBy.Should().Be(EditorId);
    }

    // ── OverrideLanguageByEditor ────────────────────────────────────────────────

    [Fact]
    public void OverrideLanguageByEditor_ValidISO6391_SetsLanguageOverride_UpdatesAudit()
    {
        var doc = CreateTestDocument();

        doc.OverrideLanguageByEditor("it", EditorId);

        doc.LanguageOverride.Should().Be("it", "reuses the existing pipeline path (line 634 OverrideLanguage)");
        doc.UpdatedAt.Should().NotBeNull();
        doc.UpdatedBy.Should().Be(EditorId);
    }

    [Fact]
    public void OverrideLanguageByEditor_ClearsWhenWhitespace()
    {
        var doc = CreateTestDocument();
        doc.OverrideLanguageByEditor("de", EditorId);

        doc.OverrideLanguageByEditor("  ", EditorId);

        doc.LanguageOverride.Should().BeNull();
    }

    private static PdfDocument CreateTestDocument()
    {
        return new PdfDocument(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            fileName: new FileName("test.pdf"),
            filePath: "/uploads/test.pdf",
            fileSize: new FileSize(1024),
            uploadedByUserId: Guid.NewGuid());
    }
}

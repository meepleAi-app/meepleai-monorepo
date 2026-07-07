using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class GamebookGlossaryEntryTests
{
    private static readonly Guid CampaignId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid UserId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
    private static readonly Guid EditorId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void Create_WithAutoBootstrapSource_Succeeds()
    {
        var entry = GamebookGlossaryEntry.Create(CampaignId, "Goblin", "Goblin", GlossarySource.AutoBootstrap, UserId);

        entry.CampaignId.Should().Be(CampaignId);
        entry.TermEn.Should().Be("Goblin");
        entry.TermIt.Should().Be("Goblin");
        entry.Source.Should().Be(GlossarySource.AutoBootstrap);
        entry.CreatedBy.Should().Be(UserId);
        entry.UpdatedBy.Should().BeNull();
        entry.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void UpdateTermIt_FlipsSourceToManualAndStampsAudit()
    {
        var entry = GamebookGlossaryEntry.Create(CampaignId, "Sword", "Spada_auto", GlossarySource.AutoBootstrap, UserId);
        var beforeUpdate = DateTimeOffset.UtcNow;

        entry.UpdateTermIt("Spada", EditorId);

        entry.TermIt.Should().Be("Spada");
        entry.Source.Should().Be(GlossarySource.Manual);
        entry.UpdatedBy.Should().Be(EditorId);
        entry.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public void Create_WithEmptyTerms_Throws()
    {
        Action actEmptyEn = () => GamebookGlossaryEntry.Create(CampaignId, "", "Qualcosa", GlossarySource.Manual, UserId);
        Action actEmptyIt = () => GamebookGlossaryEntry.Create(CampaignId, "Something", "", GlossarySource.Manual, UserId);

        actEmptyEn.Should().Throw<ArgumentException>().WithParameterName("termEn");
        actEmptyIt.Should().Throw<ArgumentException>().WithParameterName("termIt");
    }

    [Fact]
    public void Create_WithFirstSeenBookId_SetsField()
    {
        var bookId = Guid.NewGuid();

        var entry = GamebookGlossaryEntry.Create(
            CampaignId, "Voidstone", "Pietra del Vuoto",
            GlossarySource.AutoBootstrap, UserId,
            firstSeenBookId: bookId);

        entry.FirstSeenBookId.Should().Be(bookId);
    }

    [Fact]
    public void Create_WithNullFirstSeenBookId_AllowsNull()
    {
        var entry = GamebookGlossaryEntry.Create(
            CampaignId, "Voidstone", "Pietra del Vuoto",
            GlossarySource.AutoBootstrap, UserId,
            firstSeenBookId: null);

        entry.FirstSeenBookId.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyFirstSeenBookId_Throws()
    {
        Action act = () => GamebookGlossaryEntry.Create(
            CampaignId, "Voidstone", "Pietra del Vuoto",
            GlossarySource.AutoBootstrap, UserId,
            firstSeenBookId: Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("firstSeenBookId");
    }

    // -------------------------------------------------------------------------
    // #2638 / SI-7 — multi-context support
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithFirstSeenBookId_SeedsSingleContext()
    {
        var bookId = Guid.NewGuid();

        var entry = GamebookGlossaryEntry.Create(
            CampaignId, "Voidstone", "Pietra del Vuoto",
            GlossarySource.AutoBootstrap, UserId,
            firstSeenBookId: bookId);

        entry.Contexts.Should().ContainSingle();
        entry.Contexts[0].BookId.Should().Be(bookId);
        entry.Contexts[0].ParagraphRef.Should().BeNull();
        entry.Contexts[0].Definition.Should().BeNull();
    }

    [Fact]
    public void Create_WithoutFirstSeenBookId_HasEmptyContexts()
    {
        var entry = GamebookGlossaryEntry.Create(
            CampaignId, "Goblin", "Goblin", GlossarySource.AutoBootstrap, UserId);

        entry.Contexts.Should().BeEmpty();
        entry.ContextsJson.Should().Be("[]");
    }

    [Fact]
    public void Create_WithExplicitContexts_PersistsAll()
    {
        var bookA = Guid.NewGuid();
        var bookB = Guid.NewGuid();
        var contexts = new[]
        {
            GlossaryContext.Create(bookA, "§147", null),
            GlossaryContext.Create(bookB, "§63", "punto di osservazione strategica"),
        };

        var entry = GamebookGlossaryEntry.Create(
            CampaignId, "sentinel", "soldato di guardia", GlossarySource.Manual, UserId,
            contexts: contexts);

        entry.Contexts.Should().HaveCount(2);
        entry.Contexts.Should().ContainSingle(c => c.BookId == bookA && c.ParagraphRef == "§147" && c.Definition == null);
        entry.Contexts.Should().ContainSingle(c =>
            c.BookId == bookB && c.ParagraphRef == "§63" && c.Definition == "punto di osservazione strategica");
    }

    [Fact]
    public void Create_WithExplicitContexts_TakesPrecedenceOverFirstSeenBookId()
    {
        var explicitBook = Guid.NewGuid();
        var firstSeen = Guid.NewGuid();

        var entry = GamebookGlossaryEntry.Create(
            CampaignId, "sentinel", "soldato di guardia", GlossarySource.Manual, UserId,
            firstSeenBookId: firstSeen,
            contexts: new[] { GlossaryContext.Create(explicitBook, null, null) });

        entry.Contexts.Should().ContainSingle();
        entry.Contexts[0].BookId.Should().Be(explicitBook);
        entry.FirstSeenBookId.Should().Be(firstSeen); // pointer retained
    }

    [Fact]
    public void AddContext_DedupsByBookAndParagraph()
    {
        var bookA = Guid.NewGuid();
        var entry = GamebookGlossaryEntry.Create(
            CampaignId, "sentinel", "soldato", GlossarySource.Manual, UserId,
            contexts: new[] { GlossaryContext.Create(bookA, "§147", null) });

        // Same (book, paragraph) with different casing on the ref → deduped, no-op.
        entry.AddContext(GlossaryContext.Create(bookA, "§147", "a redefinition"), EditorId);
        entry.Contexts.Should().ContainSingle();

        // Distinct paragraph on the same book → appended.
        entry.AddContext(GlossaryContext.Create(bookA, "§200", null), EditorId);
        entry.Contexts.Should().HaveCount(2);
        entry.UpdatedBy.Should().Be(EditorId);
    }

    [Fact]
    public void RemoveContext_RemovesMatchingKey()
    {
        var bookA = Guid.NewGuid();
        var bookB = Guid.NewGuid();
        var entry = GamebookGlossaryEntry.Create(
            CampaignId, "sentinel", "soldato", GlossarySource.Manual, UserId,
            contexts: new[]
            {
                GlossaryContext.Create(bookA, "§147", null),
                GlossaryContext.Create(bookB, "§63", null),
            });

        entry.RemoveContext(GlossaryContext.Create(bookA, "§147", null), EditorId);

        entry.Contexts.Should().ContainSingle(c => c.BookId == bookB);
        entry.UpdatedBy.Should().Be(EditorId);
    }

    [Fact]
    public void ReplaceContexts_ReplacesAllAndDedups()
    {
        var bookA = Guid.NewGuid();
        var bookB = Guid.NewGuid();
        var entry = GamebookGlossaryEntry.Create(
            CampaignId, "sentinel", "soldato", GlossarySource.Manual, UserId,
            contexts: new[] { GlossaryContext.Create(bookA, "§1", null) });

        // Replacement set includes a duplicate (bookB/§9 twice, one lower-cased ref).
        entry.ReplaceContexts(
            new[]
            {
                GlossaryContext.Create(bookB, "§9", null),
                GlossaryContext.Create(bookB, "§9", "dup should be dropped"),
                GlossaryContext.Create(bookA, "§5", null),
            },
            EditorId);

        entry.Contexts.Should().HaveCount(2);
        entry.Contexts.Should().NotContain(c => c.ParagraphRef == "§1"); // old set fully replaced
        entry.Contexts.Should().ContainSingle(c => c.BookId == bookB && c.ParagraphRef == "§9");
        entry.UpdatedBy.Should().Be(EditorId);
    }

    [Fact]
    public void Contexts_RoundTripThroughJson()
    {
        var bookA = Guid.NewGuid();
        var bookB = Guid.NewGuid();
        var entry = GamebookGlossaryEntry.Create(
            CampaignId, "sentinel", "soldato", GlossarySource.Manual, UserId,
            contexts: new[]
            {
                GlossaryContext.Create(bookA, "§147", "def uno"),
                GlossaryContext.Create(bookB, "§63", null),
            });

        // Non-ASCII paragraph markers must survive the JSONB payload unescaped.
        entry.ContextsJson.Should().Contain("§147");

        // Re-deserialize the raw JSON exactly as EF does on read.
        var roundTripped = System.Text.Json.JsonSerializer
            .Deserialize<List<GlossaryContext>>(entry.ContextsJson);

        roundTripped.Should().NotBeNull();
        roundTripped!.Should().HaveCount(2);
        roundTripped.Should().ContainSingle(c => c.BookId == bookA && c.ParagraphRef == "§147" && c.Definition == "def uno");
        roundTripped.Should().ContainSingle(c => c.BookId == bookB && c.ParagraphRef == "§63" && c.Definition == null);
    }
}

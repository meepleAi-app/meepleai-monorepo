using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
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
}

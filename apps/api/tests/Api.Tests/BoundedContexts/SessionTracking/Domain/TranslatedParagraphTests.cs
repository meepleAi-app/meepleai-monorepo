using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class TranslatedParagraphTests
{
    private static readonly Guid CampaignId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid GameBookId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid ArtifactId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid UserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public void Create_WithValidInputs_PopulatesAllFields()
    {
        var terms = new[] { "Goblin", "Nanolith" };

        var paragraph = TranslatedParagraph.Create(
            CampaignId, GameBookId, ArtifactId, 3, GamebookPageType.Storybook,
            "You see a goblin.", "Vedi un goblin.",
            terms, UserId);

        paragraph.CampaignId.Should().Be(CampaignId);
        paragraph.GameBookId.Should().Be(GameBookId);
        paragraph.PhotoArtifactId.Should().Be(ArtifactId);
        paragraph.ParagraphNumber.Should().Be(3);
        paragraph.PageType.Should().Be(GamebookPageType.Storybook);
        paragraph.SourceTextEn.Should().Be("You see a goblin.");
        paragraph.TranslatedTextIt.Should().Be("Vedi un goblin.");
        paragraph.AppliedGlossaryTerms.Should().BeEquivalentTo(terms);
        paragraph.CreatedBy.Should().Be(UserId);
        paragraph.Id.Should().NotBeEmpty();
        paragraph.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithEmptyTranslation_Throws()
    {
        Action act = () => TranslatedParagraph.Create(
            CampaignId, GameBookId, ArtifactId, 1, GamebookPageType.Encounter,
            "Source text", "",
            Array.Empty<string>(), UserId);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("translatedIt");
    }

    [Fact]
    public void Create_WithNoAppliedTerms_DefaultsToEmptyList()
    {
        var paragraph = TranslatedParagraph.Create(
            CampaignId, GameBookId, ArtifactId, 0, GamebookPageType.Storybook,
            "Some source text", "Del testo sorgente",
            Array.Empty<string>(), UserId);

        paragraph.AppliedGlossaryTerms.Should().NotBeNull();
        paragraph.AppliedGlossaryTerms.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithGameBookId_SetsField()
    {
        var bookId = Guid.NewGuid();

        var paragraph = TranslatedParagraph.Create(
            campaignId: Guid.NewGuid(),
            gameBookId: bookId,
            photoArtifactId: Guid.NewGuid(),
            paragraphNumber: 147,
            pageType: GamebookPageType.Storybook,
            sourceEn: "Source",
            translatedIt: "Tradotto",
            appliedTerms: new[] { "term1" },
            createdBy: Guid.NewGuid());

        paragraph.GameBookId.Should().Be(bookId);
    }

    [Fact]
    public void Create_WithEmptyGameBookId_ThrowsArgumentException()
    {
        Action act = () => TranslatedParagraph.Create(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), 1, GamebookPageType.Storybook,
            "src", "trad", Array.Empty<string>(), Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithParameterName("gameBookId");
    }
}

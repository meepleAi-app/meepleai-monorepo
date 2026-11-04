using Api.Infrastructure.Seeders.ReferenceQuestions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.ReferenceQuestions;

/// <summary>
/// #4470: il set non porta UUID, perché l'id di un gioco cambia da un ambiente all'altro (Catan è
/// <c>6b76bdb3…</c> nel manifest e <c>f24f9f2f…</c> nel database locale). Il seeder lo risolve per titolo
/// esatto, e deve dire quale gioco manca o è ambiguo invece di sceglierne uno.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4470")]
public sealed class ReferenceQuestionGameResolverTests
{
    private static readonly CatalogGame Catan = new(Guid.NewGuid(), "Catan");
    private static readonly CatalogGame Settlers = new(Guid.NewGuid(), "The Settlers of Catan");
    private static readonly CatalogGame TicketToRide = new(Guid.NewGuid(), "Ticket to Ride");
    private static readonly CatalogGame Europe = new(Guid.NewGuid(), "Ticket to Ride: Europe");

    [Fact]
    public void FindsEachGameByItsExactTitle()
    {
        var resolved = ReferenceQuestionGameResolver.Resolve(
            ["Catan", "Ticket to Ride", "Ticket to Ride: Europe"],
            [Catan, Settlers, TicketToRide, Europe]);

        resolved["Catan"].Should().Be(new GameResolution.Found(Catan.Id));
        resolved["Ticket to Ride"].Should().Be(new GameResolution.Found(TicketToRide.Id));
        resolved["Ticket to Ride: Europe"].Should().Be(new GameResolution.Found(Europe.Id));
    }

    [Fact]
    public void DoesNotTakeTheSettlersOfCatan_ADuplicateWithoutDocuments_ForCatan()
    {
        var resolved = ReferenceQuestionGameResolver.Resolve(["Catan"], [Settlers]);

        resolved["Catan"].Should().BeOfType<GameResolution.Missing>();
    }

    [Theory]
    [InlineData("catan")]
    [InlineData("Catan ")]
    [InlineData("CATAN")]
    public void MatchesTheTitleExactly_NotIgnoringCaseOrSpaces(string title)
    {
        var resolved = ReferenceQuestionGameResolver.Resolve([title], [Catan]);

        resolved[title].Should().BeOfType<GameResolution.Missing>();
    }

    [Fact]
    public void ReportsAGameThatIsNotInTheCatalog()
    {
        var resolved = ReferenceQuestionGameResolver.Resolve(["Gloomhaven", "Catan"], [Catan]);

        resolved["Gloomhaven"].Should().BeOfType<GameResolution.Missing>();
        resolved["Catan"].Should().Be(new GameResolution.Found(Catan.Id));
    }

    [Fact]
    public void ReportsTwoGamesWithTheSameTitleAsAmbiguous_AndNamesBoth()
    {
        var twin = new CatalogGame(Guid.NewGuid(), "Catan");

        var resolved = ReferenceQuestionGameResolver.Resolve(["Catan"], [Catan, twin]);

        resolved["Catan"].Should().BeOfType<GameResolution.Ambiguous>()
            .Which.Candidates.Should().BeEquivalentTo([Catan.Id, twin.Id]);
    }

    [Fact]
    public void AnswersEveryTitleAskedOnce_EvenWhenRepeated()
    {
        var resolved = ReferenceQuestionGameResolver.Resolve(["Catan", "Catan", "Gloomhaven"], [Catan]);

        resolved.Keys.Should().BeEquivalentTo(["Catan", "Gloomhaven"]);
    }
}

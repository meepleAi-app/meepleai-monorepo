using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Events;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public class CatalogSeedEventTests
{
    [Fact]
    public void FetchedEvent_SetsAllProperties()
    {
        var draftId = Guid.NewGuid();
        var evt = new CatalogSeedFetchedEvent(draftId, providerUsed: "wikidata+bgg", fetchedFields: 8);
        evt.DraftId.Should().Be(draftId);
        evt.ProviderUsed.Should().Be("wikidata+bgg");
        evt.FetchedFields.Should().Be(8);
    }

    [Fact]
    public void ApprovedEvent_CarriesResultingSharedGameId()
    {
        var draftId = Guid.NewGuid();
        var sgId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var evt = new CatalogSeedApprovedEvent(draftId, sgId, approverId);
        evt.DraftId.Should().Be(draftId);
        evt.ResultingSharedGameId.Should().Be(sgId);
        evt.ApprovedByUserId.Should().Be(approverId);
    }

    [Fact]
    public void RejectedEvent_CarriesReason()
    {
        var draftId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var evt = new CatalogSeedRejectedEvent(draftId, approverId, reason: "Duplicate of BGG:13");
        evt.DraftId.Should().Be(draftId);
        evt.RejectedByUserId.Should().Be(approverId);
        evt.Reason.Should().Be("Duplicate of BGG:13");
    }
}

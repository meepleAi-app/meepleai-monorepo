using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Compliance", "BggToS")]
public class BggImportFieldFilterTests
{
    [Fact]
    public void AllowedFields_ContainsOnlyFactualMetadata()
    {
        // Spec §7.2 — these MUST be the only allowed BGG fields. Any addition
        // requires legal review per spec §8.5.6 pre-rollout checklist.
        BggImportFieldFilter.AllowedFields.Should().BeEquivalentTo(new[]
        {
            "name", "yearpublished",
            "minplayers", "maxplayers",
            "playingtime", "minplaytime", "maxplaytime",
            "minage",
            "link[type=boardgamedesigner]",
            "link[type=boardgamepublisher]",
            "link[type=boardgameartist]",
            "link[type=boardgamemechanic]",
            "link[type=boardgamecategory]",
            "link[type=boardgamefamily]",
        });
    }

    [Fact]
    public void ForbiddenFields_RejectsCopyrightedContent()
    {
        // Spec §8.5.3 + §8.5.2: description/image/comments/statistics are
        // either copyrighted (Feist excluded) or DB sui generis (EU).
        BggImportFieldFilter.ForbiddenFields.Should().Contain(new[]
        {
            "description", "image", "thumbnail",
            "statistics", "comments", "videos",
        });
    }

    [Fact]
    public void AllowedAndForbidden_AreDisjoint()
    {
        BggImportFieldFilter.AllowedFields
            .Intersect(BggImportFieldFilter.ForbiddenFields, StringComparer.Ordinal)
            .Should().BeEmpty();
    }
}

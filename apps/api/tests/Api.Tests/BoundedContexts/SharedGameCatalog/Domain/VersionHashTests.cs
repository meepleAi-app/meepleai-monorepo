using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

public class VersionHashTests
{
    [Fact]
    public void Compute_is_deterministic_and_order_independent_for_tags()
    {
        var claims = new[]
        {
            (Guid.Parse("11111111-1111-1111-1111-111111111111"), "A player wins if…", 10),
            (Guid.Parse("22222222-2222-2222-2222-222222222222"), "Trading triggers…", 5),
        };
        var tagsAsc = new[] { "Auction", "Trading" };
        var tagsDesc = new[] { "Trading", "Auction" };

        var h1 = VersionHash.Compute(claims, tagsAsc);
        var h2 = VersionHash.Compute(claims, tagsDesc);

        h1.Value.Should().HaveLength(64);
        h1.Should().Be(h2);
    }

    [Fact]
    public void Compute_changes_when_statement_changes()
    {
        var a = VersionHash.Compute(new[] { (Guid.Empty, "x", 1) }, Array.Empty<string>());
        var b = VersionHash.Compute(new[] { (Guid.Empty, "y", 1) }, Array.Empty<string>());
        a.Should().NotBe(b);
    }

    [Fact]
    public void Compute_is_deterministic_and_order_independent_for_claims()
    {
        var id1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var id2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var claimsAsc = new[] { (id1, "s1", 1), (id2, "s2", 2) };
        var claimsDesc = new[] { (id2, "s2", 2), (id1, "s1", 1) };
        var tags = new[] { "t1" };

        var h1 = VersionHash.Compute(claimsAsc, tags).Value;
        var h2 = VersionHash.Compute(claimsDesc, tags).Value;

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void Compute_changes_when_expected_page_changes()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var a = VersionHash.Compute(new[] { (id, "statement", 5) }, Array.Empty<string>()).Value;
        var b = VersionHash.Compute(new[] { (id, "statement", 6) }, Array.Empty<string>()).Value;
        Assert.NotEqual(a, b);
    }
}

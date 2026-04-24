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
}

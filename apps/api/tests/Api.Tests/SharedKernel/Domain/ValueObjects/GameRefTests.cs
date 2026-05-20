using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedKernel.Domain.ValueObjects;

public class GameRefTests
{
    [Fact]
    public void Shared_factory_produces_Shared_kind()
    {
        var id = Guid.NewGuid();
        var r = GameRef.Shared(id);
        r.Kind.Should().Be(GameRefKind.Shared);
        r.Id.Should().Be(id);
    }

    [Fact]
    public void Private_factory_produces_Private_kind()
    {
        var id = Guid.NewGuid();
        var r = GameRef.Private(id);
        r.Kind.Should().Be(GameRefKind.Private);
        r.Id.Should().Be(id);
    }

    [Fact]
    public void Equality_is_value_based()
    {
        var id = Guid.NewGuid();
        var a = GameRef.Shared(id);
        var b = GameRef.Shared(id);
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Two_refs_with_different_kind_are_not_equal()
    {
        var id = Guid.NewGuid();
        GameRef.Shared(id).Should().NotBe(GameRef.Private(id));
    }

    [Fact]
    public void Empty_guid_throws()
    {
        var act = () => GameRef.Shared(Guid.Empty);
        act.Should().Throw<ArgumentException>().WithMessage("*empty*");
    }

    [Fact]
    public void ToString_includes_kind_and_id()
    {
        var id = Guid.Parse("12345678-1234-1234-1234-123456789012");
        var r = GameRef.Shared(id);
        r.ToString().Should().Be("Shared:12345678-1234-1234-1234-123456789012");
    }
}

using Api.Infrastructure.DomainEventLog;
using Api.Infrastructure.DomainEventOutbox;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Infrastructure.DomainEventOutbox;

/// <summary>
/// Issue #1535 T2 — resolver lookup contract. Asserts that registry aliases beat
/// CLR FullNames, that FullName resolution works for unregistered events, and
/// that unknown identifiers cleanly return null (processor maps to Failed).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class DomainEventTypeResolverTests
{
    private static readonly IDomainEventTypeResolver Resolver = new DomainEventTypeResolver();

    [Fact]
    public void Resolves_registered_event_by_registry_alias()
    {
        // Pick any alias currently in the registry — the assertion is "alias → registered CLR type".
        // EventTypeRegistry.AliasByType is the source of truth; we trust whatever is there today.
        EventTypeRegistry.AliasByType.Should().NotBeEmpty(
            "the resolver tests assume the registry is non-empty (#661 already registered events)");

        var (clrType, alias) = EventTypeRegistry.AliasByType.First();

        var resolved = Resolver.Resolve(alias);

        resolved.Should().Be(clrType);
    }

    [Fact]
    public void Resolves_unregistered_event_by_clr_fullname()
    {
        // PdfStateChangedEvent is intentionally NOT in the registry (decision B3 #1590).
        // It's a perfect candidate for FullName fallback.
        var pdfStateChangedType = typeof(IDomainEvent).Assembly
            .GetType("Api.BoundedContexts.DocumentProcessing.Domain.Events.PdfStateChangedEvent");

        pdfStateChangedType.Should().NotBeNull(
            "the test assumes PdfStateChangedEvent exists in the assembly");

        var resolved = Resolver.Resolve(pdfStateChangedType!.FullName!);

        resolved.Should().Be(pdfStateChangedType);
    }

    [Fact]
    public void Returns_null_for_unknown_alias()
    {
        Resolver.Resolve("never.registered.event").Should().BeNull();
    }

    [Fact]
    public void Returns_null_for_unknown_fullname()
    {
        Resolver.Resolve("Api.Some.Removed.Or.Renamed.Type").Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Returns_null_for_empty_or_whitespace(string input)
    {
        Resolver.Resolve(input).Should().BeNull();
    }
}

using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.MechanicExtractor.Guardrails;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class RuleStructureGuardrailTests
{
    private static readonly RuleStructureGuardrail Sut = new();

    private static MechanicSourceChunk Chunk(string text) => new(0, 4, null, text);

    [Fact]
    public void Family_And_Order()
    {
        Sut.RuleFamily.Should().Be("T5");
        Sut.Order.Should().Be(25);
        Sut.IsAdvisory.Should().BeTrue();
    }

    [Fact]
    public async Task ValidStructure_NoViolations()
    {
        const string json = """
        {"mechanics":[
          {"description":"regola","citations":[{"pdf_page":4,"quote":"q"}]},
          {"description":"eccezione","kind":"exception","priority":"card","overrides":[0],"trigger":{"phase":"fase azione"},"citations":[{"pdf_page":4,"quote":"q"}]}
        ]}
        """;
        var ctx = GuardrailTestContext.Ctx(json, new[] { Chunk("Durante la Fase Azione ogni giocatore...") });
        var v = await Sut.EvaluateAsync(ctx, CancellationToken.None);
        v.Should().BeEmpty();
    }

    [Fact]
    public async Task OverrideOutOfRangeOrSelf_ReportsMissing_WithItemPath()
    {
        const string json = """{"mechanics":[{"description":"a","kind":"exception","overrides":[0,5],"citations":[{"pdf_page":4,"quote":"q"}]}]}""";
        var v = await Sut.EvaluateAsync(GuardrailTestContext.Ctx(json), CancellationToken.None);
        v.Where(x => x.Rule == "T5_override_missing").Should().HaveCount(2).And.OnlyContain(x => x.Path == "$.mechanics[0]");
        v.Should().ContainSingle(x => x.Rule == "T5_exception_unbound" && x.Path == "$.mechanics[0]");
    }

    [Fact]
    public async Task Cycle_IsReportedOnce()
    {
        const string json = """
        {"mechanics":[
          {"description":"a","kind":"exception","overrides":[1],"citations":[{"pdf_page":4,"quote":"q"}]},
          {"description":"b","kind":"exception","overrides":[0],"citations":[{"pdf_page":4,"quote":"q"}]}
        ]}
        """;
        var v = await Sut.EvaluateAsync(GuardrailTestContext.Ctx(json), CancellationToken.None);
        v.Should().ContainSingle(x => x.Rule == "T5_override_cycle" && x.Path == "$.mechanics[1]");
    }

    [Fact]
    public async Task ExampleInOverrideGraph_IsReported()
    {
        const string json = """
        {"mechanics":[
          {"description":"es","kind":"example","citations":[{"pdf_page":4,"quote":"q"}]},
          {"description":"b","kind":"exception","overrides":[0],"citations":[{"pdf_page":4,"quote":"q"}]}
        ]}
        """;
        var v = await Sut.EvaluateAsync(GuardrailTestContext.Ctx(json), CancellationToken.None);
        v.Should().ContainSingle(x => x.Rule == "T5_example_in_override" && x.Path == "$.mechanics[1]");
    }

    [Fact]
    public async Task ExceptionWithoutOverridesOrTrigger_IsReported()
    {
        const string json = """{"mechanics":[{"description":"a","kind":"exception","citations":[{"pdf_page":4,"quote":"q"}]}]}""";
        var v = await Sut.EvaluateAsync(GuardrailTestContext.Ctx(json), CancellationToken.None);
        v.Should().ContainSingle(x => x.Rule == "T5_exception_unbound");
    }

    [Fact]
    public async Task AllOrdinalsInvalid_ReportsOverrideMissingAndUnbound()
    {
        // Unbound is computed on the RESOLVED edges, like the parser: no valid ordinal and no trigger ⇒ unbound too.
        const string json = """{"mechanics":[{"description":"a","kind":"exception","overrides":[7,9],"citations":[{"pdf_page":4,"quote":"q"}]}]}""";
        var v = await Sut.EvaluateAsync(GuardrailTestContext.Ctx(json), CancellationToken.None);
        v.Should().HaveCount(3);
        v.Where(x => x.Rule == "T5_override_missing").Should().HaveCount(2);
        v.Should().ContainSingle(x => x.Rule == "T5_exception_unbound" && x.Path == "$.mechanics[0]");
    }

    [Fact]
    public async Task ExceptionWithOnlyInvalidOrdinals_IsAlsoUnbound()
    {
        // Self (0), Example target (1) and out of range (9): none resolves, so the exception is unbound.
        const string json = """
        {"mechanics":[
          {"description":"a","kind":"exception","overrides":[0,1,9],"citations":[{"pdf_page":4,"quote":"q"}]},
          {"description":"es","kind":"example","citations":[{"pdf_page":4,"quote":"q"}]}
        ]}
        """;
        var v = await Sut.EvaluateAsync(GuardrailTestContext.Ctx(json), CancellationToken.None);
        v.Should().OnlyContain(x => x.Path == "$.mechanics[0]");
        v.Where(x => x.Rule == "T5_override_missing").Should().HaveCount(2);
        v.Should().ContainSingle(x => x.Rule == "T5_example_in_override");
        v.Should().ContainSingle(x => x.Rule == "T5_exception_unbound");
        v.Should().HaveCount(4);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"phase":"   ","action":"","component":null}""")]
    public async Task ExceptionWithEmptyTriggerObject_IsUnbound(string trigger)
    {
        // Same predicate as the parser (MechanicTrigger.Create): an empty or blank-only trigger is no trigger.
        var json = $$"""{"mechanics":[{"description":"a","kind":"exception","trigger":{{trigger}},"citations":[{"pdf_page":4,"quote":"q"}]}]}""";
        var v = await Sut.EvaluateAsync(GuardrailTestContext.Ctx(json), CancellationToken.None);
        v.Should().ContainSingle().Which.Should().Match<MechanicValidationViolation>(
            x => x.Rule == "T5_exception_unbound" && x.Path == "$.mechanics[0]");
    }

    [Fact]
    public async Task ExampleItemThatOverrides_IsReportedOnItself()
    {
        const string json = """
        {"mechanics":[
          {"description":"regola","citations":[{"pdf_page":4,"quote":"q"}]},
          {"description":"es","kind":"example","overrides":[0],"citations":[{"pdf_page":4,"quote":"q"}]}
        ]}
        """;
        var v = await Sut.EvaluateAsync(GuardrailTestContext.Ctx(json), CancellationToken.None);
        v.Should().ContainSingle().Which.Should().Match<MechanicValidationViolation>(
            x => x.Rule == "T5_example_in_override" && x.Path == "$.mechanics[1]");
    }

    [Fact]
    public async Task NonObjectItems_AreIgnoredNotThrown()
    {
        const string json = """{"mechanics":["foo",3,null,{"description":"ok","kind":"exception","overrides":[0],"citations":[{"pdf_page":4,"quote":"q"}]}]}""";
        var v = await Sut.EvaluateAsync(GuardrailTestContext.Ctx(json), CancellationToken.None);
        v.Should().ContainSingle(x => x.Rule == "T5_override_missing" && x.Path == "$.mechanics[3]");
    }

    [Fact]
    public async Task TriggerNameAbsentFromSources_IsReported()
    {
        const string json = """{"mechanics":[{"description":"a","trigger":{"phase":"fase lunare"},"citations":[{"pdf_page":4,"quote":"q"}]}]}""";
        var ctx = GuardrailTestContext.Ctx(json, new[] { Chunk("Durante la Fase Azione...") });
        var v = await Sut.EvaluateAsync(ctx, CancellationToken.None);
        v.Should().ContainSingle(x => x.Rule == "T5_trigger_unknown" && x.Message!.Contains("fase lunare"));
    }
}

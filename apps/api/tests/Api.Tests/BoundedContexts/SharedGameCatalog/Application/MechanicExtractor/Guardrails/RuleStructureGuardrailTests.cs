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
        v.Should().HaveCount(2).And.OnlyContain(x => x.Rule == "T5_override_missing" && x.Path == "$.mechanics[0]");
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
    public async Task AllOrdinalsInvalid_ReportsOnlyOverrideMissing()
    {
        const string json = """{"mechanics":[{"description":"a","kind":"exception","overrides":[7,9],"citations":[{"pdf_page":4,"quote":"q"}]}]}""";
        var v = await Sut.EvaluateAsync(GuardrailTestContext.Ctx(json), CancellationToken.None);
        v.Should().HaveCount(2).And.OnlyContain(x => x.Rule == "T5_override_missing");
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

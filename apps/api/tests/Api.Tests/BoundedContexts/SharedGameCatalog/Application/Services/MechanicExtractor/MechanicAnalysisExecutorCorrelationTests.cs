using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class MechanicAnalysisExecutorCorrelationTests
{
    [Fact]
    public void Correlate_FlagsOnlyTheOffendingClaim_SiblingsPass()
    {
        // Build two claims with distinct anchors via the parser (raw-index anchors).
        var json = """
        {"mechanics":[
          {"description":"clean mechanic","citations":[{"pdf_page":1,"quote":"clean"}]},
          {"description":"verbatim mechanic","citations":[{"pdf_page":2,"quote":"verbatim"}]}
        ]}
        """;
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json }).ToList();

        var outcomes = new Dictionary<MechanicSection, IReadOnlyList<MechanicRuleOutcome>>
        {
            [MechanicSection.Mechanics] = new[]
            {
                new MechanicRuleOutcome("T1", "pass", null, null, null, Array.Empty<MechanicValidationViolation>()),
                new MechanicRuleOutcome("T2", "fail", "long verbatim", "$.mechanics[1].description", null,
                    new[] { new MechanicValidationViolation("T2_long_verbatim", "long verbatim", "$.mechanics[1].description") }),
                new MechanicRuleOutcome("T3a", "pass", null, null, null, Array.Empty<MechanicValidationViolation>()),
                new MechanicRuleOutcome("T3b", "pass", null, null, 0.8, Array.Empty<MechanicValidationViolation>()),
                new MechanicRuleOutcome("T4", "pass", null, null, null, Array.Empty<MechanicValidationViolation>()),
            }
        };

        MechanicAnalysisExecutor.CorrelateValidations(claims, outcomes); // internal static, exposed for test

        claims[0].Validations.Single(v => v.Rule == "T2").Outcome.Should().Be("pass");
        claims[1].Validations.Single(v => v.Rule == "T2").Outcome.Should().Be("fail");
        claims[1].Validations.Should().HaveCount(5); // T1,T2,T3a,T3b,T4
        claims[1].Validations.Single(v => v.Rule == "T3b").Score.Should().Be(0.8);
    }

    [Fact]
    public void Correlate_VictorySection_FailPropagatesToAllSharedAnchorClaims()
    {
        // Victory primary + alternatives all share the anchor "$.victory" (MechanicOutputParser
        // ParseVictory), so per the D4 documented approximation a Victory guardrail failure
        // attributes to every Victory claim sharing that anchor — there is no per-alternative
        // sub-anchor to disambiguate against.
        var json = """
        {"victory":{
          "primary":"Score the most points",
          "alternatives":["Eliminate all opponents"],
          "citations":[{"pdf_page":3,"quote":"most points wins"}]
        }}
        """;
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Victory] = json }).ToList();

        claims.Should().HaveCount(2);
        claims.Should().OnlyContain(c => c.SourceAnchor == "$.victory");

        var outcomes = new Dictionary<MechanicSection, IReadOnlyList<MechanicRuleOutcome>>
        {
            [MechanicSection.Victory] = new[]
            {
                new MechanicRuleOutcome("T3a", "fail", "no grounding citation", "$.victory",
                    null, new[] { new MechanicValidationViolation("T3a_no_citation", "no grounding citation", "$.victory") }),
            }
        };

        MechanicAnalysisExecutor.CorrelateValidations(claims, outcomes);

        claims.Should().OnlyContain(c => c.Validations.Single(v => v.Rule == "T3a").Outcome == "fail");
    }
}

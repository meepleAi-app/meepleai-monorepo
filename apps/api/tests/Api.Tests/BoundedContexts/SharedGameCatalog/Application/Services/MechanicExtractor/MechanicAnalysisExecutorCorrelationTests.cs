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
                new MechanicRuleOutcome("T3b", "pass", null, null, 0.8, Array.Empty<MechanicValidationViolation>(),
                    new Dictionary<string, double> { ["$.mechanics[0]"] = 0.8, ["$.mechanics[1]"] = 0.8 }),
                new MechanicRuleOutcome("T4", "pass", null, null, null, Array.Empty<MechanicValidationViolation>()),
            }
        };

        MechanicAnalysisExecutor.CorrelateValidations(claims, outcomes); // internal static, exposed for test

        claims[0].Validations.Single(v => v.Rule == "T2").Outcome.Should().Be("pass");
        claims[0].Validations.Should().HaveCount(5); // T1,T2,T3a,T3b,T4
        claims[0].Validations.Single(v => v.Rule == "T1").Outcome.Should().Be("pass");
        claims[0].Validations.Single(v => v.Rule == "T3a").Outcome.Should().Be("pass");
        claims[0].Validations.Single(v => v.Rule == "T3b").Outcome.Should().Be("pass");
        claims[0].Validations.Single(v => v.Rule == "T4").Outcome.Should().Be("pass");
        claims[1].Validations.Single(v => v.Rule == "T2").Outcome.Should().Be("fail");
        claims[1].Validations.Should().HaveCount(5); // T1,T2,T3a,T3b,T4
        claims[1].Validations.Single(v => v.Rule == "T3b").Score.Should().Be(0.8);
    }

    [Fact]
    public void Correlate_NotRunOutcome_AppliesToAllClaimsInSection()
    {
        // notRun is not narrowed by anchor matching (only Fail is, per CorrelateValidations)
        // — a guardrail that was skipped (e.g. downstream of a fail-fast T3a) must pass through
        // as notRun to EVERY claim in the section, not just the one that triggered the abort.
        var json = """
        {"mechanics":[
          {"description":"clean mechanic","citations":[{"pdf_page":1,"quote":"clean"}]},
          {"description":"another mechanic","citations":[{"pdf_page":2,"quote":"another"}]}
        ]}
        """;
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json }).ToList();

        claims.Should().HaveCount(2);

        var outcomes = new Dictionary<MechanicSection, IReadOnlyList<MechanicRuleOutcome>>
        {
            [MechanicSection.Mechanics] = new[]
            {
                new MechanicRuleOutcome("T1", "pass", null, null, null, Array.Empty<MechanicValidationViolation>()),
                new MechanicRuleOutcome("T2", "fail", "long verbatim", "$.mechanics[0].description", null,
                    new[] { new MechanicValidationViolation("T2_long_verbatim", "long verbatim", "$.mechanics[0].description") }),
                new MechanicRuleOutcome("T3a", "notRun", null, null, null, Array.Empty<MechanicValidationViolation>()),
                new MechanicRuleOutcome("T3b", "notRun", null, null, null, Array.Empty<MechanicValidationViolation>()),
                new MechanicRuleOutcome("T4", "notRun", null, null, null, Array.Empty<MechanicValidationViolation>()),
            }
        };

        MechanicAnalysisExecutor.CorrelateValidations(claims, outcomes);

        claims.Should().OnlyContain(c => c.Validations.Single(v => v.Rule == "T3a").Outcome == "notRun");
        claims.Should().OnlyContain(c => c.Validations.Single(v => v.Rule == "T3b").Outcome == "notRun");
        claims.Should().OnlyContain(c => c.Validations.Single(v => v.Rule == "T4").Outcome == "notRun");
    }

    [Fact]
    public void Correlate_VictorySection_PrimaryFail_DoesNotTaintAlternatives()
    {
        // #2808: the primary anchors to "$.victory" and each alternative to
        // "$.victory.alternatives[i]", so a "$.victory" violation (about the primary)
        // matches the primary exactly but NOT the alternative sub-anchors — the D4
        // broadcast approximation is replaced with per-claim precision.
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
        claims[0].SourceAnchor.Should().Be("$.victory");
        claims[1].SourceAnchor.Should().Be("$.victory.alternatives[0]");

        var outcomes = new Dictionary<MechanicSection, IReadOnlyList<MechanicRuleOutcome>>
        {
            [MechanicSection.Victory] = new[]
            {
                new MechanicRuleOutcome("T3a", "fail", "no grounding citation", "$.victory",
                    null, new[] { new MechanicValidationViolation("T3a_no_citation", "no grounding citation", "$.victory") }),
            }
        };

        MechanicAnalysisExecutor.CorrelateValidations(claims, outcomes);

        claims[0].Validations.Single(v => v.Rule == "T3a").Outcome.Should().Be("fail"); // primary
        claims[1].Validations.Single(v => v.Rule == "T3a").Outcome.Should().Be("pass"); // alternative untainted
    }

    [Fact]
    public void Correlate_T3b_AttachesPerClaimCosine_NotSectionMin()
    {
        // #2811: a well-grounded claim must render ITS OWN cosine, not the section-wide min
        // captured from a different, poorly-grounded sibling.
        var json = """
        {"mechanics":[
          {"description":"well grounded","citations":[{"pdf_page":1,"quote":"q"}]},
          {"description":"poorly grounded","citations":[{"pdf_page":2,"quote":"q"}]}
        ]}
        """;
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json }).ToList();

        var claimScores = new Dictionary<string, double>
        {
            ["$.mechanics[0]"] = 0.90,
            ["$.mechanics[1]"] = 0.30,
        };
        var outcomes = new Dictionary<MechanicSection, IReadOnlyList<MechanicRuleOutcome>>
        {
            [MechanicSection.Mechanics] = new[]
            {
                new MechanicRuleOutcome("T3b", "fail", "below threshold", "$.mechanics[1]", 0.30,
                    new[] { new MechanicValidationViolation("T3_grounding", "below threshold", "$.mechanics[1]") },
                    claimScores),
            }
        };

        MechanicAnalysisExecutor.CorrelateValidations(claims, outcomes);

        var c0 = claims[0].Validations.Single(v => v.Rule == "T3b");
        c0.Outcome.Should().Be("pass");
        c0.Score.Should().Be(0.90); // its own — NOT the section-min 0.30

        var c1 = claims[1].Validations.Single(v => v.Rule == "T3b");
        c1.Outcome.Should().Be("fail");
        c1.Score.Should().Be(0.30);
    }

    [Fact]
    public void Correlate_T3b_NoPerClaimScore_YieldsNull_NotSectionMin()
    {
        // #2811: when a claim has no per-claim cosine (e.g. no citations graded), its T3b score
        // is null rather than the misleading section-min carried on the outcome.
        var json = """
        {"mechanics":[{"description":"ungraded","citations":[{"pdf_page":1,"quote":"q"}]}]}
        """;
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json }).ToList();

        var outcomes = new Dictionary<MechanicSection, IReadOnlyList<MechanicRuleOutcome>>
        {
            [MechanicSection.Mechanics] = new[]
            {
                // Score (section-min) present but ClaimScores has no entry for $.mechanics[0].
                new MechanicRuleOutcome("T3b", "pass", null, null, 0.42, Array.Empty<MechanicValidationViolation>(),
                    new Dictionary<string, double>()),
            }
        };

        MechanicAnalysisExecutor.CorrelateValidations(claims, outcomes);

        claims[0].Validations.Single(v => v.Rule == "T3b").Score.Should().BeNull();
    }
}

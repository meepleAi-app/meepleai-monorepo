using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class MechanicOutputValidatorRuleOutcomesTests
{
    private sealed class StubGuardrail : IMechanicGuardrail
    {
        private readonly IReadOnlyList<MechanicValidationViolation> _violations;
        private readonly double? _score;
        public StubGuardrail(string family, int order, IReadOnlyList<MechanicValidationViolation> violations, double? score = null, bool isAdvisory = false)
        { RuleFamily = family; Order = order; _violations = violations; _score = score; IsAdvisory = isAdvisory; }
        public string RuleFamily { get; }
        public int Order { get; }
        public bool IsAdvisory { get; }
        public Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(MechanicGuardrailContext c, CancellationToken ct) => Task.FromResult(_violations);
        public Task<MechanicGuardrailResult> EvaluateDetailedAsync(MechanicGuardrailContext c, CancellationToken ct) => Task.FromResult(new MechanicGuardrailResult(_violations, _score));
    }

    private static MechanicGuardrailContext EmptyContext()
    {
        using var doc = JsonDocument.Parse("{}");
        return new MechanicGuardrailContext(MechanicSection.Summary, doc.RootElement.Clone(), Array.Empty<MechanicSourceChunk>(), 1, new());
    }

    [Fact]
    public async Task ValidateAsync_FailFast_StillAccumulatesPassFailNotRunOutcomes()
    {
        var t1Pass = new StubGuardrail("T1", 10, Array.Empty<MechanicValidationViolation>());
        var t2Fail = new StubGuardrail("T2", 30, new[] { new MechanicValidationViolation("T2_long_verbatim", "long verbatim", "$.mechanics[1].description") });
        var t3bPass = new StubGuardrail("T3b", 40, Array.Empty<MechanicValidationViolation>(), score: 0.9);
        // Ordered by Order → T1(10), T2(30), T3b(40). Fail-fast stops after T2 → T3b is notRun.
        var validator = new MechanicOutputValidator(new IMechanicGuardrail[] { t3bPass, t2Fail, t1Pass }, NullLogger<MechanicOutputValidator>.Instance);

        var result = await validator.ValidateAsync(EmptyContext(), CancellationToken.None);

        result.IsValid.Should().BeFalse(); // retry trigger unchanged
        result.RuleOutcomes.Select(o => o.Rule).Should().Equal("T1", "T2", "T3b"); // Order-preserved
        result.RuleOutcomes.Single(o => o.Rule == "T1").Outcome.Should().Be("pass");
        result.RuleOutcomes.Single(o => o.Rule == "T2").Outcome.Should().Be("fail");
        result.RuleOutcomes.Single(o => o.Rule == "T2").Path.Should().Be("$.mechanics[1].description");
        result.RuleOutcomes.Single(o => o.Rule == "T3b").Outcome.Should().Be("notRun");
        result.RuleOutcomes.Single(o => o.Rule == "T3b").Score.Should().BeNull(); // notRun suppresses score even though the stub carries 0.9
        result.RuleOutcomes.Single(o => o.Rule == "T3b").Message.Should().BeNull();
        result.RuleOutcomes.Single(o => o.Rule == "T3b").Path.Should().BeNull();
    }

    [Fact]
    public async Task T5Fail_DoesNotTriggerRetry_AndDoesNotStopTheChain()
    {
        var t1Pass = new StubGuardrail("T1", 10, Array.Empty<MechanicValidationViolation>());
        var t5Fail = new StubGuardrail("T5", 25, new[] { new MechanicValidationViolation("T5_override_cycle", "cycle", "$.mechanics[1]") }, isAdvisory: true);
        var t3bPass = new StubGuardrail("T3b", 40, Array.Empty<MechanicValidationViolation>(), score: 0.9);
        var validator = new MechanicOutputValidator(new IMechanicGuardrail[] { t3bPass, t5Fail, t1Pass }, NullLogger<MechanicOutputValidator>.Instance);

        var result = await validator.ValidateAsync(EmptyContext(), CancellationToken.None);

        result.IsValid.Should().BeTrue(); // no retry
        result.Violations.Should().BeEmpty();
        result.RuleOutcomes.Select(o => o.Rule).Should().Equal("T1", "T5", "T3b");
        var t5 = result.RuleOutcomes.Single(o => o.Rule == "T5");
        t5.Outcome.Should().Be("fail"); // still visible to the reviewer
        t5.Violations.Should().ContainSingle().Which.Path.Should().Be("$.mechanics[1]");
        result.RuleOutcomes.Single(o => o.Rule == "T3b").Outcome.Should().Be("pass"); // chain not cut
    }

    [Fact]
    public async Task T1Fail_StillTriggersRetry_EvenWithAdvisoryT5Failing()
    {
        var t1Fail = new StubGuardrail("T1", 10, new[] { new MechanicValidationViolation("T1_quote_cap", "too long", "$.mechanics[0].citations[0].quote") });
        var t5Fail = new StubGuardrail("T5", 25, new[] { new MechanicValidationViolation("T5_override_cycle", "cycle", "$.mechanics[1]") }, isAdvisory: true);
        var validator = new MechanicOutputValidator(new IMechanicGuardrail[] { t5Fail, t1Fail }, NullLogger<MechanicOutputValidator>.Instance);

        var result = await validator.ValidateAsync(EmptyContext(), CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Violations.Should().ContainSingle(v => v.Rule == "T1_quote_cap");
    }

    [Fact]
    public async Task T5Fail_DoesNotMaskLaterBlockingFailure()
    {
        var t5Fail = new StubGuardrail("T5", 25, new[] { new MechanicValidationViolation("T5_override_cycle", "cycle", "$.mechanics[1]") }, isAdvisory: true);
        var t2Fail = new StubGuardrail("T2", 30, new[] { new MechanicValidationViolation("T2_long_verbatim", "verbatim", "$.mechanics[0].description") });
        var validator = new MechanicOutputValidator(new IMechanicGuardrail[] { t2Fail, t5Fail }, NullLogger<MechanicOutputValidator>.Instance);

        var result = await validator.ValidateAsync(EmptyContext(), CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Violations.Should().ContainSingle(v => v.Rule == "T2_long_verbatim");
        result.RuleOutcomes.Single(o => o.Rule == "T5").Outcome.Should().Be("fail");
    }

    [Fact]
    public async Task ValidateAsync_AllPass_CapturesScoresWithoutExtraWork()
    {
        var t1Pass = new StubGuardrail("T1", 10, Array.Empty<MechanicValidationViolation>());
        var t3bPass = new StubGuardrail("T3b", 40, Array.Empty<MechanicValidationViolation>(), score: 0.83);
        var validator = new MechanicOutputValidator(new IMechanicGuardrail[] { t3bPass, t1Pass }, NullLogger<MechanicOutputValidator>.Instance);

        var result = await validator.ValidateAsync(EmptyContext(), CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.RuleOutcomes.Should().OnlyContain(o => o.Outcome == "pass");
        result.RuleOutcomes.Single(o => o.Rule == "T3b").Score.Should().Be(0.83);
    }
}

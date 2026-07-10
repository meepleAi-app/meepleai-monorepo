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
        public StubGuardrail(string family, int order, IReadOnlyList<MechanicValidationViolation> violations, double? score = null)
        { RuleFamily = family; Order = order; _violations = violations; _score = score; }
        public string RuleFamily { get; }
        public int Order { get; }
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

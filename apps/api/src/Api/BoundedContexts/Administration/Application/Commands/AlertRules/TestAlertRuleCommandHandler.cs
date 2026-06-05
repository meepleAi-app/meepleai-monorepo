using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertRules;

/// <summary>
/// Handler for <see cref="TestAlertRuleCommand"/>.
///
/// <para>Loads the rule, synthesises a value that crosses the threshold
/// (threshold + 10% for ratios, threshold + 1 for raw counts), and publishes
/// <see cref="AlertFiredEvent"/> with the IsDryRun flag set per the mode
/// parameter. ChannelDispatchHandler then either dispatches or skips the
/// transport calls accordingly — both paths still log the event durably and
/// broadcast over SSE so the AlertActivityFeed renders the card.</para>
///
/// <para>The synthetic value formula intentionally biases above the threshold
/// to match user intuition for "the alert fires" — admins clicking Test want
/// to see what a real firing looks like, not a no-op event.</para>
/// </summary>
internal sealed class TestAlertRuleCommandHandler : IRequestHandler<TestAlertRuleCommand, TestAlertRuleResult>
{
    private const string LiveMode = "live";

    private readonly IAlertRuleRepository _ruleRepository;
    private readonly IPublisher _publisher;

    public TestAlertRuleCommandHandler(IAlertRuleRepository ruleRepository, IPublisher publisher)
    {
        _ruleRepository = ruleRepository ?? throw new ArgumentNullException(nameof(ruleRepository));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public async Task<TestAlertRuleResult> Handle(TestAlertRuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rule = await _ruleRepository.GetByIdAsync(request.RuleId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("AlertRule", request.RuleId.ToString());

        var isDryRun = !string.Equals(request.Mode, LiveMode, StringComparison.OrdinalIgnoreCase);

        // Channel list comes from rule metadata in a follow-up. For #1840 the
        // rule schema doesn't yet model channels — we pass the canonical
        // "slack + email" pair so the dispatch handler exercises every
        // configured channel. UI will allow per-rule channel selection in
        // Phase 2 FE.
        var channels = new[] { "slack", "email" };

        var firedEvent = new AlertFiredEvent(
            RuleId: rule.Id,
            RuleName: rule.Name,
            AlertType: rule.AlertType,
            Metric: rule.AlertType, // proxy until rules carry a dedicated Metric field
            Value: SynthesizeFiringValue(rule.Threshold.Value),
            Threshold: rule.Threshold.Value,
            ThresholdUnit: rule.Threshold.Unit,
            Severity: rule.Severity.ToKind(),
            Channels: channels,
            IsDryRun: isDryRun,
            IsTest: true,
            TriggeredBy: request.TriggeredBy);

        await _publisher.Publish(firedEvent, cancellationToken).ConfigureAwait(false);

        return new TestAlertRuleResult(
            RuleId: rule.Id,
            RuleName: rule.Name,
            IsDryRun: isDryRun,
            Channels: channels,
            FiredAt: firedEvent.OccurredAt);
    }

    /// <summary>
    /// Pick a synthetic metric value that DOES cross the threshold. For
    /// "0 means firing" rules (rare in practice but valid) we bump by +1
    /// to avoid returning exactly 0 which could be ambiguous to log readers.
    /// </summary>
    private static double SynthesizeFiringValue(double threshold)
    {
        // Floating-point safe comparison for the "0 threshold" branch:
        // we treat any threshold within an ulp of zero as the degenerate
        // case and return 1d. Otherwise bias 10% above the threshold so
        // the synthetic value clearly fires the rule.
        if (Math.Abs(threshold) < double.Epsilon) return 1d;
        var delta = Math.Max(0.1d, threshold * 0.1d);
        return threshold + delta;
    }
}

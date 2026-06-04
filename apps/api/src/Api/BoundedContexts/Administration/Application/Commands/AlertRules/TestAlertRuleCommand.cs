using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertRules;

/// <summary>
/// Probes an existing <c>AlertRule</c> by raising a synthetic
/// <see cref="Api.BoundedContexts.Administration.Domain.Events.AlertFiredEvent"/>
/// (Issue #1840 SP5 F4-C7).
///
/// <para><see cref="Mode"/>:
/// <list type="bullet">
///   <item><c>"dryRun"</c> (default): the event is raised but
///         <c>ChannelDispatchHandler</c> SKIPS the transport call. Activity feed
///         still renders a "TEST · DRY RUN" card via SSE.</item>
///   <item><c>"live"</c>: real channel dispatch happens. UI should require a
///         confirmation step before sending this mode.</item>
/// </list>
/// </para>
/// </summary>
internal sealed record TestAlertRuleCommand(Guid RuleId, string? Mode, string TriggeredBy)
    : IRequest<TestAlertRuleResult>;

/// <summary>Response surfaced to the admin UI / test-alert button.</summary>
internal sealed record TestAlertRuleResult(
    Guid RuleId,
    string RuleName,
    bool IsDryRun,
    IReadOnlyList<string> Channels,
    DateTime FiredAt);

internal sealed class TestAlertRuleCommandValidator : AbstractValidator<TestAlertRuleCommand>
{
    private static readonly string[] AllowedModes = { "dryRun", "live" };

    public TestAlertRuleCommandValidator()
    {
        RuleFor(x => x.RuleId).NotEqual(Guid.Empty);

        RuleFor(x => x.Mode)
            .Must(m => string.IsNullOrEmpty(m) || AllowedModes.Contains(m, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Mode must be one of: dryRun, live (default: dryRun)");

        RuleFor(x => x.TriggeredBy).NotEmpty();
    }
}

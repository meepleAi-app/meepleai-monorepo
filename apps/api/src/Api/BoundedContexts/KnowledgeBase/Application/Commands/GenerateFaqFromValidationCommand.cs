using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to auto-generate FAQ entry from validated conflict resolution.
/// Issue #4328: Arbitro Agent Beta Testing - FAQ Auto-Expansion.
/// </summary>
/// <remarks>
/// Triggered when:
/// - Conflict was resolved by AI (not FAQ fast-path)
/// - User feedback confirms AI decision was correct (Accuracy = Correct)
/// - AI confidence was high (>0.85)
/// - Multiple similar conflicts detected (pattern threshold)
/// </remarks>
public record GenerateFaqFromValidationCommand : IRequest<Guid?>
{
    /// <summary>
    /// Validation ID that triggered FAQ generation.
    /// </summary>
    public required Guid ValidationId { get; init; }

    /// <summary>
    /// Whether to auto-approve (true) or require admin review (false).
    /// Default: false (admin review required for safety).
    /// </summary>
    public bool AutoApprove { get; init; }
}

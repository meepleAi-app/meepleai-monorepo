using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Promotes an existing DRAFT game-night session (a <c>GameNightSession</c> link in
/// <c>Pending</c> status) to live (<c>InProgress</c>), as the explicit go-live sub-resource
/// <c>POST /api/v1/sessions/{id}/go-live</c> (epic #3188 Slice 2, decisions D2 + D3).
///
/// <para>The draft already exists (created by an earlier step), so — unlike
/// <c>StartGameNightSessionCommand</c> — this does NOT create a tracking Session. It resolves the
/// owning <c>GameNightEvent</c> by <paramref name="SessionId"/>, promotes that specific draft via
/// the aggregate (enforcing invariante #10 max-1-live), and opens live mode LAST so the invariante
/// #15 night promotion fires unambiguously.</para>
/// </summary>
/// <param name="SessionId">The tracking-Session id of the draft to take live.</param>
/// <param name="UserId">The authenticated caller; must be the night's organizer (authorization).</param>
internal record GoLiveSessionCommand(Guid SessionId, Guid UserId) : ICommand<GoLiveSessionResult>;

/// <summary>
/// Result of a successful go-live promotion.
/// </summary>
/// <param name="SessionId">The tracking-Session id that was promoted.</param>
/// <param name="GameNightId">The owning GameNightEvent id.</param>
/// <param name="GameNightSessionId">The promoted <c>GameNightSession</c> link id.</param>
/// <param name="PlayOrder">The promoted session's play order within the night (1..5).</param>
/// <param name="Status">The promoted session's link status (InProgress on success).</param>
internal record GoLiveSessionResult(
    Guid SessionId,
    Guid GameNightId,
    Guid GameNightSessionId,
    int PlayOrder,
    string Status);

internal sealed class GoLiveSessionCommandValidator : AbstractValidator<GoLiveSessionCommand>
{
    public GoLiveSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

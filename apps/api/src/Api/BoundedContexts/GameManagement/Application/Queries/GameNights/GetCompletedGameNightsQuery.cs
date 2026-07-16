using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Query to get the most recently completed game nights for the dashboard
/// "Recenti" slot. F20 #1974 (audit 2026-06-07) — Asse C P2 follow-up.
/// </summary>
/// <param name="Limit">
/// Maximum rows to return (DESC by CompletedAt). Defaults to 5 — matches
/// the dashboard slot's visible card budget without forcing the FE to
/// page; trimmed further on the FE if needed.
/// </param>
/// <param name="CallerUserId">
/// #2978 (invariante #17): the authenticated viewer, used to resolve their own RSVP status
/// (<c>MyRsvpStatus</c>) on each returned DTO.
/// </param>
internal record GetCompletedGameNightsQuery(Guid CallerUserId, int Limit = 5) : IQuery<IReadOnlyList<GameNightDto>>;

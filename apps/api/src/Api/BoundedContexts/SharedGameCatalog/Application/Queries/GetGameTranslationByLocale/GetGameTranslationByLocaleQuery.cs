using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameTranslationByLocale;

/// <summary>
/// Admin query returning a single active translation by game + locale.
/// Issue #2339 — sub-PR 1/3 Wave 4 (Task 12).
/// </summary>
/// <param name="GameId">Owning <c>shared_games.id</c>.</param>
/// <param name="Locale">ISO 639-1 locale.</param>
/// <remarks>
/// DEC-M2 (2026-06-15 plan review): the spec §6.5 originally declared this
/// query as <c>IRequest&lt;SharedGameTranslationDetailDto&gt;</c> (non-nullable).
/// We instead return <c>null</c> when no active row matches so the endpoint
/// can map it to HTTP 404 without throwing.
/// </remarks>
public sealed record GetGameTranslationByLocaleQuery(Guid GameId, string Locale)
    : IRequest<SharedGameTranslationDetailDto?>;

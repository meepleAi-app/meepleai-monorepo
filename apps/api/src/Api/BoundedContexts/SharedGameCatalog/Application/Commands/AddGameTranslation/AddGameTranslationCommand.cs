using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddGameTranslation;

/// <summary>
/// Admin command to add a non-EN translation row to
/// <c>shared_game_translations</c>. Issue #2339 — sub-PR 1/3 Wave 3 (Task 9).
/// </summary>
/// <param name="GameId">Owning <c>shared_games.id</c> — must exist (checked by handler).</param>
/// <param name="Locale">ISO 639-1 (e.g. <c>"it"</c>) — must not be <c>"en"</c>.</param>
/// <param name="Title">Localized title, 1..500 chars, trimmed.</param>
/// <param name="Description">Optional localized description, trimmed when present.</param>
/// <param name="Source">Kebab-case persisted source: <c>manual</c> | <c>auto-openrouter</c> | <c>community</c>.</param>
/// <param name="ActorUserId">Identity of the admin issuing the command (audit).</param>
public sealed record AddGameTranslationCommand(
    Guid GameId,
    string Locale,
    string Title,
    string? Description,
    string Source,
    Guid ActorUserId) : IRequest<Guid>;

using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.UpdateGameTranslation;

/// <summary>
/// Admin command to update an existing non-EN translation in
/// <c>shared_game_translations</c>. Issue #2339 — sub-PR 1/3 Wave 3 (Task 10).
/// </summary>
/// <param name="GameId">Owning <c>shared_games.id</c>.</param>
/// <param name="Locale">ISO 639-1 locale identifying which translation to update.</param>
/// <param name="Title">Replacement title, 1..500 chars, trimmed.</param>
/// <param name="Description">Replacement description, trimmed when present (nullable to clear).</param>
/// <param name="Xmin">Postgres <c>xmin</c> system column captured by the client on read,
/// passed back here for optimistic concurrency check (DEC-C4 from plan review).
/// EF raises <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
/// when the value diverges from the row's current <c>xmin</c>.</param>
/// <param name="ActorUserId">Identity of the admin issuing the command (audit).</param>
public sealed record UpdateGameTranslationCommand(
    Guid GameId,
    string Locale,
    string Title,
    string? Description,
    uint Xmin,
    Guid ActorUserId) : IRequest<Unit>;

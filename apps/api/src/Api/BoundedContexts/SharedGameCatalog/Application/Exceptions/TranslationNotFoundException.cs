using System.Diagnostics.CodeAnalysis;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Exceptions;

/// <summary>
/// Thrown when a shared_game_translations row for the given game + locale cannot be
/// located. Maps to HTTP 404 via the global exception middleware.
/// Issue #2339 — sub-PR 1/3.
/// </summary>
public sealed class TranslationNotFoundException : NotFoundException
{
    public Guid GameId { get; }
    public string Locale { get; }

    [SetsRequiredMembers]
    public TranslationNotFoundException(Guid gameId, string locale)
        : base("SharedGameTranslation", $"game={gameId}, locale='{locale}'")
    {
        GameId = gameId;
        Locale = locale;
    }
}

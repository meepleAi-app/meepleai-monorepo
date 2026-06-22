using System.Diagnostics.CodeAnalysis;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Exceptions;

/// <summary>
/// Thrown when attempting to create a shared_game_translations row that violates
/// the partial unique index (active translation per game + locale). Maps to HTTP 409.
/// Issue #2339 — sub-PR 1/3.
/// </summary>
public sealed class TranslationAlreadyExistsException : ConflictException
{
    public Guid GameId { get; }
    public string Locale { get; }

    [SetsRequiredMembers]
    public TranslationAlreadyExistsException(Guid gameId, string locale)
        : base($"Translation for game {gameId} locale '{locale}' already exists")
    {
        GameId = gameId;
        Locale = locale;
    }
}

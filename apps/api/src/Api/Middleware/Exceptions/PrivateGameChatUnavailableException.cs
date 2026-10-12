using System.Diagnostics.CodeAnalysis;

namespace Api.Middleware.Exceptions;

/// <summary>
/// A chat thread was requested on the caller's own private game, and it cannot be stored yet.
/// </summary>
/// <remarks>
/// Issue #4230. <c>ChatThreads.GameId</c> references <c>shared_games</c>, so a thread on a private
/// game broke the foreign key at save time and answered 500. Until the thread gets a column for
/// private games (#4244), the request is refused with a status and a code the client can read.
/// 422 rather than 404: the game exists and belongs to the caller; what is missing is the feature.
/// This type leaves with #4244.
/// </remarks>
public sealed class PrivateGameChatUnavailableException : HttpException
{
    /// <summary>
    /// The code on the wire. Upper case, like the other codes the chat reads
    /// (<see cref="RagOwnershipRequiredException.Code"/>).
    /// </summary>
    public const string Code = "PRIVATE_GAME_CHAT_UNAVAILABLE";

    [SetsRequiredMembers]
    public PrivateGameChatUnavailableException(Guid privateGameId)
        : base(
            StatusCodes.Status422UnprocessableEntity,
            Code,
            "La chat sui giochi privati non è ancora disponibile.")
    {
        PrivateGameId = privateGameId;
    }

    public Guid PrivateGameId { get; }
}

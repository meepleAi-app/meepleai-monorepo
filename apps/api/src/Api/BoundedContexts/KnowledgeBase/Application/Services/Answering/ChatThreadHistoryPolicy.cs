using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>Why the history of a chat thread may not enter the prompt of a question.</summary>
internal enum ChatThreadHistoryRefusal
{
    /// <summary>The thread belongs to someone else, or the question carries no identity.</summary>
    NotTheOwner,

    /// <summary>The thread is about a different game than the question.</summary>
    AnotherGame,
}

/// <summary>
/// Decides whether the history of the thread named by a request may be used to answer it.
/// </summary>
/// <remarks>
/// Issue #4242. A question may name a thread (<c>chatId</c>), and the answer paths put that
/// thread's history into the prompt. They checked that the thread was about the same game, and
/// nothing else: a caller who sent someone else's thread id got an answer built on that person's
/// conversation. The rule lives here so that the paths that take a thread from the request cannot
/// drift apart: the streaming path and the single-question path, which answer without the history,
/// and the session agent, which also writes to the thread and therefore refuses it.
/// </remarks>
internal static class ChatThreadHistoryPolicy
{
    /// <summary>
    /// Returns null when the history may be used, otherwise the reason it may not.
    /// Ownership is decided first, so the reason never tells a stranger which game a thread is about.
    /// </summary>
    /// <param name="thread">The thread named by the request.</param>
    /// <param name="askingUserId">Who is asking. No identity, no history.</param>
    /// <param name="questionGameId">The game of the question, or null when it is not a usable id.</param>
    public static ChatThreadHistoryRefusal? WhyNot(ChatThread thread, Guid? askingUserId, Guid? questionGameId)
    {
        ArgumentNullException.ThrowIfNull(thread);

        if (askingUserId is null || thread.UserId != askingUserId.Value)
        {
            return ChatThreadHistoryRefusal.NotTheOwner;
        }

        // A thread with no game is a general conversation: it may accompany any question of its owner.
        if (thread.GameId.HasValue && thread.GameId != questionGameId)
        {
            return ChatThreadHistoryRefusal.AnotherGame;
        }

        return null;
    }
}

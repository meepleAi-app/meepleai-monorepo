using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Streaming query for RAG-based Q&amp;A with token-by-token delivery.
/// Returns progressive events: StateUpdate → Citations → Token(s) → Complete
/// CHAT-01: Supports chat context integration via ThreadId
/// Issue #2051: Supports document filtering via DocumentIds
/// </summary>
/// <param name="GameId">The game ID to ask question about</param>
/// <param name="Query">The user's question</param>
/// <param name="ThreadId">Optional chat thread ID for context</param>
/// <param name="DocumentIds">Optional document IDs to filter sources (null = all documents)</param>
/// <param name="ResponseStyle">Response style: "concise" (default), "detailed", or "continuation"</param>
/// <param name="ContinuationContext">Partial answer text from a previous truncated response</param>
/// <param name="UserId">Authenticated user id — per-game RAG access is enforced against it</param>
/// <param name="UserRole">Authenticated user role (string) — parsed for RAG access (admin bypass)</param>
/// <remarks>
/// Issue #4137: <paramref name="UserId"/> and <paramref name="UserRole"/> are REQUIRED.
/// They were optional (Bug B5 added them with defaults), and the handler's access check
/// was therefore conditional on their presence — so an endpoint that simply did not pass
/// them skipped authorization entirely and still compiled. That is what
/// <c>RagDashboardEndpoints</c> did: it built this query with gameId + query only. It was
/// harmless because that route is admin-gated and rule 1 grants admins anyway, which is
/// exactly the problem — the guard held by coincidence, not by construction. Required
/// parameters make "an endpoint that does not invoke the guard" impossible to express,
/// which no test can achieve on its own.
/// </remarks>
internal record StreamQaQuery(
    string GameId,
    string Query,
    Guid UserId,
    string UserRole,
    Guid? ThreadId = null,
    IReadOnlyList<Guid>? DocumentIds = null,
    string? ResponseStyle = null,
    string? ContinuationContext = null
) : IStreamingQuery<RagStreamingEvent>;

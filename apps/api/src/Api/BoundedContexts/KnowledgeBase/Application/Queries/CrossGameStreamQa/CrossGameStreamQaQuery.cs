using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.CrossGameStreamQa;

/// <summary>
/// Streaming query for cross-game RAG Q&amp;A (SSE ask).
/// Retrieves chunks from all RBAC-accessible games for the requesting user,
/// assembles a cross-game prompt, and streams LLM tokens as RagStreamingEvents.
/// Issue #1661 PR-2 Task 8b.
/// </summary>
/// <param name="Query">The user's natural-language question.</param>
/// <param name="UserId">Authenticated user — drives RBAC game-id resolution.</param>
/// <param name="Role">User role — Admin/SuperAdmin get all games, others get public + owned.</param>
/// <param name="AgentLanguage">ISO 639-1 language code for the prompt (default: "it").</param>
/// <param name="TopK">Maximum chunks to retrieve across all accessible games (default: 8).</param>
internal record CrossGameStreamQaQuery(
    string Query,
    Guid UserId,
    UserRole Role,
    string AgentLanguage = "it",
    int TopK = 8
) : IStreamingQuery<RagStreamingEvent>;

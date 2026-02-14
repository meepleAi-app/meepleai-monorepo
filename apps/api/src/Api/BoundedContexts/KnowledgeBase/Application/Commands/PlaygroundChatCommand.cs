using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Streaming command for playground chat with an AgentDefinition.
/// Returns SSE events: StateUpdate → Citations → Token(s) → FollowUpQuestions → Complete
/// Issue #4392: Replace placeholder endpoint with real AgentDefinition integration.
/// </summary>
internal record PlaygroundChatCommand(
    Guid AgentDefinitionId,
    string Message
) : IStreamingQuery<RagStreamingEvent>;

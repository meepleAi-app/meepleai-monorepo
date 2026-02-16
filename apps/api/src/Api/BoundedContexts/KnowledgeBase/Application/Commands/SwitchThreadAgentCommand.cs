using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to switch the agent type on an active chat thread (Issue #4465).
/// </summary>
internal record SwitchThreadAgentCommand(
    Guid ThreadId,
    string AgentType
) : ICommand<ChatThreadDto>;

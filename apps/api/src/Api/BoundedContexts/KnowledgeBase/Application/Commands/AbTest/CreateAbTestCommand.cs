using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AbTest;

/// <summary>
/// Command to create a new A/B test session with parallel model responses.
/// Issue #5494: A/B Test CQRS commands and queries.
/// </summary>
internal sealed record CreateAbTestCommand(
    Guid CreatedBy,
    string Query,
    List<string> ModelIds,
    Guid? KnowledgeBaseId = null
) : ICommand<AbTestSessionDto>;

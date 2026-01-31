using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command for Editor to test a Draft agent typology in sandbox.
/// Issue #3177: AGT-003 Editor Proposal Commands.
/// </summary>
internal record TestAgentTypologyCommand(
    Guid TypologyId,
    string TestQuery,
    Guid RequestedBy
) : IRequest<TestAgentTypologyResult>;

/// <summary>
/// Result of testing an agent typology in sandbox.
/// </summary>
internal record TestAgentTypologyResult(
    bool Success,
    string Response,
    double? ConfidenceScore,
    string? ErrorMessage = null
);

using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to export a chat thread in a specified format.
/// </summary>
public record ExportChatCommand(
    Guid ThreadId,
    string Format = "json" // Default to JSON
) : ICommand<ExportedChatDto>;

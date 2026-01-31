using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for ExportChatCommand.
/// Exports a chat thread in the specified format (JSON or Markdown).
/// </summary>
internal class ExportChatCommandHandler : ICommandHandler<ExportChatCommand, ExportedChatDto>
{
    private readonly IChatThreadRepository _threadRepository;

    public ExportChatCommandHandler(IChatThreadRepository threadRepository)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
    }

    public async Task<ExportedChatDto> Handle(ExportChatCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Retrieve thread
        var thread = await _threadRepository.GetByIdAsync(command.ThreadId, cancellationToken).ConfigureAwait(false);
        if (thread == null)
            throw new InvalidOperationException($"Thread with ID {command.ThreadId} not found");

        // Parse format
        var format = ParseFormat(command.Format);

        // Export using domain logic
        var exportedData = thread.Export(format);

        // Map to DTO
        return new ExportedChatDto(
            Format: exportedData.Format.ToString(),
            Content: exportedData.Content,
            ContentType: exportedData.ContentType,
            FileExtension: exportedData.FileExtension,
            ExportedAt: exportedData.ExportedAt
        );
    }

    private static ExportFormat ParseFormat(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "json" => ExportFormat.Json,
            "markdown" or "md" => ExportFormat.Markdown,
            _ => throw new ArgumentException($"Invalid export format: {format}. Supported formats: json, markdown", nameof(format))
        };
    }
}

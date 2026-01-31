using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to delete an AI model configuration
/// </summary>
/// <remarks>
/// Issue #2567: Admin endpoint for deleting AI models
/// Note: This is a hard delete. Consider soft-delete if needed in the future.
/// </remarks>
internal sealed record DeleteAiModelCommand(Guid Id) : ICommand<bool>;

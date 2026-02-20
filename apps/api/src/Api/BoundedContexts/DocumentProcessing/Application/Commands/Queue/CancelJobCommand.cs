using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Cancels a processing or queued job.
/// Issue #4731: Queue commands.
/// </summary>
internal record CancelJobCommand(Guid JobId) : ICommand;

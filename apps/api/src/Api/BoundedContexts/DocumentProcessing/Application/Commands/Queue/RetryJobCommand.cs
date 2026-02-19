using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Retries a failed processing job.
/// Issue #4731: Queue commands.
/// </summary>
internal record RetryJobCommand(Guid JobId) : ICommand;

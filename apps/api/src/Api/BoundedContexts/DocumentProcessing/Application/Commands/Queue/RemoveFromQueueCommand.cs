using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Removes a queued (not yet processing) job from the queue.
/// Issue #4731: Queue commands.
/// </summary>
internal record RemoveFromQueueCommand(Guid JobId) : ICommand;

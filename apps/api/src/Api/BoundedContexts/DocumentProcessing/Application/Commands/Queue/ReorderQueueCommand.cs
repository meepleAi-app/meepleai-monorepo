using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Reorders queued jobs by updating their priority values.
/// Receives an ordered list of job IDs reflecting the desired queue order.
/// Issue #4731: Queue commands.
/// </summary>
internal record ReorderQueueCommand(List<Guid> OrderedJobIds) : ICommand;

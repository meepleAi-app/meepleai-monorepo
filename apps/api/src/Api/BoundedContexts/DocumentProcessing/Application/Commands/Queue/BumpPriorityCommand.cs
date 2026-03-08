using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Bumps the priority of a queued processing job.
/// Issue #5455: Admin priority management.
/// </summary>
internal record BumpPriorityCommand(
    Guid JobId,
    ProcessingPriority NewPriority
) : ICommand;

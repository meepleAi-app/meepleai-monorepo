using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Sets the priority of a queued processing job to an absolute value.
/// Issue #5455: Admin priority management.
/// </summary>
internal record SetPriorityCommand(
    Guid JobId,
    ProcessingPriority NewPriority
) : ICommand;

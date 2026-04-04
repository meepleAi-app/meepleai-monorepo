using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to retry a dead-lettered notification queue item.
/// Resets the item status to Pending and clears the error.
/// </summary>
internal record RetryDeadLetterCommand(Guid ItemId) : ICommand<bool>;

using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for sending loan reminder notifications.
/// Enforces rate limiting (max 1 per 24h) and validates game is loaned out.
/// </summary>
internal class SendLoanReminderCommandHandler : ICommandHandler<SendLoanReminderCommand>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SendLoanReminderCommandHandler> _logger;

    public SendLoanReminderCommandHandler(
        IUserLibraryRepository libraryRepository,
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork,
        ILogger<SendLoanReminderCommandHandler> logger)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SendLoanReminderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Find library entry
        var entry = await _libraryRepository.GetByUserAndGameAsync(command.UserId, command.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Game {command.GameId} not found in your library");

        // Validate game is loaned out
        if (entry.CurrentState.Value != GameStateType.InPrestito)
        {
            throw new ConflictException("Cannot send loan reminder - game is not currently loaned out");
        }

        // Rate limiting: Check for recent reminders (simplified - in production use distributed cache)
        var recentNotifications = await _notificationRepository.GetByUserIdAsync(
            command.UserId,
            unreadOnly: false,
            limit: 100,
            cancellationToken).ConfigureAwait(false);

        var recentReminder = recentNotifications
            .Where(n => n.Type == NotificationType.LoanReminder)
            .Where(n => n.Metadata != null && n.Metadata.Contains(command.GameId.ToString(), StringComparison.Ordinal))
            .OrderByDescending(n => n.CreatedAt)
            .FirstOrDefault();

        if (recentReminder != null && (DateTime.UtcNow - recentReminder.CreatedAt).TotalHours < 24)
        {
            throw new ConflictException("Loan reminder already sent in the last 24 hours. Please wait before sending another.");
        }

        // Create notification
        var borrowerInfo = entry.CurrentState.StateNotes ?? "someone";
        var defaultMessage = $"Reminder: Your game is currently loaned to {borrowerInfo}";
        var message = command.CustomMessage ?? defaultMessage;

        var notification = new Notification(
            id: Guid.NewGuid(),
            userId: command.UserId,
            type: NotificationType.LoanReminder,
            severity: NotificationSeverity.Info,
            title: "Loan Reminder",
            message: message,
            link: $"/games/{command.GameId}",
            metadata: $"{{\"gameId\":\"{command.GameId}\",\"type\":\"loan-reminder\"}}"
        );

        await _notificationRepository.AddAsync(notification, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Sent loan reminder for game {GameId} to user {UserId}",
            command.GameId, command.UserId);
    }
}

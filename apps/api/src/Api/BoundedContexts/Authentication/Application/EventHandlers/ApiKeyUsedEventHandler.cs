using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Handles ApiKeyUsedEvent by logging usage to the database.
/// Provides audit trail and analytics for API key usage.
/// </summary>
internal class ApiKeyUsedEventHandler : INotificationHandler<ApiKeyUsedEvent>
{
    private readonly IApiKeyUsageLogRepository _usageLogRepository;
    private readonly ILogger<ApiKeyUsedEventHandler> _logger;

    public ApiKeyUsedEventHandler(
        IApiKeyUsageLogRepository usageLogRepository,
        ILogger<ApiKeyUsedEventHandler> logger)
    {
        _usageLogRepository = usageLogRepository ?? throw new ArgumentNullException(nameof(usageLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ApiKeyUsedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        try
        {
            var usageLog = ApiKeyUsageLog.Create(
                Guid.NewGuid(),
                notification.KeyId,
                notification.Endpoint,
                notification.IpAddress,
                notification.UserAgent,
                usedAt: notification.UsedAt);

            await _usageLogRepository.AddAsync(usageLog, cancellationToken).ConfigureAwait(false);
            await _usageLogRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "Recorded API key usage: KeyId={KeyId}, Endpoint={Endpoint}, UsedAt={UsedAt}",
                notification.KeyId,
                notification.Endpoint,
                notification.UsedAt);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: EVENT HANDLER PATTERN - Background event processing
        // Event handlers must not throw exceptions (violates mediator/event pattern).
        // Errors logged for monitoring; failed usage logging doesn't break API requests.
#pragma warning restore S125
        catch (Exception ex)
        {
            // Log error but don't throw - usage logging should not break the request
            _logger.LogError(
                ex,
                "Failed to record API key usage: KeyId={KeyId}, Endpoint={Endpoint}",
                notification.KeyId,
                notification.Endpoint);
        }
#pragma warning restore CA1031
    }
}

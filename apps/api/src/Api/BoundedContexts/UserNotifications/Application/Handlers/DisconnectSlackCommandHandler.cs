using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for DisconnectSlackCommand.
/// Disconnects the Slack connection, revokes the token (best-effort), and saves.
/// </summary>
internal class DisconnectSlackCommandHandler : ICommandHandler<DisconnectSlackCommand, bool>
{
#pragma warning disable S1075 // URIs should not be hardcoded - Slack API base URL
    private const string SlackAuthRevokeUrl = "https://slack.com/api/auth.revoke";
#pragma warning restore S1075

    private readonly ISlackConnectionRepository _connectionRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly SlackNotificationConfiguration _config;
    private readonly ILogger<DisconnectSlackCommandHandler> _logger;

    public DisconnectSlackCommandHandler(
        ISlackConnectionRepository connectionRepository,
        IHttpClientFactory httpClientFactory,
        IUnitOfWork unitOfWork,
        IOptions<SlackNotificationConfiguration> config,
        ILogger<DisconnectSlackCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(connectionRepository);
        _connectionRepository = connectionRepository;
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClientFactory = httpClientFactory;
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _unitOfWork = unitOfWork;
        ArgumentNullException.ThrowIfNull(config);
        _config = config.Value;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public async Task<bool> Handle(DisconnectSlackCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var connection = await _connectionRepository.GetActiveByUserIdAsync(command.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (connection == null)
        {
            _logger.LogWarning("No active Slack connection found for user {UserId}", command.UserId);
            return false;
        }

        // Best-effort token revocation
        await RevokeTokenAsync(connection.BotAccessToken, cancellationToken).ConfigureAwait(false);

        connection.Disconnect(DateTime.UtcNow);
        await _connectionRepository.UpdateAsync(connection, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Disconnected Slack for user {UserId}", command.UserId);
        return true;
    }

    private async Task RevokeTokenAsync(string token, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SlackOAuth");
            var content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["token"] = token,
                ["client_id"] = _config.ClientId,
                ["client_secret"] = _config.ClientSecret
            });

            await client.PostAsync(SlackAuthRevokeUrl, content, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Best-effort Slack token revocation failed");
        }
    }
}

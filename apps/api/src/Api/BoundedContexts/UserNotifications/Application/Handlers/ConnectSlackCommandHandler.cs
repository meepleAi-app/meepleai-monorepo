using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for ConnectSlackCommand.
/// Builds the Slack OAuth authorization URL for the user to grant permissions.
/// Uses a signed, time-limited state token to prevent CSRF attacks.
/// </summary>
internal class ConnectSlackCommandHandler : ICommandHandler<ConnectSlackCommand, string>
{
    private readonly SlackNotificationConfiguration _config;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<ConnectSlackCommandHandler> _logger;

    public ConnectSlackCommandHandler(
        IOptions<SlackNotificationConfiguration> config,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<ConnectSlackCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config.Value;
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        _dataProtectionProvider = dataProtectionProvider;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public Task<string> Handle(ConnectSlackCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Generate CSRF state: encrypt userId + timestamp with time-limited protector (10 min)
        var protector = _dataProtectionProvider.CreateProtector("MeepleAI.SlackOAuth");
        var timedProtector = protector.ToTimeLimitedDataProtector();
        var state = timedProtector.Protect(command.UserId.ToString(), TimeSpan.FromMinutes(10));

        var oauthUrl = $"https://slack.com/oauth/v2/authorize" +
            $"?client_id={Uri.EscapeDataString(_config.ClientId)}" +
            $"&scope=chat:write,im:write" +
            $"&redirect_uri={Uri.EscapeDataString(_config.RedirectUri)}" +
            $"&state={Uri.EscapeDataString(state)}";

        _logger.LogInformation("Generated Slack OAuth URL for user {UserId}", command.UserId);

        return Task.FromResult(oauthUrl);
    }
}

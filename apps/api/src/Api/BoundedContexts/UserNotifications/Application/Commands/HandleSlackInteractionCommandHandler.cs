using System.Globalization;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Handles Slack interaction payloads (button clicks) with two-phase processing:
/// 1. Validate signature and parse payload synchronously
/// 2. Dispatch the mapped MediatR command and call response_url asynchronously
/// </summary>
internal class HandleSlackInteractionCommandHandler : ICommandHandler<HandleSlackInteractionCommand, SlackInteractionResult>
{
    private static readonly TimeSpan ActionExpiryDuration = TimeSpan.FromHours(24);

    /// <summary>
    /// Maps Slack action_id values to factory functions that create MediatR commands.
    /// The factory receives (resourceGuid, userId) where userId is the MeepleAI user
    /// resolved from the Slack interaction's user.id field.
    /// </summary>
    private static readonly Dictionary<string, Func<Guid, Guid, IBaseRequest>> ActionMap = new(StringComparer.Ordinal)
    {
        ["share_request_approve"] = (resourceId, userId) => new ApproveShareRequestCommand(resourceId, userId),
        ["share_request_reject"] = (resourceId, userId) => new RejectShareRequestCommand(resourceId, userId, "Rejected via Slack"),
        ["game_night_rsvp_yes"] = (resourceId, userId) => new RespondToGameNightCommand(resourceId, userId, RsvpStatus.Accepted),
        ["game_night_rsvp_no"] = (resourceId, userId) => new RespondToGameNightCommand(resourceId, userId, RsvpStatus.Declined),
        ["game_night_rsvp_maybe"] = (resourceId, userId) => new RespondToGameNightCommand(resourceId, userId, RsvpStatus.Maybe),
    };

    private readonly SlackSignatureValidator _signatureValidator;
    private readonly ISlackConnectionRepository _slackConnectionRepository;
    private readonly IMediator _mediator;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HandleSlackInteractionCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public HandleSlackInteractionCommandHandler(
        SlackSignatureValidator signatureValidator,
        ISlackConnectionRepository slackConnectionRepository,
        IMediator mediator,
        IHttpClientFactory httpClientFactory,
        ILogger<HandleSlackInteractionCommandHandler> logger,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(signatureValidator);
        ArgumentNullException.ThrowIfNull(slackConnectionRepository);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _signatureValidator = signatureValidator;
        _slackConnectionRepository = slackConnectionRepository;
        _mediator = mediator;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<SlackInteractionResult> Handle(HandleSlackInteractionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Phase 1: Validate HMAC signature
        if (!_signatureValidator.Validate(command.Timestamp, command.Payload, command.Signature))
        {
            _logger.LogWarning("Slack interaction signature validation failed");
            return new SlackInteractionResult(false, "Invalid signature");
        }

        // Parse payload JSON
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(command.Payload);
            root = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Slack interaction payload");
            return new SlackInteractionResult(false, "Invalid payload");
        }

        // Extract action details: actions[0].action_id, actions[0].value, actions[0].block_id
        if (!root.TryGetProperty("actions", out var actions) ||
            actions.GetArrayLength() == 0)
        {
            _logger.LogWarning("Slack interaction payload missing actions array");
            return new SlackInteractionResult(false, "Invalid action");
        }

        var action = actions[0];
        var actionId = action.GetProperty("action_id").GetString() ?? string.Empty;
        var actionValue = action.GetProperty("value").GetString() ?? string.Empty;
        var blockId = action.GetProperty("block_id").GetString() ?? string.Empty;

        // Parse block_id: {action_prefix}:{resource_guid}:{unix_timestamp_seconds}
        var segments = blockId.Split(':');
        if (segments.Length != 3)
        {
            _logger.LogWarning("Slack interaction block_id has invalid segment count: {BlockId}", blockId);
            return new SlackInteractionResult(false, "\u26a0\ufe0f Invalid action. Please visit MeepleAI.");
        }

        if (!long.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var timestampSeconds))
        {
            _logger.LogWarning("Slack interaction block_id has non-numeric timestamp: {BlockId}", blockId);
            return new SlackInteractionResult(false, "\u26a0\ufe0f Invalid action. Please visit MeepleAI.");
        }

        // Check action expiry (24h)
        var actionCreatedAt = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
        var now = _timeProvider.GetUtcNow();
        if (now - actionCreatedAt > ActionExpiryDuration)
        {
            _logger.LogInformation("Slack interaction expired. BlockId: {BlockId}, Age: {Age}", blockId, now - actionCreatedAt);
            return new SlackInteractionResult(false, "\u23f0 This action has expired. Please visit MeepleAI to take action.");
        }

        // Look up MeepleAI user via SlackConnection
        var slackUserId = root.TryGetProperty("user", out var user)
            ? user.GetProperty("id").GetString() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrEmpty(slackUserId))
        {
            _logger.LogWarning("Slack interaction payload missing user.id");
            return new SlackInteractionResult(false, "Unable to identify user");
        }

        var connection = await _slackConnectionRepository.GetBySlackUserIdAsync(slackUserId, cancellationToken).ConfigureAwait(false);
        if (connection is null || !connection.IsActive)
        {
            _logger.LogWarning("No active Slack connection found for SlackUserId: {SlackUserId}", slackUserId);
            return new SlackInteractionResult(false, "Slack connection not found. Please reconnect in MeepleAI.");
        }

        // Map action_id to MediatR command
        if (!ActionMap.TryGetValue(actionId, out var commandFactory))
        {
            _logger.LogWarning("Unknown Slack action_id: {ActionId}", actionId);
            return new SlackInteractionResult(false, "Unknown action");
        }

        // Parse resource GUID from action value
        if (!Guid.TryParse(actionValue, out var resourceId))
        {
            _logger.LogWarning("Invalid resource GUID in Slack action value: {ActionValue}", actionValue);
            return new SlackInteractionResult(false, "\u26a0\ufe0f Invalid action. Please visit MeepleAI.");
        }

        // Phase 2: Dispatch command and call response_url (best-effort)
        var responseUrl = root.TryGetProperty("response_url", out var respUrl)
            ? respUrl.GetString()
            : null;

        try
        {
            var mediatRCommand = commandFactory(resourceId, connection.UserId);
            await _mediator.Send(mediatRCommand, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Slack interaction processed successfully. ActionId: {ActionId}, ResourceId: {ResourceId}, UserId: {UserId}",
                actionId, resourceId, connection.UserId);

            var successMessage = GetSuccessMessage(actionId);

            // Best-effort response_url callback
            await SendResponseUrlAsync(responseUrl, successMessage).ConfigureAwait(false);

            return new SlackInteractionResult(true, successMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process Slack interaction. ActionId: {ActionId}, ResourceId: {ResourceId}",
                actionId, resourceId);

            var errorMessage = "\u274c Could not process action. Please try in MeepleAI.";
            await SendResponseUrlAsync(responseUrl, errorMessage).ConfigureAwait(false);

            return new SlackInteractionResult(false, errorMessage);
        }
    }

    private static string GetSuccessMessage(string actionId) => actionId switch
    {
        "share_request_approve" => "\u2705 Share request approved by you.",
        "share_request_reject" => "\u274c Share request rejected by you.",
        "game_night_rsvp_yes" => "\u2705 You accepted the game night invitation!",
        "game_night_rsvp_no" => "\u274c You declined the game night invitation.",
        "game_night_rsvp_maybe" => "\ud83e\udd14 You responded 'maybe' to the game night invitation.",
        _ => "\u2705 Action processed."
    };

    /// <summary>
    /// Sends a deferred response to the Slack response_url (valid 30 minutes, up to 5 responses).
    /// This is fire-and-forget; failures are logged but do not affect the result.
    /// </summary>
    private async Task SendResponseUrlAsync(string? responseUrl, string message)
    {
        if (string.IsNullOrEmpty(responseUrl))
            return;

        try
        {
            using var client = _httpClientFactory.CreateClient();
            var payload = JsonSerializer.Serialize(new
            {
                text = message,
                replace_original = true
            });

            using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync(responseUrl, content).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Slack response_url callback failed with status {StatusCode}",
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Slack response_url callback");
        }
    }
}

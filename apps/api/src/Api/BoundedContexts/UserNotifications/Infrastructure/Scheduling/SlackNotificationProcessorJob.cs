using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using Api.Infrastructure;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job for processing queued Slack notifications (DM and team channel).
/// Runs every 10 seconds, processes up to 20 items per execution.
/// Groups by SlackTeamId for rate limiting. Handles 429 with Retry-After,
/// token revocation with connection deactivation, and dead letter escalation.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class SlackNotificationProcessorJob : IJob
{
    private readonly INotificationQueueRepository _queueRepository;
    private readonly ISlackConnectionRepository _slackConnectionRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SlackMessageBuilderFactory _builderFactory;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<SlackNotificationProcessorJob> _logger;

    private const int BatchSize = 20;
    private static readonly Uri SlackChatPostMessageUri = new("https://slack.com/api/chat.postMessage");

    public SlackNotificationProcessorJob(
        INotificationQueueRepository queueRepository,
        ISlackConnectionRepository slackConnectionRepository,
        INotificationRepository notificationRepository,
        IHttpClientFactory httpClientFactory,
        SlackMessageBuilderFactory builderFactory,
        MeepleAiDbContext dbContext,
        ILogger<SlackNotificationProcessorJob> logger)
    {
        _queueRepository = queueRepository ?? throw new ArgumentNullException(nameof(queueRepository));
        _slackConnectionRepository = slackConnectionRepository ?? throw new ArgumentNullException(nameof(slackConnectionRepository));
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _builderFactory = builderFactory ?? throw new ArgumentNullException(nameof(builderFactory));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogDebug("Starting Slack notification processor job: FireTime={FireTime}", context.FireTimeUtc);

        var sentCount = 0;
        var failedCount = 0;
        var rateLimitedCount = 0;

        try
        {
            // Fetch pending items for both SlackUser and SlackTeam channels
            var slackUserItems = await _queueRepository
                .GetPendingByChannelAsync(NotificationChannelType.SlackUser, BatchSize, context.CancellationToken)
                .ConfigureAwait(false);

            var slackTeamItems = await _queueRepository
                .GetPendingByChannelAsync(NotificationChannelType.SlackTeam, BatchSize, context.CancellationToken)
                .ConfigureAwait(false);

            var allItems = slackUserItems.Concat(slackTeamItems).ToList();

            if (allItems.Count == 0)
            {
                _logger.LogDebug("No pending Slack notifications to process");
                context.Result = new { Success = true, Sent = 0, Failed = 0, RateLimited = 0 };
                return;
            }

            _logger.LogInformation("Processing {Count} pending Slack notifications", allItems.Count);

            // Group by SlackTeamId for rate limiting awareness
            var groupedByTeam = allItems.GroupBy(i => i.SlackTeamId ?? "__webhook__", StringComparer.Ordinal);
            var rateLimitedTeams = new HashSet<string>(StringComparer.Ordinal);

            foreach (var teamGroup in groupedByTeam)
            {
                if (context.CancellationToken.IsCancellationRequested)
                    break;

                var teamId = teamGroup.Key;

                foreach (var item in teamGroup)
                {
                    if (context.CancellationToken.IsCancellationRequested)
                        break;

                    // Skip remaining items for rate-limited teams
                    if (rateLimitedTeams.Contains(teamId))
                    {
                        rateLimitedCount++;
                        continue;
                    }

                    try
                    {
                        item.MarkAsProcessing();
                        await _queueRepository.UpdateAsync(item, context.CancellationToken).ConfigureAwait(false);
                        await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

                        await SendSlackMessageAsync(item, context.CancellationToken).ConfigureAwait(false);

                        item.MarkAsSent(DateTime.UtcNow);
                        await _queueRepository.UpdateAsync(item, context.CancellationToken).ConfigureAwait(false);
                        await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

                        sentCount++;

                        _logger.LogInformation(
                            "Slack notification {ItemId} sent successfully via {Channel}, team={TeamId}",
                            item.Id, item.ChannelType, item.SlackTeamId);
                    }
                    catch (SlackRateLimitException ex)
                    {
                        rateLimitedCount++;
                        rateLimitedTeams.Add(teamId);

                        _logger.LogWarning(
                            ex,
                            "Slack rate limited for team {TeamId}, retryAfter={RetryAfter}s. Deferring remaining items.",
                            teamId, ex.RetryAfterSeconds);

                        await HandleRateLimitAsync(item, ex.RetryAfterSeconds, context.CancellationToken)
                            .ConfigureAwait(false);
                        break; // Move to next team
                    }
                    catch (SlackTokenRevokedException ex)
                    {
                        failedCount++;

                        _logger.LogWarning(
                            ex,
                            "Slack token revoked for team {TeamId}, deactivating connection. Item {ItemId} dead-lettered.",
                            teamId, item.Id);

                        await HandleTokenRevocationAsync(item, context.CancellationToken).ConfigureAwait(false);
                    }
#pragma warning disable CA1031
                    catch (Exception ex)
                    {
                        failedCount++;

                        _logger.LogError(
                            ex,
                            "Failed to send Slack notification {ItemId} to {Channel}, team={TeamId}",
                            item.Id, item.SlackChannelTarget, item.SlackTeamId);

                        await HandleFailureAsync(item, ex.Message, context.CancellationToken).ConfigureAwait(false);
                    }
#pragma warning restore CA1031
                }
            }

            _logger.LogInformation(
                "Slack processor completed: Sent={Sent}, Failed={Failed}, RateLimited={RateLimited}",
                sentCount, failedCount, rateLimitedCount);

            context.Result = new { Success = true, Sent = sentCount, Failed = failedCount, RateLimited = rateLimitedCount };
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Slack notification processor job failed");
            context.Result = new { Success = false, Error = ex.Message };
        }
#pragma warning restore CA1031
    }

    private async Task SendSlackMessageAsync(NotificationQueueItem item, CancellationToken ct)
    {
        using var client = _httpClientFactory.CreateClient("SlackApi");

        if (item.ChannelType == NotificationChannelType.SlackTeam)
        {
            // Webhook-based delivery for team channels
            await SendViaWebhookAsync(client, item, ct).ConfigureAwait(false);
        }
        else
        {
            // Bot token-based delivery for DMs
            await SendViaBotApiAsync(client, item, ct).ConfigureAwait(false);
        }
    }

    private async Task SendViaWebhookAsync(HttpClient client, NotificationQueueItem item, CancellationToken ct)
    {
        var webhookUrl = item.SlackChannelTarget
            ?? throw new InvalidOperationException($"Missing webhook URL for SlackTeam item {item.Id}");

        var builder = _builderFactory.GetBuilder(item.NotificationType);
        var message = builder.BuildMessage(item.Payload, null);
        var payload = JsonSerializer.Serialize(message);

        using var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = ParseRetryAfter(response.Headers.RetryAfter);
            throw new SlackRateLimitException(retryAfter);
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            if (IsTokenRevocationError(responseBody))
                throw new SlackTokenRevokedException($"Webhook revoked: {responseBody}");

            throw new HttpRequestException($"Slack webhook failed: {response.StatusCode} - {responseBody}");
        }
    }

    private async Task SendViaBotApiAsync(HttpClient client, NotificationQueueItem item, CancellationToken ct)
    {
        var channelId = item.SlackChannelTarget
            ?? throw new InvalidOperationException($"Missing channel ID for SlackUser item {item.Id}");

        // Look up the bot token from the Slack connection
        string? botToken = null;
        if (item.RecipientUserId.HasValue)
        {
            var connection = await _slackConnectionRepository
                .GetActiveByUserIdAsync(item.RecipientUserId.Value, ct)
                .ConfigureAwait(false);
            botToken = connection?.BotAccessToken;
        }

        if (string.IsNullOrEmpty(botToken))
            throw new InvalidOperationException($"No active bot token for item {item.Id}, user {item.RecipientUserId}");

        var builder = _builderFactory.GetBuilder(item.NotificationType);
        var blockKitMessage = builder.BuildMessage(item.Payload, null);
        // Merge channel into the Block Kit message by serializing and re-parsing
        var messageJson = JsonSerializer.Serialize(blockKitMessage);
        using var mergedDoc = JsonDocument.Parse(messageJson);
        var blocksElement = mergedDoc.RootElement.TryGetProperty("blocks", out var blocks) ? blocks : default;
        var payload = blocksElement.ValueKind != JsonValueKind.Undefined
            ? JsonSerializer.Serialize(new { channel = channelId, blocks = JsonSerializer.Deserialize<object>(blocksElement.GetRawText()) })
            : JsonSerializer.Serialize(new { channel = channelId, text = messageJson });

        using var request = new HttpRequestMessage(HttpMethod.Post, SlackChatPostMessageUri)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", botToken);

        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = ParseRetryAfter(response.Headers.RetryAfter);
            throw new SlackRateLimitException(retryAfter);
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        // Slack API returns 200 with ok=false for errors
        using var jsonDoc = JsonDocument.Parse(responseBody);
        var root = jsonDoc.RootElement;

        if (root.TryGetProperty("ok", out var okProp) && !okProp.GetBoolean())
        {
            var error = root.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : "unknown";

            if (IsTokenRevocationError(error))
                throw new SlackTokenRevokedException($"Token revoked: {error}");

            throw new HttpRequestException($"Slack API error: {error}");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Slack API HTTP error: {response.StatusCode} - {responseBody}");
        }
    }

    private async Task HandleRateLimitAsync(
        NotificationQueueItem currentItem,
        int retryAfterSeconds,
        CancellationToken ct)
    {
        var retryAt = DateTime.UtcNow.AddSeconds(retryAfterSeconds);

        try
        {
            // Reset current item back to pending with retry time
            currentItem.MarkAsFailed($"Rate limited, retry after {retryAfterSeconds}s");
            currentItem.SetNextRetryAt(retryAt);
            await _queueRepository.UpdateAsync(currentItem, ct).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update rate-limited item {ItemId}", currentItem.Id);
        }
#pragma warning restore CA1031
    }

    private async Task HandleTokenRevocationAsync(NotificationQueueItem item, CancellationToken ct)
    {
        try
        {
            // Dead-letter the item
            item.MarkAsDeadLetter("Token revoked by workspace admin");
            await _queueRepository.UpdateAsync(item, ct).ConfigureAwait(false);

            // Deactivate the Slack connection
            if (item.RecipientUserId.HasValue)
            {
                var connection = await _slackConnectionRepository
                    .GetActiveByUserIdAsync(item.RecipientUserId.Value, ct)
                    .ConfigureAwait(false);

                if (connection != null)
                {
                    connection.Deactivate();
                    await _slackConnectionRepository.UpdateAsync(connection, ct).ConfigureAwait(false);

                    _logger.LogWarning(
                        "Deactivated Slack connection {ConnectionId} for user {UserId} due to token revocation",
                        connection.Id, item.RecipientUserId);
                }

                // Create in-app notification about the disconnection
                var notification = new Notification(
                    id: Guid.NewGuid(),
                    userId: item.RecipientUserId.Value,
                    type: NotificationType.ProcessingFailed,
                    severity: NotificationSeverity.Warning,
                    title: "Slack Disconnected",
                    message: "Your Slack workspace connection has been revoked. Please reconnect to continue receiving Slack notifications.",
                    link: "/settings/notifications");

                await _notificationRepository.AddAsync(notification, ct).ConfigureAwait(false);
            }

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle token revocation for item {ItemId}", item.Id);
        }
#pragma warning restore CA1031
    }

    private async Task HandleFailureAsync(NotificationQueueItem item, string errorMessage, CancellationToken ct)
    {
        try
        {
            item.MarkAsFailed(errorMessage);
            await _queueRepository.UpdateAsync(item, ct).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            if (item.Status.IsDeadLetter)
            {
                _logger.LogWarning(
                    "Slack notification {ItemId} moved to dead letter after {RetryCount} retries",
                    item.Id, item.RetryCount);
            }
        }
#pragma warning disable CA1031
        catch (Exception updateEx)
        {
            _logger.LogError(updateEx, "Failed to update Slack notification {ItemId} status after failure", item.Id);
        }
#pragma warning restore CA1031
    }

    private static int ParseRetryAfter(RetryConditionHeaderValue? retryAfter)
    {
        if (retryAfter?.Delta.HasValue == true)
            return (int)retryAfter.Delta.Value.TotalSeconds;

        // Default to 30 seconds if no Retry-After header
        return 30;
    }

    private static bool IsTokenRevocationError(string? error)
    {
        return error != null &&
            (error.Contains("invalid_auth", StringComparison.OrdinalIgnoreCase) ||
             error.Contains("token_revoked", StringComparison.OrdinalIgnoreCase) ||
             error.Contains("account_inactive", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Exception thrown when Slack returns HTTP 429 (Too Many Requests).
/// </summary>
public sealed class SlackRateLimitException : Exception
{
    public int RetryAfterSeconds { get; }

    public SlackRateLimitException(int retryAfterSeconds)
        : base($"Slack rate limited, retry after {retryAfterSeconds}s")
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}

/// <summary>
/// Exception thrown when Slack token has been revoked or is invalid.
/// </summary>
public sealed class SlackTokenRevokedException : Exception
{
    public SlackTokenRevokedException(string message) : base(message) { }
}

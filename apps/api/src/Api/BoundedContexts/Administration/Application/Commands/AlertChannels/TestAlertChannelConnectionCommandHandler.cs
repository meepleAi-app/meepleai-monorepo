using System.Text.Json;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.External;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertChannels;

/// <summary>
/// Handles <see cref="TestAlertChannelConnectionCommand"/>. Looks up the
/// stored channel, dispatches a benign probe, records the outcome on the
/// aggregate, and surfaces it back to the caller.
///
/// <para>For Email the probe is a no-op success today: a deeper SMTP probe
/// would require either a live test recipient or an SMTP HELO/EHLO handshake
/// — deferred to a follow-up. Slack uses <see cref="ISlackWebhookClient.TestConnectionAsync"/>.</para>
/// </summary>
internal sealed class TestAlertChannelConnectionCommandHandler
    : IRequestHandler<TestAlertChannelConnectionCommand, TestAlertChannelConnectionResult>
{
    private readonly IAlertChannelRepository _repository;
    private readonly ISlackWebhookClient _slackClient;
    private readonly TimeProvider _timeProvider;

    public TestAlertChannelConnectionCommandHandler(
        IAlertChannelRepository repository,
        ISlackWebhookClient slackClient,
        TimeProvider? timeProvider = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _slackClient = slackClient ?? throw new ArgumentNullException(nameof(slackClient));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<TestAlertChannelConnectionResult> Handle(
        TestAlertChannelConnectionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var type = AlertChannelTypeExtensions.FromWireValue(request.Type);
        var channel = await _repository.GetByTypeAsync(type, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("AlertChannel", request.Type);

        var probedAt = _timeProvider.GetUtcNow().UtcDateTime;
        bool success;
        string message;
        int? statusCode = null;

        switch (type)
        {
            case AlertChannelType.Slack:
                if (!TryReadWebhookUrl(channel.ConfigJson, out var webhookUrl))
                {
                    success = false;
                    message = "Slack channel configuration is missing 'webhookUrl'.";
                }
                else
                {
                    var slackResult = await _slackClient
                        .TestConnectionAsync(webhookUrl, cancellationToken)
                        .ConfigureAwait(false);
                    success = slackResult.Success;
                    message = slackResult.Message;
                    statusCode = slackResult.StatusCode;
                }
                break;

            case AlertChannelType.Email:
                // Light validation only — a real SMTP probe is deferred. We
                // accept the channel as "OK" if the persisted config parses
                // into the expected shape.
                if (TryReadEmailRecipients(channel.ConfigJson, out var recipients) && recipients.Count > 0)
                {
                    success = true;
                    message = $"Email channel configured ({recipients.Count} recipient(s)). SMTP probe not yet implemented.";
                }
                else
                {
                    success = false;
                    message = "Email channel configuration is missing recipients.";
                }
                break;

            default:
                success = false;
                message = $"Test-connection not supported for channel type '{type}'.";
                break;
        }

        channel.RecordTestResult(success, message, probedAt);
        await _repository.UpsertAsync(channel, cancellationToken).ConfigureAwait(false);

        return new TestAlertChannelConnectionResult(success, message, statusCode, probedAt);
    }

    private static bool TryReadWebhookUrl(string configJson, out string webhookUrl)
    {
        webhookUrl = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            if (doc.RootElement.TryGetProperty("webhookUrl", out var prop)
                && prop.ValueKind == JsonValueKind.String)
            {
                var value = prop.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    webhookUrl = value;
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            // fallthrough — caller treats as failure
        }
        return false;
    }

    private static bool TryReadEmailRecipients(string configJson, out IReadOnlyList<string> recipients)
    {
        recipients = Array.Empty<string>();
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            if (doc.RootElement.TryGetProperty("recipients", out var prop)
                && prop.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>(prop.GetArrayLength());
                foreach (var item in prop.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    {
                        list.Add(item.GetString()!);
                    }
                }
                recipients = list;
                return true;
            }
        }
        catch (JsonException)
        {
            // fallthrough
        }
        return false;
    }
}

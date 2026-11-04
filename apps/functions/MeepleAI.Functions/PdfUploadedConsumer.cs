using Azure.Messaging.ServiceBus;
using MeepleAI.Functions.Contracts;
using MeepleAI.Functions.Forwarding;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MeepleAI.Functions;

/// <summary>
/// Consumer della coda <c>pdf-uploaded</c> (spec azure-local §4.3). Settlement esplicito: contratto invalido in
/// dead-letter con motivo (Review Focus 3), guasto transitorio abbandonato (Service Bus riconsegna fino a
/// MaxDeliveryCount), esito definitivo completato.
/// </summary>
public sealed class PdfUploadedConsumer
{
    private readonly IPdfUploadedForwarder _forwarder;
    private readonly ILogger<PdfUploadedConsumer> _logger;

    public PdfUploadedConsumer(IPdfUploadedForwarder forwarder, ILogger<PdfUploadedConsumer> logger)
    {
        _forwarder = forwarder;
        _logger = logger;
    }

    [Function("PdfUploadedConsumer")]
    public async Task Run(
        [ServiceBusTrigger("%ServiceBusQueue%", Connection = "ServiceBusConnection", AutoCompleteMessages = false)] ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(actions);
        var parsed = PdfUploadedContract.TryParse(message.Body);
        if (!parsed.IsValid)
        {
            _logger.LogWarning("Message {MessageId} dead-lettered: {Reason} ({Detail})", message.MessageId, parsed.Reason, parsed.Detail);
            await actions.DeadLetterMessageAsync(message, deadLetterReason: parsed.Reason, deadLetterErrorDescription: parsed.Detail, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        var outcome = await _forwarder.ForwardAsync(parsed.Event!, cancellationToken).ConfigureAwait(false);
        if (outcome == ForwardOutcome.TransientFailure)
        {
            await actions.AbandonMessageAsync(message, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        await actions.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
    }
}

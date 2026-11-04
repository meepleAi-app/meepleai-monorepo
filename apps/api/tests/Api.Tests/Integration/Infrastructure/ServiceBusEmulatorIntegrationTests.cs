using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;
using Api.Infrastructure.Messaging;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Azure.Messaging.ServiceBus;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.Infrastructure;

[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Slow)]
[Trait("BoundedContext", "Infrastructure")]
[Trait("Feature", "AzureServiceBus")]
public sealed class ServiceBusEmulatorIntegrationTests : IClassFixture<ServiceBusEmulatorFixture>
{
    private readonly ServiceBusEmulatorFixture _emulator;

    public ServiceBusEmulatorIntegrationTests(ServiceBusEmulatorFixture emulator)
    {
        _emulator = emulator;
    }

    /// <summary>Riceve fino a trovare <paramref name="messageId"/>; un messaggio estraneo viene rilasciato (abandon).</summary>
    private static async Task<ServiceBusReceivedMessage?> ReceiveMatchingAsync(ServiceBusReceiver receiver, string messageId)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
            if (message is null)
            {
                return null;
            }

            if (string.Equals(message.MessageId, messageId, StringComparison.Ordinal))
            {
                return message;
            }

            await receiver.AbandonMessageAsync(message);
        }

        return null;
    }

    [Fact]
    public async Task Publish_ThenReceive_RoundTripsTheContract()
    {
        _emulator.SkipIfUnavailable();
        await using var sender = _emulator.Client.CreateSender(TestcontainersConfiguration.ServiceBusEmulatorDefaultQueue);
        await using var receiver = _emulator.Client.CreateReceiver(TestcontainersConfiguration.ServiceBusEmulatorDefaultQueue);
        var publisher = new ServiceBusIntegrationEventPublisher(sender, Mock.Of<ILogger<ServiceBusIntegrationEventPublisher>>());
        var evt = new PdfUploadedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), null, "pdfs/g/f_a.pdf");

        await publisher.PublishAsync(evt, CancellationToken.None);
        var received = await ReceiveMatchingAsync(receiver, evt.EventId.ToString());

        received.Should().NotBeNull();
        received!.MessageId.Should().Be(evt.EventId.ToString());
        received.ApplicationProperties["contractVersion"].Should().Be("v1");
        using var doc = JsonDocument.Parse(received.Body.ToString());
        doc.RootElement.GetProperty("pdfDocumentId").GetGuid().Should().Be(evt.PdfDocumentId);
        await receiver.CompleteMessageAsync(received);
    }

    [Fact]
    public async Task DeadLetter_LandsInDeadLetterSubQueueWithReason()
    {
        _emulator.SkipIfUnavailable();
        await using var sender = _emulator.Client.CreateSender(TestcontainersConfiguration.ServiceBusEmulatorDefaultQueue);
        await using var receiver = _emulator.Client.CreateReceiver(TestcontainersConfiguration.ServiceBusEmulatorDefaultQueue);
        await using var deadLetters = _emulator.Client.CreateReceiver(
            TestcontainersConfiguration.ServiceBusEmulatorDefaultQueue,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var messageId = Guid.NewGuid().ToString();

        await sender.SendMessageAsync(new ServiceBusMessage("{\"contractVersion\":\"v9\"}") { MessageId = messageId });
        var received = await ReceiveMatchingAsync(receiver, messageId);
        received.Should().NotBeNull();
        await receiver.DeadLetterMessageAsync(received!, "unsupported_contract", "contractVersion v9");

        var dead = await ReceiveMatchingAsync(deadLetters, messageId);

        dead.Should().NotBeNull();
        dead!.MessageId.Should().Be(messageId);
        dead.DeadLetterReason.Should().Be("unsupported_contract");
        // Nessun CompleteMessageAsync sul messaggio dead-letter: su servicebus-emulator:2.0.1 lancia sempre
        // ServiceBusException MessageLockLost ("The lock supplied is invalid. Either the lock expired, or the message
        // has already been removed from the queue, or was received by a different receiver instance."), anche con
        // il test eseguito da solo su container nuovi (3 run su 3). Le asserzioni sopra non ne sono toccate; il
        // container e' per classe e viene scartato.
    }
}

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

    [Fact]
    public async Task Publish_ThenReceive_RoundTripsTheContract()
    {
        _emulator.SkipIfUnavailable();
        await using var sender = _emulator.Client.CreateSender(TestcontainersConfiguration.ServiceBusEmulatorDefaultQueue);
        await using var receiver = _emulator.Client.CreateReceiver(TestcontainersConfiguration.ServiceBusEmulatorDefaultQueue);
        var publisher = new ServiceBusIntegrationEventPublisher(sender, Mock.Of<ILogger<ServiceBusIntegrationEventPublisher>>());
        var evt = new PdfUploadedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), null, "pdfs/g/f_a.pdf");

        await publisher.PublishAsync(evt, CancellationToken.None);
        var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30));

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
        var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30));
        received.Should().NotBeNull();
        await receiver.DeadLetterMessageAsync(received!, "unsupported_contract", "contractVersion v9");

        var dead = await deadLetters.ReceiveMessageAsync(TimeSpan.FromSeconds(30));

        dead.Should().NotBeNull();
        dead!.MessageId.Should().Be(messageId);
        dead.DeadLetterReason.Should().Be("unsupported_contract");
    }
}

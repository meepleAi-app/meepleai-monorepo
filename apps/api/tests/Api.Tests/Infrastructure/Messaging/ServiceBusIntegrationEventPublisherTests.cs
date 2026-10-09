using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;
using Api.Infrastructure.Messaging;
using Api.Tests.Constants;
using Azure.Messaging.ServiceBus;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Messaging;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class ServiceBusIntegrationEventPublisherTests
{
    [Fact]
    public async Task PublishAsync_SendsJsonEnvelopeWithContractMetadata()
    {
        var sender = new Mock<ServiceBusSender>();
        ServiceBusMessage? sent = null;
        sender.Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((m, _) => sent = m)
            .Returns(Task.CompletedTask);
        var sut = new ServiceBusIntegrationEventPublisher(sender.Object, Mock.Of<ILogger<ServiceBusIntegrationEventPublisher>>());
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var evt = new PdfUploadedIntegrationEvent(pdfId, gameId, null, "pdfs/g/f_a.pdf");

        await sut.PublishAsync(evt, CancellationToken.None);

        sent.Should().NotBeNull();
        sent!.ContentType.Should().Be("application/json");
        sent.MessageId.Should().Be(evt.EventId.ToString());
        sent.Subject.Should().Be(nameof(PdfUploadedIntegrationEvent));
        sent.ApplicationProperties["contractVersion"].Should().Be("v1");
        using var doc = JsonDocument.Parse(sent.Body.ToString());
        doc.RootElement.GetProperty("pdfDocumentId").GetGuid().Should().Be(pdfId);
        doc.RootElement.GetProperty("gameId").GetGuid().Should().Be(gameId);
        doc.RootElement.GetProperty("sharedGameId").ValueKind.Should().Be(JsonValueKind.Null);
        doc.RootElement.GetProperty("filePath").GetString().Should().Be("pdfs/g/f_a.pdf");
        doc.RootElement.GetProperty("contractVersion").GetString().Should().Be("v1");
        doc.RootElement.GetProperty("sourceContext").GetString().Should().Be("DocumentProcessing");
        doc.RootElement.TryGetProperty("occurredAt", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("eventId", out _).Should().BeTrue();
    }
}

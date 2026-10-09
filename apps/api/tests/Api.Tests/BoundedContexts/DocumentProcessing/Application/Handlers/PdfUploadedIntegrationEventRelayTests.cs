using System.Diagnostics.Metrics;
using Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;
using Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

[Collection("PdfUploadedEventMetrics")]
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfUploadedIntegrationEventRelayTests
{
    private readonly Mock<IIntegrationEventPublisher> _publisher = new();
    private readonly Mock<IPdfDocumentRepository> _repository = new();
    private readonly PdfUploadedIntegrationEventRelay _sut;

    public PdfUploadedIntegrationEventRelayTests()
    {
        _sut = new PdfUploadedIntegrationEventRelay(_publisher.Object, _repository.Object, Mock.Of<ILogger<PdfUploadedIntegrationEventRelay>>());
    }

    private PdfDocument SeedDocument(Guid id)
    {
        var document = new PdfDocumentBuilder()
            .WithId(id)
            .WithFilePath($"pdfs/{Guid.NewGuid():N}/{Guid.NewGuid():N}_rulebook.pdf")
            .Build();
        _repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(document);
        return document;
    }

    private static PdfStateChangedEvent PendingToUploading(Guid pdfId) =>
        new(pdfId, PdfProcessingState.Pending, PdfProcessingState.Uploading, Guid.NewGuid());

    /// <summary>Raccoglie i tag <c>outcome</c> di <c>meepleai.pdf.uploaded_events.total</c> emessi durante <paramref name="action"/>.</summary>
    private static async Task<List<string?>> CollectOutcomesAsync(Func<Task> action)
    {
        var outcomes = new List<string?>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == "meepleai.pdf.uploaded_events.total")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "outcome")
                {
                    outcomes.Add(tag.Value as string);
                }
            }
        });
        listener.Start();
        await action();
        return outcomes;
    }

    [Fact]
    public async Task Handle_PendingToUploading_PublishesEventWithDocumentData()
    {
        var pdfId = Guid.NewGuid();
        var document = SeedDocument(pdfId);
        PdfUploadedIntegrationEvent? published = null;
        _publisher.Setup(p => p.PublishAsync(It.IsAny<PdfUploadedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<PdfUploadedIntegrationEvent, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        var outcomes = await CollectOutcomesAsync(() => _sut.Handle(PendingToUploading(pdfId), CancellationToken.None));

        published.Should().NotBeNull();
        published!.PdfDocumentId.Should().Be(pdfId);
        published.GameId.Should().Be(document.GameId);
        published.FilePath.Should().Be(document.FilePath);
        published.ContractVersion.Should().Be("v1");
        outcomes.Should().Equal(MeepleAiMetrics.PdfUploadedEventOutcomes.Published);
    }

    [Fact]
    public async Task Handle_OtherTransition_DoesNotPublish()
    {
        await _sut.Handle(new PdfStateChangedEvent(Guid.NewGuid(), PdfProcessingState.Uploading, PdfProcessingState.Extracting, Guid.NewGuid()), CancellationToken.None);

        _publisher.Verify(p => p.PublishAsync(It.IsAny<PdfUploadedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DocumentMissing_DoesNotPublishAndCountsNotFound()
    {
        var pdfId = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>())).ReturnsAsync((PdfDocument?)null);

        var outcomes = await CollectOutcomesAsync(() => _sut.Handle(PendingToUploading(pdfId), CancellationToken.None));

        _publisher.Verify(p => p.PublishAsync(It.IsAny<PdfUploadedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        outcomes.Should().Equal(MeepleAiMetrics.PdfUploadedEventOutcomes.NotFound);
    }

    [Fact]
    public async Task Handle_PublisherThrows_SwallowsAndCountsFailure()
    {
        var pdfId = Guid.NewGuid();
        SeedDocument(pdfId);
        _publisher.Setup(p => p.PublishAsync(It.IsAny<PdfUploadedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bus down"));

        // L'upload NON deve fallire perché il bus è giù (Review Focus 5): il relay assorbe e conta.
        var act = () => CollectOutcomesAsync(() => _sut.Handle(PendingToUploading(pdfId), CancellationToken.None));

        var outcomes = await act.Should().NotThrowAsync();
        outcomes.Subject.Should().Equal(MeepleAiMetrics.PdfUploadedEventOutcomes.PublishFailed);
    }
}

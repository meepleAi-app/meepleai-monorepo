using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.SharedKernel.Application.IntegrationEvents;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.EventHandlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class VectorDocumentReadyNotificationHandlerTests
{
    private readonly Mock<INotificationDispatcher> _dispatcher;
    private readonly Mock<ILogger<VectorDocumentReadyNotificationHandler>> _logger;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _documentId = Guid.NewGuid();
    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private const string TestFileName = "twilight-imperium-rulebook.pdf";
    private const int TestChunkCount = 247;

    public VectorDocumentReadyNotificationHandlerTests()
    {
        _dispatcher = new Mock<INotificationDispatcher>();
        _logger = new Mock<ILogger<VectorDocumentReadyNotificationHandler>>();
    }

    private VectorDocumentReadyNotificationHandler CreateHandler() =>
        new(_dispatcher.Object, _logger.Object);

    private VectorDocumentReadyIntegrationEvent CreateEvent() =>
        new()
        {
            DocumentId = _documentId,
            GameId = _gameId,
            ChunkCount = TestChunkCount,
            PdfDocumentId = _pdfDocumentId,
            UploadedByUserId = _userId,
            FileName = TestFileName,
            CurrentProcessingState = "Processing"
        };

    [Fact]
    public async Task Handle_DispatchesNotificationWithCorrectProperties()
    {
        var sut = CreateHandler();
        var evt = CreateEvent();

        await sut.Handle(evt, CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m =>
                m.Type == NotificationType.ProcessingJobCompleted &&
                m.RecipientUserId == _userId &&
                m.DeepLinkPath == $"/library/games/{_gameId}/agent" &&
                m.Payload is PdfProcessingPayload),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DispatcherFailure_Propagates()
    {
        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("dispatch error"));

        var sut = CreateHandler();
        var evt = CreateEvent();

        var act = async () => await sut.Handle(evt, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

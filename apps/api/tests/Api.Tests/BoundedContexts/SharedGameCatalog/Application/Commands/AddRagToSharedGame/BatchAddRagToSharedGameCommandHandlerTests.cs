using Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Tests.Constants;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

[Trait("Category", TestCategories.Unit)]
public class BatchAddRagToSharedGameCommandHandlerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<BatchAddRagToSharedGameCommandHandler>> _loggerMock;
    private readonly BatchAddRagToSharedGameCommandHandler _handler;

    public BatchAddRagToSharedGameCommandHandlerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<BatchAddRagToSharedGameCommandHandler>>();
        _handler = new BatchAddRagToSharedGameCommandHandler(
            _mediatorMock.Object,
            _loggerMock.Object);
    }

    private static AddRagToSharedGameCommand CreateItem(Guid? sharedGameId = null, string fileName = "test.pdf")
    {
        var file = new FormFile(
            baseStream: new MemoryStream(new byte[1024]),
            baseStreamOffset: 0,
            length: 1024,
            name: "file",
            fileName: fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        return new AddRagToSharedGameCommand(
            SharedGameId: sharedGameId ?? Guid.NewGuid(),
            File: file,
            DocumentType: SharedGameDocumentType.Rulebook,
            Version: "1.0",
            Tags: null,
            UserId: Guid.NewGuid(),
            IsAdmin: true);
    }

    private static AddRagToSharedGameResult CreateSuccessResult()
    {
        return new AddRagToSharedGameResult(
            PdfDocumentId: Guid.NewGuid(),
            SharedGameDocumentId: Guid.NewGuid(),
            ProcessingJobId: Guid.NewGuid(),
            AutoApproved: true,
            StreamUrl: "/api/v1/pdf/test/status/stream");
    }

    [Fact]
    public async Task Handle_AllItemsSucceed_ReturnsFullSuccess()
    {
        // Arrange
        var items = new List<AddRagToSharedGameCommand>
        {
            CreateItem(fileName: "rulebook1.pdf"),
            CreateItem(fileName: "rulebook2.pdf"),
            CreateItem(fileName: "rulebook3.pdf")
        };
        var command = new BatchAddRagToSharedGameCommand(items);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddRagToSharedGameCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(3, result.Results.Count);
        Assert.All(result.Results, r =>
        {
            Assert.NotNull(r.Result);
            Assert.Null(r.Error);
        });
    }

    [Fact]
    public async Task Handle_OneFailsOneSucceeds_ReturnsPartialSuccess()
    {
        // Arrange
        var successId = Guid.NewGuid();
        var failId = Guid.NewGuid();

        var items = new List<AddRagToSharedGameCommand>
        {
            CreateItem(sharedGameId: successId, fileName: "good.pdf"),
            CreateItem(sharedGameId: failId, fileName: "bad.pdf")
        };
        var command = new BatchAddRagToSharedGameCommand(items);

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<AddRagToSharedGameCommand>(c => c.SharedGameId == successId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult());

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<AddRagToSharedGameCommand>(c => c.SharedGameId == failId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("PDF upload failed"));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.Equal(2, result.Results.Count);

        var successItem = result.Results.Single(r => r.SharedGameId == successId);
        Assert.NotNull(successItem.Result);
        Assert.Null(successItem.Error);

        var failItem = result.Results.Single(r => r.SharedGameId == failId);
        Assert.Null(failItem.Result);
        Assert.Contains("PDF upload failed", failItem.Error);
    }

    [Fact]
    public async Task Handle_EmptyList_ReturnsZeroCounts()
    {
        // Arrange
        var command = new BatchAddRagToSharedGameCommand(new List<AddRagToSharedGameCommand>());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Empty(result.Results);

        _mediatorMock.Verify(
            m => m.Send(It.IsAny<AddRagToSharedGameCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AllItemsFail_ReturnsFullFailure()
    {
        // Arrange
        var items = new List<AddRagToSharedGameCommand>
        {
            CreateItem(fileName: "bad1.pdf"),
            CreateItem(fileName: "bad2.pdf")
        };
        var command = new BatchAddRagToSharedGameCommand(items);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddRagToSharedGameCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Upload service unavailable"));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(2, result.FailureCount);
        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, r =>
        {
            Assert.Null(r.Result);
            Assert.NotNull(r.Error);
            Assert.Contains("Upload service unavailable", r.Error);
        });
    }
}

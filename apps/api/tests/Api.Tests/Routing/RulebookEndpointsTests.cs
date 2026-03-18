using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Api.Tests.Routing;

[Trait("Category", TestCategories.Unit)]
public sealed class RulebookEndpointsTests
{
    private readonly Mock<IMediator> _mediatorMock;

    public RulebookEndpointsTests()
    {
        _mediatorMock = new Mock<IMediator>();
    }

    [Fact]
    public async Task AddRulebook_NewPdf_ReturnsOkWithResult()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var expectedResult = new RulebookUploadResult(
            pdfDocumentId,
            IsNew: true,
            Status: "pending",
            Message: "Rulebook uploaded successfully and queued for processing.");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddRulebookCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var command = new AddRulebookCommand(gameId, userId, Mock.Of<IFormFile>());

        // Act
        var result = await _mediatorMock.Object.Send(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.PdfDocumentId.Should().Be(pdfDocumentId);
        result.IsNew.Should().BeTrue();
        result.Status.Should().Be("pending");
        result.Message.Should().NotBeNullOrEmpty();

        _mediatorMock.Verify(
            m => m.Send(It.IsAny<AddRulebookCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddRulebook_DuplicateHash_ReturnsOkWithReuse()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingPdfDocumentId = Guid.NewGuid();
        var expectedResult = new RulebookUploadResult(
            existingPdfDocumentId,
            IsNew: false,
            Status: "ready",
            Message: "Rulebook already exists and has been reused.");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddRulebookCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var command = new AddRulebookCommand(gameId, userId, Mock.Of<IFormFile>());

        // Act
        var result = await _mediatorMock.Object.Send(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.PdfDocumentId.Should().Be(existingPdfDocumentId);
        result.IsNew.Should().BeFalse();
        result.Status.Should().Be("ready");
        result.Message.Should().NotBeNullOrEmpty();

        _mediatorMock.Verify(
            m => m.Send(It.IsAny<AddRulebookCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetGamesWithKb_ReturnsGamesList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedGames = new List<GameWithKbDto>
        {
            new(Guid.NewGuid(), "Catan", PdfCount: 2, LatestPdfStatus: "Indexed"),
            new(Guid.NewGuid(), "Ticket to Ride", PdfCount: 1, LatestPdfStatus: "Indexed"),
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetGamesWithKbQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedGames);

        var query = new GetGamesWithKbQuery(userId);

        // Act
        var result = await _mediatorMock.Object.Send(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].GameName.Should().Be("Catan");
        result[0].PdfCount.Should().Be(2);
        result[1].GameName.Should().Be("Ticket to Ride");
        result[1].PdfCount.Should().Be(1);

        _mediatorMock.Verify(
            m => m.Send(It.IsAny<GetGamesWithKbQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetGamesWithKb_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedGames = new List<GameWithKbDto>();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetGamesWithKbQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedGames);

        var query = new GetGamesWithKbQuery(userId);

        // Act
        var result = await _mediatorMock.Object.Send(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        _mediatorMock.Verify(
            m => m.Send(It.IsAny<GetGamesWithKbQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// #4413, the path the integration tests cannot reach without R2: a ready PDF of the game sends the
/// chosen page to the renderer, under a key of its own.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4413")]
public sealed class SetPdfCoverFromPageCommandHandlerTests
{
    [Fact]
    public async Task AReadyPdfOfTheGame_IsRenderedAtTheChosenPage()
    {
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = gameId,
            FileName = "rules.pdf",
            FilePath = "/uploads/x_rules.pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready",
            PageCount = 12,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        MaterializePdfCoverCommand? sent = null;
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<MaterializePdfCoverCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<string>, CancellationToken>((c, _) => sent = (MaterializePdfCoverCommand)c)
            .ReturnsAsync((IRequest<string> c, CancellationToken _) => ((MaterializePdfCoverCommand)c).DbKey);
        var handler = new SetPdfCoverFromPageCommandHandler(
            db, mediator.Object, NullLogger<SetPdfCoverFromPageCommandHandler>.Instance);

        var first = await handler.Handle(
            new SetPdfCoverFromPageCommand(gameId, pdfId, 3, Guid.NewGuid()), TestContext.Current.CancellationToken);
        var second = await handler.Handle(
            new SetPdfCoverFromPageCommand(gameId, pdfId, 5, Guid.NewGuid()), TestContext.Current.CancellationToken);

        sent.Should().NotBeNull();
        sent!.PdfDocumentId.Should().Be(pdfId);
        sent.PageNumber.Should().Be(5);
        first.Should().StartWith($"covers/{gameId:D}/pdf-cover-");
        second.Should().NotBe(first, "a later choice must not overwrite the image an earlier one points to");
    }
}

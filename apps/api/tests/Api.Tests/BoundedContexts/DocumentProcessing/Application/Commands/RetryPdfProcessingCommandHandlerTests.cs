using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Unit tests for RetryPdfProcessingCommandHandler.
/// Issue #5189: Added IsAdmin flag; aligned error handling to NotFoundException/ForbiddenException.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class RetryPdfProcessingCommandHandlerTests
{
    private static readonly Guid _ownerId = Guid.NewGuid();
    private static readonly Guid _otherId = Guid.NewGuid();

    private readonly Mock<IPdfDocumentRepository> _repoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly RetryPdfProcessingCommandHandler _handler;

    public RetryPdfProcessingCommandHandlerTests()
    {
        _repoMock = new Mock<IPdfDocumentRepository>();
        _uowMock = new Mock<IUnitOfWork>();
        _mediatorMock = new Mock<IMediator>();
        _handler = new RetryPdfProcessingCommandHandler(
            _repoMock.Object,
            _uowMock.Object,
            _mediatorMock.Object,
            new Mock<ILogger<RetryPdfProcessingCommandHandler>>().Object);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument BuildFailedPdf(Guid pdfId, Guid ownerId)
    {
        return new PdfDocumentBuilder()
            .WithId(pdfId)
            .WithUploadedBy(ownerId)
            .ThatIsFailed()
            .Build();
    }

    // ── Happy paths ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OwnerRetryOwnPdf_ReturnsSuccess()
    {
        var pdfId = Guid.NewGuid();
        var pdf = BuildFailedPdf(pdfId, _ownerId);

        _repoMock.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(pdf);

        var command = new RetryPdfProcessingCommand(PdfId: pdfId, UserId: _ownerId);
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Contains("Retry", result.Message);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AdminRetryAnotherUsersPdf_ReturnsSuccess()
    {
        var pdfId = Guid.NewGuid();
        // PDF owned by _ownerId, retried by _otherId with IsAdmin=true
        var pdf = BuildFailedPdf(pdfId, _ownerId);

        _repoMock.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(pdf);

        var command = new RetryPdfProcessingCommand(PdfId: pdfId, UserId: _otherId, IsAdmin: true);
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Authorization errors ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NonOwnerNonAdmin_ThrowsForbiddenException()
    {
        var pdfId = Guid.NewGuid();
        var pdf = BuildFailedPdf(pdfId, _ownerId);

        _repoMock.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(pdf);

        var command = new RetryPdfProcessingCommand(PdfId: pdfId, UserId: _otherId, IsAdmin: false);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    // ── Not-found error ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PdfNotFound_ThrowsNotFoundException()
    {
        var pdfId = Guid.NewGuid();

        _repoMock.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument?)null);

        var command = new RetryPdfProcessingCommand(PdfId: pdfId, UserId: _ownerId);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains(pdfId.ToString(), ex.Message);
    }

    // ── Domain validation failure ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_MaxRetriesReached_ReturnsSuccessFalse()
    {
        var pdfId = Guid.NewGuid();
        // Build a PDF and exhaust retries (MaxRetries = 3)
        var pdf = BuildFailedPdf(pdfId, _ownerId);
        pdf.Retry(); // retry 1 → Extracting
        pdf.TransitionTo(PdfProcessingState.Failed); // fail again
        pdf.Retry(); // retry 2 → Extracting
        pdf.TransitionTo(PdfProcessingState.Failed); // fail again
        pdf.Retry(); // retry 3 → Extracting
        pdf.TransitionTo(PdfProcessingState.Failed); // fail again — RetryCount=3, no more retries

        _repoMock.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(pdf);

        var command = new RetryPdfProcessingCommand(PdfId: pdfId, UserId: _ownerId);
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("Cannot retry", result.Message);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Null guard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    // ── Constructor null guards ────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullRepository_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new RetryPdfProcessingCommandHandler(
                null!,
                _uowMock.Object,
                _mediatorMock.Object,
                new Mock<ILogger<RetryPdfProcessingCommandHandler>>().Object));
        Assert.Equal("pdfRepository", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullUnitOfWork_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new RetryPdfProcessingCommandHandler(
                _repoMock.Object,
                null!,
                _mediatorMock.Object,
                new Mock<ILogger<RetryPdfProcessingCommandHandler>>().Object));
        Assert.Equal("unitOfWork", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullMediator_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new RetryPdfProcessingCommandHandler(
                _repoMock.Object,
                _uowMock.Object,
                null!,
                new Mock<ILogger<RetryPdfProcessingCommandHandler>>().Object));
        Assert.Equal("mediator", ex.ParamName);
    }
}

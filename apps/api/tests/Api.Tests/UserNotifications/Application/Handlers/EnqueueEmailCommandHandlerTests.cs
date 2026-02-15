using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Handlers;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.UserNotifications.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public sealed class EnqueueEmailCommandHandlerTests
{
    private readonly Mock<IEmailQueueRepository> _emailQueueRepo;
    private readonly Mock<IEmailTemplateService> _templateService;
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<ILogger<EnqueueEmailCommandHandler>> _logger;
    private readonly EnqueueEmailCommandHandler _sut;

    public EnqueueEmailCommandHandlerTests()
    {
        _emailQueueRepo = new Mock<IEmailQueueRepository>();
        _templateService = new Mock<IEmailTemplateService>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _logger = new Mock<ILogger<EnqueueEmailCommandHandler>>();

        _sut = new EnqueueEmailCommandHandler(
            _emailQueueRepo.Object,
            _templateService.Object,
            _unitOfWork.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Handle_DocumentReady_RendersTemplateAndEnqueues()
    {
        _templateService.Setup(t => t.RenderDocumentReady(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("<html>ready</html>");

        var command = new EnqueueEmailCommand(
            UserId: Guid.NewGuid(),
            To: "user@test.com",
            Subject: "PDF Ready",
            TemplateName: "document_ready",
            UserName: "Test User",
            FileName: "rules.pdf",
            DocumentUrl: "/documents/123");

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        _templateService.Verify(t => t.RenderDocumentReady("Test User", "rules.pdf", "/documents/123"), Times.Once);
        _emailQueueRepo.Verify(r => r.AddAsync(It.Is<EmailQueueItem>(e =>
            e.To == "user@test.com" &&
            e.Subject == "PDF Ready" &&
            e.HtmlBody == "<html>ready</html>"), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DocumentFailed_RendersFailureTemplate()
    {
        _templateService.Setup(t => t.RenderDocumentFailed(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("<html>failed</html>");

        var command = new EnqueueEmailCommand(
            UserId: Guid.NewGuid(),
            To: "user@test.com",
            Subject: "PDF Failed",
            TemplateName: "document_failed",
            UserName: "Test User",
            FileName: "rules.pdf",
            ErrorMessage: "OCR failed");

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        _templateService.Verify(t => t.RenderDocumentFailed("Test User", "rules.pdf", "OCR failed"), Times.Once);
    }

    [Fact]
    public async Task Handle_RetryAvailable_RendersRetryTemplate()
    {
        _templateService.Setup(t => t.RenderRetryAvailable(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns("<html>retry</html>");

        var command = new EnqueueEmailCommand(
            UserId: Guid.NewGuid(),
            To: "user@test.com",
            Subject: "PDF Retry",
            TemplateName: "retry_available",
            UserName: "Test User",
            FileName: "rules.pdf",
            RetryCount: 2);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        _templateService.Verify(t => t.RenderRetryAvailable("Test User", "rules.pdf", 2), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownTemplate_ThrowsArgumentException()
    {
        var command = new EnqueueEmailCommand(
            UserId: Guid.NewGuid(),
            To: "user@test.com",
            Subject: "Test",
            TemplateName: "unknown_template",
            UserName: "User",
            FileName: "file.pdf");

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unknown email template*");
    }
}

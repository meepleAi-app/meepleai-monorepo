using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.EventHandlers;

[Trait("Category", TestCategories.Unit)]
public sealed class ProcessingJobNotificationEventHandlerTests
{
    private readonly Mock<INotificationDispatcher> _dispatcher;
    private readonly Mock<IPdfDocumentRepository> _pdfRepo;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<ILogger<ProcessingJobNotificationEventHandler>> _logger;
    private readonly ProcessingJobNotificationEventHandler _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _jobId = Guid.NewGuid();
    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private const string TestFileName = "rules-catan.pdf";

    public ProcessingJobNotificationEventHandlerTests()
    {
        _dispatcher = new Mock<INotificationDispatcher>();
        _pdfRepo = new Mock<IPdfDocumentRepository>();
        _userRepo = new Mock<IUserRepository>();
        _logger = new Mock<ILogger<ProcessingJobNotificationEventHandler>>();

        _sut = new ProcessingJobNotificationEventHandler(_dispatcher.Object, _pdfRepo.Object, _userRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_JobCompleted_DispatchesNotification()
    {
        var pdfDoc = new PdfDocumentBuilder().WithId(_pdfDocumentId).WithFileName(TestFileName).Build();
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>())).ReturnsAsync(pdfDoc);

        var evt = new JobCompletedEvent(_jobId, _pdfDocumentId, _userId, TimeSpan.FromMinutes(2));

        await _sut.Handle(evt, CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m =>
                m.Type == NotificationType.ProcessingJobCompleted &&
                m.RecipientUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_JobCompleted_PdfNotFound_DoesNotDispatch()
    {
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>())).ReturnsAsync((PdfDocument?)null);

        var evt = new JobCompletedEvent(_jobId, _pdfDocumentId, _userId, TimeSpan.FromMinutes(1));

        await _sut.Handle(evt, CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_JobFailed_DispatchesFailedNotification()
    {
        var pdfDoc = new PdfDocumentBuilder().WithId(_pdfDocumentId).WithFileName(TestFileName).Build();
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>())).ReturnsAsync(pdfDoc);
        _userRepo.Setup(r => r.GetAdminUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());

        var evt = new JobFailedEvent(_jobId, _pdfDocumentId, _userId, "Out of memory", ProcessingStepType.Extract, 0);

        await _sut.Handle(evt, CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m =>
                m.Type == NotificationType.ProcessingJobFailed &&
                m.RecipientUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_JobFailed_NotifiesAdminUsers()
    {
        var pdfDoc = new PdfDocumentBuilder().WithId(_pdfDocumentId).WithFileName(TestFileName).Build();
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>())).ReturnsAsync(pdfDoc);

        var admin1Id = Guid.NewGuid();
        var admin2Id = Guid.NewGuid();
        var admins = new List<User>
        {
            new UserBuilder().WithId(admin1Id).AsAdmin().WithEmail("admin1@meepleai.dev").Build(),
            new UserBuilder().WithId(admin2Id).AsAdmin().WithEmail("admin2@meepleai.dev").Build(),
        };
        _userRepo.Setup(r => r.GetAdminUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(admins);

        var evt = new JobFailedEvent(_jobId, _pdfDocumentId, _userId, "Connection timeout", null, 0);

        await _sut.Handle(evt, CancellationToken.None);

        // 1 for uploader + 2 for admins = 3 dispatches
        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_JobFailed_AdminIsUploader_SkipsDuplicate()
    {
        var pdfDoc = new PdfDocumentBuilder().WithId(_pdfDocumentId).WithFileName(TestFileName).Build();
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>())).ReturnsAsync(pdfDoc);

        var admins = new List<User>
        {
            new UserBuilder().WithId(_userId).AsAdmin().WithEmail("uploader-admin@meepleai.dev").Build(),
        };
        _userRepo.Setup(r => r.GetAdminUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(admins);

        var evt = new JobFailedEvent(_jobId, _pdfDocumentId, _userId, "Parse error", ProcessingStepType.Chunk, 1);

        await _sut.Handle(evt, CancellationToken.None);

        // Only 1 dispatch for uploader (admin duplicate skipped)
        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

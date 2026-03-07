using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.BoundedContexts.GameManagement.Infrastructure.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure.Services;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class SessionAttachmentCleanupJobTests
{
    private readonly Mock<ISessionAttachmentRepository> _repoMock = new();
    private readonly Mock<ISessionAttachmentService> _serviceMock = new();
    private readonly Mock<ILogger<SessionAttachmentCleanupJob>> _loggerMock = new();

    [Fact]
    public async Task RunCleanupAsync_WithExpiredAttachments_DeletesAndSoftDeletes()
    {
        var sessionId = Guid.NewGuid();
        var attachments = new List<SessionAttachment>
        {
            CreateAttachment(sessionId),
            CreateAttachment(sessionId),
        };

        _repoMock.SetupSequence(x => x.GetExpiredAttachmentsAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachments)
            .ReturnsAsync(new List<SessionAttachment>());

        _repoMock.Setup(x => x.SoftDeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var job = CreateJob();
        await job.RunCleanupAsync(CancellationToken.None);

        _serviceMock.Verify(
            x => x.DeleteBlobsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _repoMock.Verify(
            x => x.SoftDeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _repoMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunCleanupAsync_WithNoExpiredAttachments_DoesNothing()
    {
        _repoMock.Setup(x => x.GetExpiredAttachmentsAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionAttachment>());

        var job = CreateJob();
        await job.RunCleanupAsync(CancellationToken.None);

        _serviceMock.Verify(
            x => x.DeleteBlobsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repoMock.Verify(
            x => x.SoftDeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunCleanupAsync_WhenS3Fails_ContinuesWithSoftDelete()
    {
        var attachment = CreateAttachment(Guid.NewGuid());

        _repoMock.SetupSequence(x => x.GetExpiredAttachmentsAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionAttachment> { attachment })
            .ReturnsAsync(new List<SessionAttachment>());

        _serviceMock.Setup(x => x.DeleteBlobsAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("S3 error"));

        _repoMock.Setup(x => x.SoftDeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var job = CreateJob();
        await job.RunCleanupAsync(CancellationToken.None);

        // Should still soft-delete even though S3 failed
        _repoMock.Verify(
            x => x.SoftDeleteAsync(attachment.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        _repoMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunCleanupAsync_WithMultipleBatches_ProcessesAll()
    {
        var batch1 = new List<SessionAttachment> { CreateAttachment(Guid.NewGuid()) };
        var batch2 = new List<SessionAttachment> { CreateAttachment(Guid.NewGuid()) };

        _repoMock.SetupSequence(x => x.GetExpiredAttachmentsAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch1)
            .ReturnsAsync(batch2)
            .ReturnsAsync(new List<SessionAttachment>());

        _repoMock.Setup(x => x.SoftDeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var job = CreateJob();
        await job.RunCleanupAsync(CancellationToken.None);

        _repoMock.Verify(
            x => x.SoftDeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        // SaveChanges once per batch scope
        _repoMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullScopeFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SessionAttachmentCleanupJob(
                null!,
                CreateConfiguration(),
                _loggerMock.Object));
    }

    [Fact]
    public void Constructor_NullConfiguration_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SessionAttachmentCleanupJob(
                CreateScopeFactory(),
                null!,
                _loggerMock.Object));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SessionAttachmentCleanupJob(
                CreateScopeFactory(),
                CreateConfiguration(),
                null!));
    }

    #endregion

    #region Helpers

    private SessionAttachmentCleanupJob CreateJob(int retentionDays = 90, int batchSize = 100)
    {
        return new SessionAttachmentCleanupJob(
            CreateScopeFactory(),
            CreateConfiguration(retentionDays, batchSize),
            _loggerMock.Object);
    }

    private IServiceScopeFactory CreateScopeFactory()
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(ISessionAttachmentRepository)))
            .Returns(_repoMock.Object);
        serviceProvider.Setup(x => x.GetService(typeof(ISessionAttachmentService)))
            .Returns(_serviceMock.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);
        return scopeFactory.Object;
    }

    private static IConfiguration CreateConfiguration(int retentionDays = 90, int batchSize = 100)
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["SessionAttachments:RetentionDays"] = retentionDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["SessionAttachments:CleanupBatchSize"] = batchSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
    }

    private static SessionAttachment CreateAttachment(Guid sessionId)
    {
        return SessionAttachment.Create(
            sessionId,
            Guid.NewGuid(),
            AttachmentType.BoardState,
            "https://storage.example.com/photo.jpg",
            "image/jpeg",
            5000,
            "https://storage.example.com/thumb.jpg");
    }

    #endregion
}

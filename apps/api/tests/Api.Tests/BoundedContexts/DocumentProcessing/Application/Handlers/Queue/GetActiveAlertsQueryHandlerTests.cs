using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

/// <summary>
/// Tests for GetActiveAlertsQueryHandler.
/// Issue #5460: Proactive alerts — stuck docs, queue depth, failure rate.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class GetActiveAlertsQueryHandlerTests
{
    private static IConfiguration CreateConfig(Dictionary<string, string?>? overrides = null)
    {
        var dict = new Dictionary<string, string?>
        {
            ["ProcessingQueueMonitor:StuckJobTimeoutMinutes"] = "10",
            ["ProcessingQueueMonitor:QueueDepthThreshold"] = "20",
            ["ProcessingQueueMonitor:FailureRateThresholdPercent"] = "15",
        };

        if (overrides != null)
        {
            foreach (var kvp in overrides)
            {
                dict[kvp.Key] = kvp.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = new GetActiveAlertsQueryHandler(db, CreateConfig());

        // Act
        var result = await handler.Handle(new GetActiveAlertsQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_StuckJob_DetectsProcessingOverTimeout()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        // Add the PdfDocumentEntity (navigation property target)
        db.Set<PdfDocumentEntity>().Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "stuck-rules.pdf",
            FilePath = "/test/stuck-rules.pdf",
            FileSizeBytes = 2048,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            ProcessingStatus = "processing",
        });

        // Add a job stuck for 15 minutes (exceeds 10-minute default)
        db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            UserId = userId,
            Status = "Processing",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-15),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20),
        });

        await db.SaveChangesAsync();

        var handler = new GetActiveAlertsQueryHandler(db, CreateConfig());

        // Act
        var result = await handler.Handle(new GetActiveAlertsQuery(), CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        var alert = result[0];
        alert.Type.Should().Be(QueueAlertType.DocumentStuck);
        alert.Severity.Should().Be(QueueAlertSeverity.Warning);
        alert.Message.Should().Contain("stuck-rules.pdf");
        alert.Data.Should().BeOfType<StuckDocumentAlertData>();
    }

    [Fact]
    public async Task Handle_NonStuckJob_NotFlagged()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        db.Set<PdfDocumentEntity>().Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "recent.pdf",
            FilePath = "/test/recent.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingStatus = "processing",
        });

        // Job has been processing for only 5 minutes (under 10-minute threshold)
        db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            UserId = userId,
            Status = "Processing",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-6),
        });

        await db.SaveChangesAsync();

        var handler = new GetActiveAlertsQueryHandler(db, CreateConfig());

        // Act
        var result = await handler.Handle(new GetActiveAlertsQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_QueueDepthExceedsThreshold_TriggersAlert()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        // Add 25 queued jobs (exceeds default threshold of 20)
        for (var i = 0; i < 25; i++)
        {
            var pdfId = Guid.NewGuid();

            db.Set<PdfDocumentEntity>().Add(new PdfDocumentEntity
            {
                Id = pdfId,
                FileName = $"queued-{i}.pdf",
                FilePath = $"/test/queued-{i}.pdf",
                FileSizeBytes = 1024,
                UploadedByUserId = userId,
                UploadedAt = DateTime.UtcNow,
                ProcessingStatus = "pending",
            });

            db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                UserId = userId,
                Status = "Queued",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync();

        var handler = new GetActiveAlertsQueryHandler(db, CreateConfig());

        // Act
        var result = await handler.Handle(new GetActiveAlertsQuery(), CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        var alert = result[0];
        alert.Type.Should().Be(QueueAlertType.QueueDepthHigh);
        alert.Severity.Should().Be(QueueAlertSeverity.Warning);
        alert.Data.Should().BeOfType<QueueDepthAlertData>();

        var data = (QueueDepthAlertData)alert.Data!;
        data.CurrentDepth.Should().Be(25);
        data.Threshold.Should().Be(20);
    }

    [Fact]
    public async Task Handle_QueueDepthBelowThreshold_NoAlert()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        // Add 10 queued jobs (below default threshold of 20)
        for (var i = 0; i < 10; i++)
        {
            var pdfId = Guid.NewGuid();

            db.Set<PdfDocumentEntity>().Add(new PdfDocumentEntity
            {
                Id = pdfId,
                FileName = $"queued-{i}.pdf",
                FilePath = $"/test/queued-{i}.pdf",
                FileSizeBytes = 1024,
                UploadedByUserId = userId,
                UploadedAt = DateTime.UtcNow,
                ProcessingStatus = "pending",
            });

            db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                UserId = userId,
                Status = "Queued",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync();

        var handler = new GetActiveAlertsQueryHandler(db, CreateConfig());

        // Act
        var result = await handler.Handle(new GetActiveAlertsQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_HighFailureRate_TriggersAlert()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        // 2 completed + 3 failed in last hour = 60% failure rate (exceeds 15% threshold)
        for (var i = 0; i < 2; i++)
        {
            var pdfId = Guid.NewGuid();
            db.Set<PdfDocumentEntity>().Add(CreatePdfDocument(pdfId, userId));
            db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                UserId = userId,
                Status = "Completed",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            });
        }

        for (var i = 0; i < 3; i++)
        {
            var pdfId = Guid.NewGuid();
            db.Set<PdfDocumentEntity>().Add(CreatePdfDocument(pdfId, userId));
            db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                UserId = userId,
                Status = "Failed",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                ErrorMessage = "Test failure",
            });
        }

        await db.SaveChangesAsync();

        var handler = new GetActiveAlertsQueryHandler(db, CreateConfig());

        // Act
        var result = await handler.Handle(new GetActiveAlertsQuery(), CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        var alert = result[0];
        alert.Type.Should().Be(QueueAlertType.HighFailureRate);
        alert.Severity.Should().Be(QueueAlertSeverity.Critical); // 60% > 30% → Critical
        alert.Data.Should().BeOfType<HighFailureRateAlertData>();

        var data = (HighFailureRateAlertData)alert.Data!;
        data.FailureRatePercent.Should().Be(60.0);
        data.FailedCount.Should().Be(3);
        data.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task Handle_LowFailureRate_NoAlert()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        // 9 completed + 1 failed = 10% failure rate (below 15% threshold)
        for (var i = 0; i < 9; i++)
        {
            var pdfId = Guid.NewGuid();
            db.Set<PdfDocumentEntity>().Add(CreatePdfDocument(pdfId, userId));
            db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                UserId = userId,
                Status = "Completed",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            });
        }

        {
            var pdfId = Guid.NewGuid();
            db.Set<PdfDocumentEntity>().Add(CreatePdfDocument(pdfId, userId));
            db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                UserId = userId,
                Status = "Failed",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                ErrorMessage = "Test failure",
            });
        }

        await db.SaveChangesAsync();

        var handler = new GetActiveAlertsQueryHandler(db, CreateConfig());

        // Act
        var result = await handler.Handle(new GetActiveAlertsQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MultipleAlerts_FireSimultaneously()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        // Alert 1: Stuck job (processing > 10 minutes)
        var stuckPdfId = Guid.NewGuid();
        db.Set<PdfDocumentEntity>().Add(new PdfDocumentEntity
        {
            Id = stuckPdfId,
            FileName = "stuck.pdf",
            FilePath = "/test/stuck.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            ProcessingStatus = "processing",
        });
        db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = stuckPdfId,
            UserId = userId,
            Status = "Processing",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-20),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-25),
        });

        // Alert 2: Queue depth > 20
        for (var i = 0; i < 25; i++)
        {
            var pdfId = Guid.NewGuid();
            db.Set<PdfDocumentEntity>().Add(CreatePdfDocument(pdfId, userId));
            db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                UserId = userId,
                Status = "Queued",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        // Alert 3: High failure rate (all failed in last hour)
        for (var i = 0; i < 4; i++)
        {
            var pdfId = Guid.NewGuid();
            db.Set<PdfDocumentEntity>().Add(CreatePdfDocument(pdfId, userId));
            db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                UserId = userId,
                Status = "Failed",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                ErrorMessage = "Test failure",
            });
        }

        await db.SaveChangesAsync();

        var handler = new GetActiveAlertsQueryHandler(db, CreateConfig());

        // Act
        var result = await handler.Handle(new GetActiveAlertsQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result.Select(a => a.Type).Should().Contain(QueueAlertType.DocumentStuck);
        result.Select(a => a.Type).Should().Contain(QueueAlertType.QueueDepthHigh);
        result.Select(a => a.Type).Should().Contain(QueueAlertType.HighFailureRate);
    }

    #region Helper Methods

    private static PdfDocumentEntity CreatePdfDocument(Guid id, Guid userId)
    {
        return new PdfDocumentEntity
        {
            Id = id,
            FileName = $"test-{id:N}.pdf",
            FilePath = $"/test/test-{id:N}.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingStatus = "pending",
        };
    }

    #endregion
}

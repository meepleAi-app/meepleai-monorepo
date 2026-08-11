using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure.BackgroundTasks;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Unit tests for <see cref="AnalyzeRulebookCommandHandler"/>.
/// Issue #3163 — a concurrent analysis (distributed lock not acquired) must map to
/// HTTP 409 (ConflictException), not HTTP 500 (InvalidOperationException).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AnalyzeRulebookCommandHandlerTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_WhenBackgroundLockNotAcquired_ThrowsConflictException()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var gameRepo = new Mock<ISharedGameRepository>();
        var game = SharedGame.CreateSkeleton("Test Game", userId, TimeProvider.System);
        gameRepo
            .Setup(r => r.GetByIdAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var analysisRepo = new Mock<IRulebookAnalysisRepository>();
        analysisRepo
            .Setup(r => r.GetPdfDocumentCategoryAsync(pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        // Long, OCR-heavy text (single-char words) → complexity score > 0.4 threshold
        // → RoutingDecision.Background, which is the branch that acquires the distributed lock.
        var complexContent = string.Concat(Enumerable.Repeat("x y z w ", 20000));
        analysisRepo
            .Setup(r => r.GetPdfTextAsync(pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(complexContent);

        var taskOrchestrator = new Mock<IBackgroundTaskOrchestrator>();
        // Lock already held by a concurrent analysis → ExecuteWithDistributedLockAsync returns false.
        taskOrchestrator
            .Setup(o => o.ExecuteWithDistributedLockAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new AnalyzeRulebookCommandHandler(
            gameRepo.Object,
            analysisRepo.Object,
            Mock.Of<IRulebookAnalyzer>(),
            Mock.Of<IBackgroundRulebookAnalysisOrchestrator>(),
            taskOrchestrator.Object,
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IHybridCacheService>(),
            Options.Create(new BackgroundAnalysisOptions()),
            Mock.Of<ILogger<AnalyzeRulebookCommandHandler>>());

        var command = new AnalyzeRulebookCommand(pdfDocumentId, sharedGameId, userId);

        // Act & Assert — #3163: a concurrent analysis is a conflict (409), not a server error (500).
        await FluentActions
            .Awaiting(() => handler.Handle(command, TestCancellationToken))
            .Should()
            .ThrowAsync<ConflictException>();
    }
}

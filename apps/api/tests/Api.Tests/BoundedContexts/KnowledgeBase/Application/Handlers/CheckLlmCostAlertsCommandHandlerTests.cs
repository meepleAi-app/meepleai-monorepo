using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for CheckLlmCostAlertsCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CheckLlmCostAlertsCommandHandlerTests
{
    private readonly LlmCostAlertService _alertService;
    private readonly CheckLlmCostAlertsCommandHandler _handler;
    private readonly Mock<ILlmCostLogRepository> _mockCostLogRepository;
    private readonly Mock<IAlertingService> _mockAlertingService;

    public CheckLlmCostAlertsCommandHandlerTests()
    {
        _mockCostLogRepository = new Mock<ILlmCostLogRepository>();
        _mockAlertingService = new Mock<IAlertingService>();
        var mockLogger = new Mock<ILogger<LlmCostAlertService>>();

        _alertService = new LlmCostAlertService(
            _mockCostLogRepository.Object,
            _mockAlertingService.Object,
            mockLogger.Object);

        _handler = new CheckLlmCostAlertsCommandHandler(_alertService);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }
}

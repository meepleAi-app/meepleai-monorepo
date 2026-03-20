using Api.BoundedContexts.Authentication.Application.EventHandlers;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.EventHandlers;

[Trait("Category", TestCategories.Unit)]
public class ApiKeyUsedEventHandlerTests
{
    private readonly Mock<IApiKeyUsageLogRepository> _mockUsageLogRepository;
    private readonly Mock<ILogger<ApiKeyUsedEventHandler>> _mockLogger;
    private readonly ApiKeyUsedEventHandler _handler;

    public ApiKeyUsedEventHandlerTests()
    {
        _mockUsageLogRepository = new Mock<IApiKeyUsageLogRepository>();
        _mockLogger = new Mock<ILogger<ApiKeyUsedEventHandler>>();
        _handler = new ApiKeyUsedEventHandler(
            _mockUsageLogRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidEvent_AddsUsageLog()
    {
        var keyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var endpoint = "/api/v1/games";
        var @event = new ApiKeyUsedEvent(keyId, userId, endpoint);

        _mockUsageLogRepository
            .Setup(r => r.AddAsync(It.IsAny<ApiKeyUsageLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUsageLogRepository
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await _handler.Handle(@event, CancellationToken.None);

        _mockUsageLogRepository.Verify(
            r => r.AddAsync(It.IsAny<ApiKeyUsageLog>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
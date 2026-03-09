using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Tests for ProcessingQueueMonitorService constructor validation.
/// Issue #5460: Proactive alerts background service.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ProcessingQueueMonitorServiceTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly IConfiguration _configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ProcessingQueueMonitor:CheckIntervalSeconds"] = "120",
        })
        .Build();
    private readonly Mock<ILogger<ProcessingQueueMonitorService>> _loggerMock = new();

    [Fact]
    public void Constructor_NullScopeFactory_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new ProcessingQueueMonitorService(
            null!,
            _configuration,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("scopeFactory");
    }

    [Fact]
    public void Constructor_NullConfiguration_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new ProcessingQueueMonitorService(
            _scopeFactoryMock.Object,
            null!,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configuration");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new ProcessingQueueMonitorService(
            _scopeFactoryMock.Object,
            _configuration,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}

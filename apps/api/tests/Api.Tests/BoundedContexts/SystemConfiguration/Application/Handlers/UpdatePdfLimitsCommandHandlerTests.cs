using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Tests for UpdatePdfLimitsCommandHandler.
/// Issue #3673: PDF Upload Limits Admin UI
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class UpdatePdfLimitsCommandHandlerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IConfigurationRepository> _mockConfigRepository;
    private readonly Mock<ILogger<UpdatePdfLimitsCommandHandler>> _mockLogger;
    private readonly UpdatePdfLimitsCommandHandler _handler;

    public UpdatePdfLimitsCommandHandlerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockConfigRepository = new Mock<IConfigurationRepository>();
        _mockLogger = new Mock<ILogger<UpdatePdfLimitsCommandHandler>>();
        _handler = new UpdatePdfLimitsCommandHandler(
            _mockMediator.Object,
            _mockConfigRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WhenConfigurationsDoNotExist_CreatesNewConfigurations()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var command = new UpdatePdfLimitsCommand
        {
            Tier = "free",
            MaxPerDay = 10,
            MaxPerWeek = 50,
            MaxPerGame = 3,
            AdminUserId = adminUserId
        };

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), "All", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfig?)null);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateConfigurationCommand cmd, CancellationToken ct) =>
            {
                return new Api.BoundedContexts.SystemConfiguration.Application.DTOs.ConfigurationDto(
                    Id: Guid.NewGuid(),
                    Key: cmd.Key,
                    Value: cmd.Value,
                    ValueType: cmd.ValueType,
                    Description: cmd.Description,
                    Category: cmd.Category,
                    IsActive: true,
                    RequiresRestart: false,
                    Environment: cmd.Environment,
                    Version: 1,
                    CreatedAt: DateTime.UtcNow,
                    UpdatedAt: DateTime.UtcNow
                );
            });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("free", result.Tier);
        Assert.Equal(10, result.MaxPerDay);
        Assert.Equal(50, result.MaxPerWeek);
        Assert.Equal(3, result.MaxPerGame);

        // Verify CreateConfigurationCommand was sent 3 times
        _mockMediator.Verify(
            m => m.Send(
                It.Is<CreateConfigurationCommand>(c =>
                    c.Key.StartsWith("UploadLimits:free:", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_WhenConfigurationsExist_UpdatesConfigurations()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var command = new UpdatePdfLimitsCommand
        {
            Tier = "premium",
            MaxPerDay = 200,
            MaxPerWeek = 1000,
            MaxPerGame = 20,
            AdminUserId = adminUserId
        };

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), "All", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, string env, bool activeOnly, CancellationToken ct) =>
            {
                var id = Guid.NewGuid();
                return new SystemConfig(id,
                    new Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects.ConfigKey(key),
                    "100", "int", Guid.NewGuid());
            });

        _mockMediator
            .Setup(m => m.Send(It.IsAny<UpdateConfigValueCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpdateConfigValueCommand cmd, CancellationToken ct) =>
            {
                return new Api.BoundedContexts.SystemConfiguration.Application.DTOs.ConfigurationDto(
                    Id: cmd.ConfigId,
                    Key: "UploadLimits:premium:DailyLimit",
                    Value: cmd.NewValue,
                    ValueType: "int",
                    Description: null,
                    Category: "UploadLimits",
                    IsActive: true,
                    RequiresRestart: false,
                    Environment: "All",
                    Version: 2,
                    CreatedAt: DateTime.UtcNow.AddDays(-1),
                    UpdatedAt: DateTime.UtcNow
                );
            });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("premium", result.Tier);
        Assert.Equal(200, result.MaxPerDay);

        // Verify UpdateConfigValueCommand was sent 3 times
        _mockMediator.Verify(
            m => m.Send(It.IsAny<UpdateConfigValueCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }
}

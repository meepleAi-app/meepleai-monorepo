using Api.BoundedContexts.Administration.Application.Commands.AlertConfiguration;
using Api.BoundedContexts.Administration.Application.Commands.AlertConfiguration;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertConfigurations;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for UpdateAlertConfigurationCommandHandler.
/// Tests configuration update/create with logging and category validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdateAlertConfigurationCommandHandlerTests
{
    private readonly Mock<IAlertConfigurationRepository> _mockRepository;
    private readonly Mock<ILogger<UpdateAlertConfigurationCommandHandler>> _mockLogger;
    private readonly UpdateAlertConfigurationCommandHandler _handler;

    public UpdateAlertConfigurationCommandHandlerTests()
    {
        _mockRepository = new Mock<IAlertConfigurationRepository>();
        _mockLogger = new Mock<ILogger<UpdateAlertConfigurationCommandHandler>>();
        _handler = new UpdateAlertConfigurationCommandHandler(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithExistingConfiguration_UpdatesSuccessfully()
    {
        // Arrange
        var existingConfig = AlertConfiguration.Create(
            "smtp.host",
            "smtp.example.com",
            ConfigCategory.Email,
            "admin@test.com",
            "SMTP host configuration"
        );

        var command = new UpdateAlertConfigurationCommand(
            ConfigKey: "smtp.host",
            ConfigValue: "smtp.newhost.com",
            Category: "Email",
            UpdatedBy: "admin@test.com",
            Description: "Updated SMTP host"
        );

        _mockRepository.Setup(r => r.GetByKeyAsync("smtp.host", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        AlertConfiguration? capturedConfig = null;
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<AlertConfiguration>(), It.IsAny<CancellationToken>()))
            .Callback<AlertConfiguration, CancellationToken>((config, _) => capturedConfig = config)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        capturedConfig.Should().NotBeNull();
        capturedConfig!.ConfigValue.Should().Be("smtp.newhost.com");
        capturedConfig.UpdatedBy.Should().Be("admin@test.com");

        _mockRepository.Verify(r => r.GetByKeyAsync("smtp.host", It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<AlertConfiguration>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<AlertConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNonExistentConfiguration_CreatesNew()
    {
        // Arrange
        var command = new UpdateAlertConfigurationCommand(
            ConfigKey: "slack.webhook",
            ConfigValue: "https://hooks.slack.com/services/T00/B00/XX",
            Category: "Slack",
            UpdatedBy: "admin@test.com",
            Description: "Slack webhook URL"
        );

        _mockRepository.Setup(r => r.GetByKeyAsync("slack.webhook", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertConfiguration?)null);

        AlertConfiguration? capturedConfig = null;
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<AlertConfiguration>(), It.IsAny<CancellationToken>()))
            .Callback<AlertConfiguration, CancellationToken>((config, _) => capturedConfig = config)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        capturedConfig.Should().NotBeNull();
        capturedConfig!.ConfigKey.Should().Be("slack.webhook");
        capturedConfig.ConfigValue.Should().Be("https://hooks.slack.com/services/T00/B00/XX");
        capturedConfig.Category.Should().Be(ConfigCategory.Slack);
        capturedConfig.Description.Should().Be("Slack webhook URL");

        _mockRepository.Verify(r => r.GetByKeyAsync("slack.webhook", It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<AlertConfiguration>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<AlertConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUpdating_LogsInformation()
    {
        // Arrange
        var existingConfig = AlertConfiguration.Create(
            "pagerduty.apikey",
            "old-key",
            ConfigCategory.PagerDuty,
            "system@test.com"
        );

        var command = new UpdateAlertConfigurationCommand(
            ConfigKey: "pagerduty.apikey",
            ConfigValue: "new-key",
            Category: "PagerDuty",
            UpdatedBy: "admin@test.com",
            Description: null
        );

        _mockRepository.Setup(r => r.GetByKeyAsync("pagerduty.apikey", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<AlertConfiguration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Alert configuration updated")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCreating_LogsInformation()
    {
        // Arrange
        var command = new UpdateAlertConfigurationCommand(
            ConfigKey: "email.from",
            ConfigValue: "alerts@example.com",
            Category: "Email",
            UpdatedBy: "admin@test.com",
            Description: "Alert sender email"
        );

        _mockRepository.Setup(r => r.GetByKeyAsync("email.from", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertConfiguration?)null);

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<AlertConfiguration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Alert configuration created")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("Global")]
    [InlineData("Email")]
    [InlineData("Slack")]
    [InlineData("PagerDuty")]
    public async Task Handle_WithValidCategories_AcceptsAllCategories(string category)
    {
        // Arrange
        var command = new UpdateAlertConfigurationCommand(
            ConfigKey: "test.key",
            ConfigValue: "test-value",
            Category: category,
            UpdatedBy: "admin@test.com",
            Description: null
        );

        _mockRepository.Setup(r => r.GetByKeyAsync("test.key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertConfiguration?)null);

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<AlertConfiguration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithInvalidCategory_ThrowsArgumentException()
    {
        // Arrange
        var command = new UpdateAlertConfigurationCommand(
            ConfigKey: "test.key",
            ConfigValue: "test-value",
            Category: "InvalidCategory",
            UpdatedBy: "admin@test.com",
            Description: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _mockRepository.Verify(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        UpdateAlertConfigurationCommand? command = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(command!, CancellationToken.None));

        _mockRepository.Verify(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UpdateAlertConfigurationCommandHandler(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UpdateAlertConfigurationCommandHandler(_mockRepository.Object, null!));
    }
}

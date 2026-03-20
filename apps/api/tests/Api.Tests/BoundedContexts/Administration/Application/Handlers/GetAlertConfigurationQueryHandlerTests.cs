using Api.BoundedContexts.Administration.Application.Commands.AlertConfiguration;
using Api.BoundedContexts.Administration.Application.Queries.AlertConfiguration;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertConfigurations;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for GetAlertConfigurationQueryHandler.
/// Tests configuration retrieval by category with validation and error handling.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetAlertConfigurationQueryHandlerTests
{
    private readonly Mock<IAlertConfigurationRepository> _mockRepository;
    private readonly GetAlertConfigurationQueryHandler _handler;

    public GetAlertConfigurationQueryHandlerTests()
    {
        _mockRepository = new Mock<IAlertConfigurationRepository>();
        _handler = new GetAlertConfigurationQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidCategory_ReturnsConfigurationDto()
    {
        // Arrange
        var config = AlertConfiguration.Create(
            "smtp.host",
            "smtp.example.com",
            ConfigCategory.Email,
            "admin@test.com",
            "SMTP server configuration"
        );

        var query = new GetAlertConfigurationQuery("Email");

        _mockRepository.Setup(r => r.GetByCategoryAsync(ConfigCategory.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertConfiguration> { config });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(config.Id);
        result.ConfigKey.Should().Be("smtp.host");
        result.ConfigValue.Should().Be("smtp.example.com");
        result.Category.Should().Be("Email");
        result.IsEncrypted.Should().BeFalse();
        result.Description.Should().Be("SMTP server configuration");
        result.UpdatedBy.Should().Be("admin@test.com");

        _mockRepository.Verify(r => r.GetByCategoryAsync(ConfigCategory.Email, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMultipleConfigsInCategory_ReturnsFirstOne()
    {
        // Arrange
        var config1 = AlertConfiguration.Create(
            "slack.webhook",
            "https://hooks.slack.com/1",
            ConfigCategory.Slack,
            "admin@test.com"
        );

        var config2 = AlertConfiguration.Create(
            "slack.channel",
            "#alerts",
            ConfigCategory.Slack,
            "admin@test.com"
        );

        var query = new GetAlertConfigurationQuery("Slack");

        _mockRepository.Setup(r => r.GetByCategoryAsync(ConfigCategory.Slack, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertConfiguration> { config1, config2 });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ConfigKey.Should().Be("slack.webhook");
        result.Category.Should().Be("Slack");
    }

    [Fact]
    public async Task Handle_WithEmptyCategory_ThrowsInvalidOperationException()
    {
        // Arrange
        var query = new GetAlertConfigurationQuery("PagerDuty");

        _mockRepository.Setup(r => r.GetByCategoryAsync(ConfigCategory.PagerDuty, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertConfiguration>());

        // Act & Assert
        var act = () =>
            _handler.Handle(query, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Contain("No configuration found");
        exception.Message.Should().Contain("PagerDuty");

        _mockRepository.Verify(r => r.GetByCategoryAsync(ConfigCategory.PagerDuty, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Global")]
    [InlineData("Email")]
    [InlineData("Slack")]
    [InlineData("PagerDuty")]
    public async Task Handle_WithAllValidCategories_MapsCorrectly(string categoryName)
    {
        // Arrange
        var category = ConfigCategoryExtensions.FromString(categoryName);
        var config = AlertConfiguration.Create(
            $"{categoryName.ToLower()}.key",
            "test-value",
            category,
            "admin@test.com"
        );

        var query = new GetAlertConfigurationQuery(categoryName);

        _mockRepository.Setup(r => r.GetByCategoryAsync(category, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertConfiguration> { config });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Category.Should().Be(categoryName);
    }

    [Fact]
    public async Task Handle_WithInvalidCategory_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetAlertConfigurationQuery("InvalidCategory");

        // Act & Assert
        var act = () =>
            _handler.Handle(query, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();

        _mockRepository.Verify(r => r.GetByCategoryAsync(It.IsAny<ConfigCategory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithCaseInsensitiveCategory_WorksCorrectly()
    {
        // Arrange
        var config = AlertConfiguration.Create(
            "email.from",
            "alerts@example.com",
            ConfigCategory.Email,
            "admin@test.com"
        );

        var query = new GetAlertConfigurationQuery("email"); // lowercase

        _mockRepository.Setup(r => r.GetByCategoryAsync(ConfigCategory.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertConfiguration> { config });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Category.Should().Be("Email"); // normalized to proper case
    }

    [Fact]
    public async Task Handle_MapsAllDtoFieldsCorrectly()
    {
        // Arrange
        var config = AlertConfiguration.Create(
            "test.config",
            "test-value",
            ConfigCategory.Global,
            "creator@test.com",
            "Test description"
        );

        var query = new GetAlertConfigurationQuery("Global");

        _mockRepository.Setup(r => r.GetByCategoryAsync(ConfigCategory.Global, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertConfiguration> { config });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Id.Should().Be(config.Id);
        result.ConfigKey.Should().Be("test.config");
        result.ConfigValue.Should().Be("test-value");
        result.Category.Should().Be("Global");
        result.IsEncrypted.Should().BeFalse();
        result.Description.Should().Be("Test description");
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.UpdatedBy.Should().Be("creator@test.com");
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        GetAlertConfigurationQuery? query = null;

        // Act & Assert
        var act = () =>
            _handler.Handle(query!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();

        _mockRepository.Verify(r => r.GetByCategoryAsync(It.IsAny<ConfigCategory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new GetAlertConfigurationQueryHandler(null!);
act.Should().Throw<ArgumentNullException>();
    }
}
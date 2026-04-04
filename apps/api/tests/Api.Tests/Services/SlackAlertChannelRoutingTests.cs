using System.Net;
using System.Net.Http.Json;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for SlackAlertChannel multi-channel routing logic.
/// Validates that alerts are routed to the correct Slack channel
/// based on severity and _slack_category metadata.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SlackAlertChannelRoutingTests
{
    private static SlackAlertChannel CreateChannel(
        SlackConfiguration slackConfig,
        out Mock<HttpMessageHandler> mockHandler)
    {
        mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(mockHandler.Object);
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var alertingConfig = new AlertingConfiguration
        {
            Slack = slackConfig
        };

        var mockLogger = new Mock<ILogger<SlackAlertChannel>>();

        return new SlackAlertChannel(
            Options.Create(alertingConfig),
            mockHttpClientFactory.Object,
            mockLogger.Object);
    }

    private static SlackConfiguration CreateConfigWithRouting()
    {
        return new SlackConfiguration
        {
            Enabled = true,
            WebhookUrl = "https://hooks.slack.com/default",
            Channel = "#alerts",
            ChannelRouting = new Dictionary<string, SlackChannelRoute>
            {
                ["critical"] = new SlackChannelRoute
                {
                    WebhookUrl = "https://hooks.slack.com/critical",
                    Channel = "#alerts-critical"
                },
                ["infrastructure"] = new SlackChannelRoute
                {
                    WebhookUrl = "https://hooks.slack.com/infra",
                    Channel = "#alerts-infra"
                },
                ["ai"] = new SlackChannelRoute
                {
                    WebhookUrl = "https://hooks.slack.com/ai",
                    Channel = "#alerts-ai"
                }
            }
        };
    }

    [Fact]
    public async Task Critical_severity_routes_to_critical_channel()
    {
        // Arrange
        var config = CreateConfigWithRouting();
        var channel = CreateChannel(config, out var mockHandler);
        var metadata = new Dictionary<string, object>
        {
            ["_slack_category"] = "infrastructure"
        };

        // Act
        var result = await channel.SendAsync("health.postgres", "critical", "DB down", metadata);

        // Assert
        result.Should().BeTrue();
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.ToString() == "https://hooks.slack.com/critical"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Infrastructure_category_routes_to_infra_channel()
    {
        // Arrange
        var config = CreateConfigWithRouting();
        var channel = CreateChannel(config, out var mockHandler);
        var metadata = new Dictionary<string, object>
        {
            ["_slack_category"] = "infrastructure"
        };

        // Act — warning severity, so critical route is skipped
        var result = await channel.SendAsync("health.redis", "warning", "Redis degraded", metadata);

        // Assert
        result.Should().BeTrue();
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.ToString() == "https://hooks.slack.com/infra"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Unknown_category_falls_back_to_default()
    {
        // Arrange
        var config = CreateConfigWithRouting();
        var channel = CreateChannel(config, out var mockHandler);
        var metadata = new Dictionary<string, object>
        {
            ["_slack_category"] = "unknown-category"
        };

        // Act
        var result = await channel.SendAsync("health.custom", "info", "Custom service up", metadata);

        // Assert
        result.Should().BeTrue();
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.ToString() == "https://hooks.slack.com/default"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task No_metadata_falls_back_to_default()
    {
        // Arrange
        var config = CreateConfigWithRouting();
        var channel = CreateChannel(config, out var mockHandler);

        // Act
        var result = await channel.SendAsync("health.test", "info", "Test alert", null);

        // Assert
        result.Should().BeTrue();
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.ToString() == "https://hooks.slack.com/default"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Empty_webhookUrl_in_routing_falls_back_to_default()
    {
        // Arrange
        var config = CreateConfigWithRouting();
        // Override infrastructure route with empty webhook URL
        config.ChannelRouting!["infrastructure"] = new SlackChannelRoute
        {
            WebhookUrl = "",
            Channel = "#alerts-infra"
        };
        var channel = CreateChannel(config, out var mockHandler);
        var metadata = new Dictionary<string, object>
        {
            ["_slack_category"] = "infrastructure"
        };

        // Act
        var result = await channel.SendAsync("health.redis", "warning", "Redis degraded", metadata);

        // Assert
        result.Should().BeTrue();
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.ToString() == "https://hooks.slack.com/default"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Critical_takes_precedence_over_category()
    {
        // Arrange
        var config = CreateConfigWithRouting();
        var channel = CreateChannel(config, out var mockHandler);
        var metadata = new Dictionary<string, object>
        {
            ["_slack_category"] = "ai"  // Would route to ai channel, but critical overrides
        };

        // Act
        var result = await channel.SendAsync("health.embedding", "critical", "Embedding down", metadata);

        // Assert
        result.Should().BeTrue();
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.ToString() == "https://hooks.slack.com/critical"),
            ItExpr.IsAny<CancellationToken>());
    }
}

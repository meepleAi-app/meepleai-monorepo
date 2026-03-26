using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Services;

[Trait("Category", TestCategories.Unit)]
public class StatusPageRendererTests
{
    [Fact]
    public void Renders_valid_html_with_doctype_and_charset()
    {
        // Arrange
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["postgres"] = new HealthReportEntry(
                HealthStatus.Healthy,
                "PostgreSQL is accessible",
                TimeSpan.FromMilliseconds(14),
                exception: null,
                data: null,
                tags: new[] { "db", "sql", "core", "critical" })
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(100));

        // Act
        var html = StatusPageRenderer.RenderHtml(report);

        // Assert
        html.Should().StartWithEquivalentOf("<!DOCTYPE html>");
        html.Should().ContainEquivalentOf("charset=\"UTF-8\"");
        html.Should().ContainEquivalentOf("</html>");
    }

    [Fact]
    public void All_healthy_shows_green_overall_status()
    {
        // Arrange
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["postgres"] = new HealthReportEntry(
                HealthStatus.Healthy,
                "OK",
                TimeSpan.FromMilliseconds(10),
                exception: null,
                data: null,
                tags: new[] { "core", "critical" }),
            ["redis"] = new HealthReportEntry(
                HealthStatus.Healthy,
                "OK",
                TimeSpan.FromMilliseconds(5),
                exception: null,
                data: null,
                tags: new[] { "core", "critical" })
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(15));

        // Act
        var html = StatusPageRenderer.RenderHtml(report);

        // Assert
        html.Should().ContainEquivalentOf("#22c55e");
        html.Should().ContainEquivalentOf("All Systems Operational");
    }

    [Fact]
    public void One_degraded_shows_yellow_overall_status()
    {
        // Arrange
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["postgres"] = new HealthReportEntry(
                HealthStatus.Healthy,
                "OK",
                TimeSpan.FromMilliseconds(10),
                exception: null,
                data: null,
                tags: new[] { "core", "critical" }),
            ["openrouter"] = new HealthReportEntry(
                HealthStatus.Degraded,
                "Slow response",
                TimeSpan.FromMilliseconds(2500),
                exception: null,
                data: null,
                tags: new[] { "ai", "non-critical" })
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(2510));

        // Act
        var html = StatusPageRenderer.RenderHtml(report);

        // Assert
        html.Should().ContainEquivalentOf("#eab308");
        html.Should().ContainEquivalentOf("Partial Degradation");
    }

    [Fact]
    public void One_unhealthy_shows_red_overall_status()
    {
        // Arrange
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["postgres"] = new HealthReportEntry(
                HealthStatus.Unhealthy,
                "Connection refused",
                TimeSpan.FromMilliseconds(5000),
                exception: null,
                data: null,
                tags: new[] { "core", "critical" }),
            ["redis"] = new HealthReportEntry(
                HealthStatus.Healthy,
                "OK",
                TimeSpan.FromMilliseconds(3),
                exception: null,
                data: null,
                tags: new[] { "core", "critical" })
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(5003));

        // Act
        var html = StatusPageRenderer.RenderHtml(report);

        // Assert
        html.Should().ContainEquivalentOf("#ef4444");
        html.Should().ContainEquivalentOf("Service Disruption");
    }

    [Fact]
    public void Groups_services_by_tag_category()
    {
        // Arrange
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["postgres"] = new HealthReportEntry(
                HealthStatus.Healthy,
                "OK",
                TimeSpan.FromMilliseconds(10),
                exception: null,
                data: null,
                tags: new[] { "db", "core", "critical" }),
            ["openrouter"] = new HealthReportEntry(
                HealthStatus.Healthy,
                "OK",
                TimeSpan.FromMilliseconds(100),
                exception: null,
                data: null,
                tags: new[] { "ai", "non-critical" }),
            ["bgg-api"] = new HealthReportEntry(
                HealthStatus.Healthy,
                "OK",
                TimeSpan.FromMilliseconds(200),
                exception: null,
                data: null,
                tags: new[] { "external", "non-critical" })
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(310));

        // Act
        var html = StatusPageRenderer.RenderHtml(report);

        // Assert
        html.Should().ContainEquivalentOf("Core Services");
        html.Should().ContainEquivalentOf("AI Services");
        html.Should().ContainEquivalentOf("External Services");
    }

    [Fact]
    public void Contains_auto_refresh_meta_tag()
    {
        // Arrange
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["postgres"] = new HealthReportEntry(
                HealthStatus.Healthy,
                "OK",
                TimeSpan.FromMilliseconds(10),
                exception: null,
                data: null,
                tags: new[] { "core", "critical" })
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(10));

        // Act
        var html = StatusPageRenderer.RenderHtml(report);

        // Assert
        html.Should().ContainEquivalentOf("<meta http-equiv=\"refresh\" content=\"30\">");
    }

    [Fact]
    public void Shows_response_time_per_service()
    {
        // Arrange
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["postgres"] = new HealthReportEntry(
                HealthStatus.Healthy,
                "OK",
                TimeSpan.FromMilliseconds(14),
                exception: null,
                data: null,
                tags: new[] { "core", "critical" })
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(14));

        // Act
        var html = StatusPageRenderer.RenderHtml(report);

        // Assert
        html.Should().ContainEquivalentOf("14");
        html.Should().ContainEquivalentOf("ms");
    }

    [Fact]
    public void Renders_service_description_when_available()
    {
        // Arrange
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["postgres"] = new HealthReportEntry(
                HealthStatus.Healthy,
                "PostgreSQL is accessible and responding",
                TimeSpan.FromMilliseconds(14),
                exception: null,
                data: null,
                tags: new[] { "core", "critical" })
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(14));

        // Act
        var html = StatusPageRenderer.RenderHtml(report);

        // Assert
        html.Should().ContainEquivalentOf("PostgreSQL is accessible and responding");
    }
}

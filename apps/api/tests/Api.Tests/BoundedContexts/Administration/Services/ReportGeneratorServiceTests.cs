using System.Net.Http;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Events;
using Api.Tests.TestHelpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Services;

/// <summary>
/// Unit tests for ReportGeneratorService
/// ISSUE-919: Comprehensive test coverage for report generation
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ReportGeneratorServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<ReportGeneratorService>> _loggerMock;
    private readonly Mock<IPrometheusQueryService> _prometheusMock;
    private readonly ReportGeneratorService _sut;

    public ReportGeneratorServiceTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _loggerMock = new Mock<ILogger<ReportGeneratorService>>();
        _prometheusMock = new Mock<IPrometheusQueryService>();
        // Default: Prometheus returns no data → health metrics are omitted from
        // reports (unless a test sets up specific values).
        _prometheusMock
            .Setup(p => p.QueryRangeAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrometheusQueryResult("matrix", Array.Empty<PrometheusTimeSeries>()));
        _sut = new ReportGeneratorService(_dbContext, _loggerMock.Object, _prometheusMock.Object);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region GenerateAsync Tests

    [Theory]
    [InlineData(ReportTemplate.SystemHealth, ReportFormat.Csv)]
    [InlineData(ReportTemplate.UserActivity, ReportFormat.Json)]
    [InlineData(ReportTemplate.AIUsage, ReportFormat.Pdf)]
    [InlineData(ReportTemplate.ContentMetrics, ReportFormat.Csv)]
    public async Task GenerateAsync_ValidTemplateAndFormat_ReturnsReportData(
        ReportTemplate template,
        ReportFormat format)
    {
        var parameters = CreateValidParameters(template);

        var result = await _sut.GenerateAsync(template, format, parameters, CancellationToken.None);

        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        result.FileName.Should().NotBeNull();
        (result.FileSizeBytes > 0).Should().BeTrue();
        result.FileName.Should().Contain(template.ToString());
        result.FileName.ToLowerInvariant().Should().Contain(format.ToString().ToLowerInvariant());
    }

    [Fact]
    public async Task GenerateAsync_SystemHealthReport_WithPrometheusData_ContainsRealMetrics()
    {
        // Prometheus reports a 5% error rate and 0.25s (250ms) average latency.
        SetupPrometheusMetrics(errorRateRatio: 0.05, avgLatencySeconds: 0.25);

        var parameters = new Dictionary<string, object> { ["hours"] = 24 };

        var result = await _sut.GenerateAsync(
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters,
            CancellationToken.None);

        var content = System.Text.Encoding.UTF8.GetString(result.Content);
        content.Should().ContainEquivalentOf("\"errorRate\"");
        content.Should().ContainEquivalentOf("\"responseTime\"");
        // Real values, not the old hardcoded 0.0 placeholder.
        content.Should().Contain("0.05");
        content.Should().Contain("250");
        // The fabricated "uptime" (which was just the window param) is gone.
        content.Should().NotContainEquivalentOf("\"uptime\"");
    }

    [Fact]
    public async Task GenerateAsync_SystemHealthReport_WhenPrometheusUnavailable_OmitsMetricsInsteadOfFabricatingZero()
    {
        _prometheusMock
            .Setup(p => p.QueryRangeAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("prometheus unavailable"));

        var parameters = new Dictionary<string, object> { ["hours"] = 24 };

        var result = await _sut.GenerateAsync(
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters,
            CancellationToken.None);

        var content = System.Text.Encoding.UTF8.GetString(result.Content);
        // No fabricated metric keys when Prometheus is unavailable — omit, don't zero.
        content.Should().NotContainEquivalentOf("\"errorRate\"");
        content.Should().NotContainEquivalentOf("\"responseTime\"");
        content.Should().NotContainEquivalentOf("\"uptime\"");
        // The report still renders with its real section data + window metadata.
        content.Should().ContainEquivalentOf("\"hours\"");
    }

    [Fact]
    public async Task GenerateAsync_UserActivityReport_ContainsUserMetrics()
    {
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-30),
            ["endDate"] = DateTime.UtcNow
        };

        var result = await _sut.GenerateAsync(
            ReportTemplate.UserActivity,
            ReportFormat.Json,
            parameters,
            CancellationToken.None);

        var content = System.Text.Encoding.UTF8.GetString(result.Content);
        content.Should().ContainEquivalentOf("\"activeUsers\"");
        content.Should().ContainEquivalentOf("\"totalLogins\"");
    }

    [Fact]
    public async Task GenerateAsync_UserActivityReport_ActiveUsers_CountsDistinctSessionUsers_NotRegistrations()
    {
        // #3104: "active users" must be the count of DISTINCT users with >=1 session in the
        // window, NOT the number of new registrations. Seed 3 sessions from 2 distinct users
        // and ZERO registrations in the window: the old code reported activeUsers = registrations = 0;
        // the correct value is 2.
        var now = DateTime.UtcNow;
        var startDate = now.AddDays(-30);
        var endDate = now.AddDays(1);

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        _dbContext.UserSessions.AddRange(
            NewSession(userA, now.AddDays(-5)),
            NewSession(userA, now.AddDays(-3)),
            NewSession(userB, now.AddDays(-2)));
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GenerateAsync(
            ReportTemplate.UserActivity,
            ReportFormat.Json,
            new Dictionary<string, object> { ["startDate"] = startDate, ["endDate"] = endDate },
            CancellationToken.None);

        var content = System.Text.Encoding.UTF8.GetString(result.Content);
        using var doc = System.Text.Json.JsonDocument.Parse(content);
        var metadata = doc.RootElement.GetProperty("metadata");
        metadata.GetProperty("activeUsers").GetInt32().Should().Be(2);
        metadata.GetProperty("totalRegistrations").GetInt32().Should().Be(0);
    }

    private static UserSessionEntity NewSession(Guid userId, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = Guid.NewGuid().ToString(),
        CreatedAt = createdAt,
        ExpiresAt = createdAt.AddHours(1),
        User = null!
    };

    [Fact]
    public async Task GenerateAsync_AIUsageReport_ContainsCostMetrics()
    {
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-30),
            ["endDate"] = DateTime.UtcNow
        };

        var result = await _sut.GenerateAsync(
            ReportTemplate.AIUsage,
            ReportFormat.Json,
            parameters,
            CancellationToken.None);

        var content = System.Text.Encoding.UTF8.GetString(result.Content);
        content.Should().ContainEquivalentOf("\"totalCost\"");
        content.Should().ContainEquivalentOf("\"tokenUsage\"");
    }

    [Fact]
    public async Task GenerateAsync_ContentMetricsReport_ContainsDocumentStats()
    {
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-30),
            ["endDate"] = DateTime.UtcNow
        };

        var result = await _sut.GenerateAsync(
            ReportTemplate.ContentMetrics,
            ReportFormat.Json,
            parameters,
            CancellationToken.None);

        var content = System.Text.Encoding.UTF8.GetString(result.Content);
        content.Should().ContainEquivalentOf("\"totalDocuments\"");
        content.Should().ContainEquivalentOf("\"vectorEmbeddings\"");
    }

    [Fact]
    public async Task GenerateAsync_ContentMetricsReport_ActiveGamesCountsOnlyAiReadyGames()
    {
        // #3123: "active" = AI-ready (HasKnowledgeBase). 3 AI-ready + 2 plain → Active=3, Total=5.
        SeedSharedGame("AI-ready A", hasKnowledgeBase: true);
        SeedSharedGame("AI-ready B", hasKnowledgeBase: true);
        SeedSharedGame("AI-ready C", hasKnowledgeBase: true);
        SeedSharedGame("Plain D", hasKnowledgeBase: false);
        SeedSharedGame("Plain E", hasKnowledgeBase: false);
        await _dbContext.SaveChangesAsync();

        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-30),
            ["endDate"] = DateTime.UtcNow
        };

        var result = await _sut.GenerateAsync(
            ReportTemplate.ContentMetrics,
            ReportFormat.Json,
            parameters,
            CancellationToken.None);

        var content = System.Text.Encoding.UTF8.GetString(result.Content);
        using var doc = System.Text.Json.JsonDocument.Parse(content);
        var totalGames = FindNumber(doc.RootElement, "totalGames");
        var activeGames = FindNumber(doc.RootElement, "activeGames");

        totalGames.Should().Be(5);
        activeGames.Should().Be(3);              // only AI-ready games, not all 5
        activeGames.Should().NotBe(totalGames);  // regression guard: Active must not equal Total
    }

    private void SeedSharedGame(string title, bool hasKnowledgeBase)
    {
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = title,
            HasKnowledgeBase = hasKnowledgeBase,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static int? FindNumber(System.Text.Json.JsonElement element, string propertyName)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                        && prop.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        return prop.Value.GetInt32();
                    }

                    var nested = FindNumber(prop.Value, propertyName);
                    if (nested.HasValue) return nested;
                }
                break;
            case System.Text.Json.JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var nested = FindNumber(item, propertyName);
                    if (nested.HasValue) return nested;
                }
                break;
        }

        return null;
    }

    [Fact]
    public async Task GenerateAsync_CsvFormat_ProducesValidCsv()
    {
        var parameters = CreateValidParameters(ReportTemplate.SystemHealth);

        var result = await _sut.GenerateAsync(
            ReportTemplate.SystemHealth,
            ReportFormat.Csv,
            parameters,
            CancellationToken.None);

        var content = System.Text.Encoding.UTF8.GetString(result.Content);
        content.Should().Contain(",");
        content.Should().Contain("\n");
        result.FileName.Should().EndWith(".csv");
    }

    [Fact]
    public async Task GenerateAsync_JsonFormat_ProducesValidJson()
    {
        var parameters = CreateValidParameters(ReportTemplate.UserActivity);

        var result = await _sut.GenerateAsync(
            ReportTemplate.UserActivity,
            ReportFormat.Json,
            parameters,
            CancellationToken.None);

        var content = System.Text.Encoding.UTF8.GetString(result.Content);
        content.TrimStart().Should().StartWith("{");
        content.TrimEnd().Should().EndWith("}");
        result.FileName.Should().EndWith(".json");
    }

    [Fact]
    public async Task GenerateAsync_PdfFormat_ProducesPdfFile()
    {
        var parameters = CreateValidParameters(ReportTemplate.AIUsage);

        var result = await _sut.GenerateAsync(
            ReportTemplate.AIUsage,
            ReportFormat.Pdf,
            parameters,
            CancellationToken.None);

        (result.Content.Length >= 4).Should().BeTrue();
        result.Content[0].Should().Be(0x25); // %
        result.Content[1].Should().Be(0x50); // P
        result.Content[2].Should().Be(0x44); // D
        result.Content[3].Should().Be(0x46); // F
        result.FileName.Should().EndWith(".pdf");
        (result.FileSizeBytes > 1000).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_InvalidTemplate_ThrowsArgumentException()
    {
        var invalidTemplate = (ReportTemplate)999;
        var parameters = new Dictionary<string, object>();

        var act = async () =>
            await _sut.GenerateAsync(invalidTemplate, ReportFormat.Csv, parameters, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GenerateAsync_InvalidParameters_ThrowsArgumentException()
    {
        var parameters = new Dictionary<string, object>
        {
            ["invalidParam"] = "value"
        };

        var act = async () =>
            await _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Csv, parameters, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateAsync_MissingDateRange_ThrowsArgumentException()
    {
        var parameters = new Dictionary<string, object>();

        var act = async () =>
            await _sut.GenerateAsync(ReportTemplate.UserActivity, ReportFormat.Csv, parameters, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<ArgumentException>()).Which;

        exception.Message.Should().ContainEquivalentOf("startDate");
    }

    [Fact]
    public async Task GenerateAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var parameters = CreateValidParameters(ReportTemplate.SystemHealth);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () =>
            await _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Csv, parameters, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region ValidateParameters Tests

    [Fact]
    public void ValidateParameters_ValidDateRange_ReturnsValid()
    {
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-7),
            ["endDate"] = DateTime.UtcNow
        };

        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.SystemHealth, parameters);

        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void ValidateParameters_MissingStartDate_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, object>
        {
            ["endDate"] = DateTime.UtcNow
        };

        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.UserActivity, parameters);

        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNull();
        errorMessage.Should().ContainEquivalentOf("startDate");
    }

    [Fact]
    public void ValidateParameters_MissingEndDate_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-7)
        };

        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.AIUsage, parameters);

        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNull();
        errorMessage.Should().ContainEquivalentOf("endDate");
    }

    [Fact]
    public void ValidateParameters_EndDateBeforeStartDate_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow,
            ["endDate"] = DateTime.UtcNow.AddDays(-7)
        };

        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.ContentMetrics, parameters);

        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNull();
        errorMessage.Should().ContainEquivalentOf("after");
    }

    [Fact]
    public void ValidateParameters_DateRangeTooLarge_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddYears(-2),
            ["endDate"] = DateTime.UtcNow
        };

        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.SystemHealth, parameters);

        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNull();
        errorMessage.Should().ContainEquivalentOf("range");
    }

    #endregion

    #region Helper Methods

    private static IReadOnlyDictionary<string, object> CreateValidParameters(ReportTemplate template)
    {
        return new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-30),
            ["endDate"] = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Wires the Prometheus mock so the error-rate query returns <paramref name="errorRateRatio"/>
    /// and the latency query returns <paramref name="avgLatencySeconds"/> (seconds; the service
    /// converts to ms). Queries are told apart by a substring of their PromQL.
    /// </summary>
    private void SetupPrometheusMetrics(double errorRateRatio, double avgLatencySeconds)
    {
        _prometheusMock
            .Setup(p => p.QueryRangeAsync(
                It.Is<string>(q => q.Contains("status=~", StringComparison.Ordinal)),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SingleValueResult(errorRateRatio));
        _prometheusMock
            .Setup(p => p.QueryRangeAsync(
                It.Is<string>(q => q.Contains("http_request_duration_seconds", StringComparison.Ordinal)),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SingleValueResult(avgLatencySeconds));
    }

    private static PrometheusQueryResult SingleValueResult(double value) =>
        new PrometheusQueryResult(
            "matrix",
            new[]
            {
                new PrometheusTimeSeries(
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    new[] { new PrometheusDataPoint(DateTime.UtcNow, value) })
            });

    #endregion
}

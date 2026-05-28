using System.Diagnostics.Metrics;
using System.Net;
using Api.Infrastructure;
using Api.Observability;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Observability.Metrics;

/// <summary>
/// Integration tests for domain event log Prometheus counters (BE-3 #1590 AC7).
/// G1: meepleai.domain_event_log.inserted.total{event_type} — incremented once per
///     domain_event_logs row inserted.
/// G2: meepleai.domain_event_log.dispatch_failures.total{event_type,handler_name} —
///     incremented when a MediatR handler throws after the row is durably persisted.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1590")]
public sealed class DomainEventLogMetricsTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _webFactory = null!;
    private HttpClient _client = null!;

    public DomainEventLogMetricsTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"domain_event_metrics_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _webFactory = IntegrationWebApplicationFactory.Create(connectionString);
        using var scope = _webFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await dbContext.Database.MigrateAsync();
        _client = _webFactory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_webFactory is not null)
            await _webFactory.DisposeAsync();
        if (!string.IsNullOrEmpty(_testDbName))
            await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    /// <summary>
    /// G1: Posting quick-create (which emits agent.created) must increment
    /// meepleai.domain_event_log.inserted.total with tag event_type="agent.created".
    /// </summary>
    [Fact]
    public async Task InsertedCounter_G1_IncrementsWithEventTypeTag_WhenAgentCreatedEmitted()
    {
        // Arrange
        using var scope = _webFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan-Metrics-G1");

        var measurements = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (string.Equals(instrument.Meter.Name, MeepleAiMetrics.MeterName, StringComparison.Ordinal) &&
                    string.Equals(instrument.Name, "meepleai.domain_event_log.inserted.total", StringComparison.Ordinal))
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, state) =>
            measurements.Add((value, tags.ToArray())));
        listener.Start();

        var createRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/agents/quick-create",
            sessionToken,
            new { gameId });

        // Act
        var response = await _client.SendAsync(createRequest);

        // Assert — HTTP success first
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.OK, HttpStatusCode.Created },
            "quick-create must succeed to generate the domain event log row");

        // Give any async metric emission a moment to land
        await Task.Delay(100);

        // G1: at least one measurement with event_type="agent.created"
        measurements.Should().Contain(
            m => m.Tags.Any(t =>
                string.Equals(t.Key, "event_type", StringComparison.Ordinal) &&
                string.Equals(t.Value as string, "agent.created", StringComparison.Ordinal)),
            "meepleai.domain_event_log.inserted.total must fire with event_type=agent.created (#1590 G1)");

        // Each measurement must carry value=1 (one row at a time)
        measurements
            .Where(m => m.Tags.Any(t => string.Equals(t.Value as string, "agent.created", StringComparison.Ordinal)))
            .Should().AllSatisfy(m => m.Value.Should().Be(1));
    }

    /// <summary>
    /// Smoke test: Verifies the two counter instruments are properly initialized
    /// and carry the expected names/units without spinning up Testcontainers.
    /// </summary>
    [Fact]
    public void Counters_G1_G2_AreInitialized_WithCorrectNamesAndUnits()
    {
        MeepleAiMetrics.DomainEventsInserted.Should().NotBeNull(
            "G1 counter must be registered at startup (#1590)");
        MeepleAiMetrics.DomainEventDispatchFailures.Should().NotBeNull(
            "G2 counter must be registered at startup (#1590)");

        MeepleAiMetrics.DomainEventsInserted.Name
            .Should().Be("meepleai.domain_event_log.inserted.total");
        MeepleAiMetrics.DomainEventDispatchFailures.Name
            .Should().Be("meepleai.domain_event_log.dispatch_failures.total");

        MeepleAiMetrics.DomainEventsInserted.Unit.Should().Be("events");
        MeepleAiMetrics.DomainEventDispatchFailures.Unit.Should().Be("failures");
    }
}

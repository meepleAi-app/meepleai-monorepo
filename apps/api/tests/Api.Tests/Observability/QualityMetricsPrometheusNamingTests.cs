using Api.Models;
using Api.Observability;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using Xunit;

namespace Api.Tests.Observability;

/// <summary>
/// #3817 — il nome che una metrica assume in Prometheus non e' quello dichiarato in C#:
/// l'exporter OTel vi incorpora l'unit. Sbagliarlo produce alert ciechi che non falliscono
/// mai (il difetto di #3798), quindi il nome va PINNATO da uno scrape vero, non dedotto.
///
/// Questi test avviano l'exporter reale in-process e leggono /metrics: se un aggiornamento
/// dell'esportatore cambiasse la composizione, cadono qui invece che in silenzio su staging.
/// I nomi asseriti sono quelli che <c>infra/prometheus-rules.yml</c> interroga.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class QualityMetricsPrometheusNamingTests
{
    [Fact]
    public async Task QualityScore_IsExposedWithoutDuplicatedUnitSuffix()
    {
        // L'istrumento e' "meepleai.quality.score" con unit "score": poiche' il nome termina
        // gia' con l'unit, l'exporter NON la riappende (nessun "_score_score").
        var scrape = await ScrapeAfterRecordingAsync();

        scrape.Should().Contain("meepleai_quality_score_bucket");
        scrape.Should().NotContain("meepleai_quality_score_score");
    }

    [Fact]
    public async Task QualityScore_IsAHistogram_SoTheBareSeriesDoesNotExist()
    {
        // Conseguenza che le recording rules devono rispettare: la media NON si ottiene con
        // avg(meepleai_quality_score{...}) — quella serie non esiste — ma da _sum/_count.
        var scrape = await ScrapeAfterRecordingAsync();

        scrape.Should().Contain("meepleai_quality_score_sum");
        scrape.Should().Contain("meepleai_quality_score_count");
        scrape.Should().Contain("# TYPE meepleai_quality_score histogram");
    }

    [Fact]
    public async Task QualityScore_CarriesTheDimensionLabelUsedByTheRules()
    {
        var scrape = await ScrapeAfterRecordingAsync();

        scrape.Should().Contain("dimension=\"overall_confidence\"");
        scrape.Should().Contain("dimension=\"rag_confidence\"");
        scrape.Should().Contain("dimension=\"llm_confidence\"");
        scrape.Should().Contain("dimension=\"citation_quality\"");
    }

    [Fact]
    public async Task LowQualityCounter_KeepsBothTheUnitAndTheTotalSuffix()
    {
        // L'istrumento e' "meepleai.quality.low_quality_responses.total" con unit "responses":
        // il nome NON termina con l'unit, quindi l'exporter appende "_responses" e poi "_total".
        // Il nome dichiarato in C# non e' quello scrapato: la recording rule deve usare questo.
        var scrape = await ScrapeAfterRecordingAsync(isLowQuality: true);

        scrape.Should().Contain("meepleai_quality_low_quality_responses_total_responses_total");
    }

    private static async Task<string> ScrapeAfterRecordingAsync(bool isLowQuality = false)
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder => webBuilder
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddMetrics();
                    services.AddSingleton<QualityMetrics>();
                    services.AddOpenTelemetry().WithMetrics(metrics => metrics
                        .AddMeter("MeepleAI.Api")
                        .AddPrometheusExporter());
                })
                .Configure(app => app.UseOpenTelemetryPrometheusScrapingEndpoint()))
            .StartAsync(TestContext.Current.CancellationToken);

        var qualityMetrics = host.Services.GetRequiredService<QualityMetrics>();
        qualityMetrics.RecordQualityScores(
            new QualityScores
            {
                RagConfidence = 0.81,
                LlmConfidence = 0.72,
                CitationQuality = 0.64,
                OverallConfidence = isLowQuality ? 0.42 : 0.75,
                IsLowQuality = isLowQuality
            },
            agentType: "qa",
            operation: "answer");

        using var client = host.GetTestClient();
        return await client.GetStringAsync("/metrics", TestContext.Current.CancellationToken);
    }
}

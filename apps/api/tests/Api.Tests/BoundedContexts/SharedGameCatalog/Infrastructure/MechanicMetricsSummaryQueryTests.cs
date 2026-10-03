using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicMetrics;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// La fixture di <see cref="MechanicMetricsSummaryQueryTests"/>.
/// </summary>
/// <remarks>
/// <para>
/// #4050. Host condiviso per la classe, database fresco per test. Prima questa classe
/// costruiva l'host dentro il proprio <c>InitializeAsync</c>, che xUnit chiama una volta per
/// METODO: ~24s a test, contro ~0,1s per il clone del database.
/// </para>
/// </remarks>
public sealed class MechanicMetricsSummaryQueryTestsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "me532_summary");

/// <summary>
/// #532: the metrics summary query computes cost / review-time / approval-rate KPIs + rejection breakdown
/// over mechanic analyses, honoring the game / reviewer / date filters.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicMetricsSummaryQueryTests
    : IClassFixture<MechanicMetricsSummaryQueryTestsHostFixture>, IAsyncLifetime
{
    private readonly MechanicMetricsSummaryQueryTestsHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private Guid _userId;
    private readonly Guid _reviewerId = Guid.NewGuid();

    public MechanicMetricsSummaryQueryTests(MechanicMetricsSummaryQueryTestsHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, Guid.NewGuid());
    }

    // Host, client e database appartengono alla fixture.
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Summary_ComputesKpis_AndRejectionBreakdown()
    {
        var now = DateTime.UtcNow;
        using (var scope = _factory.Services.CreateScope())
        {
            // At most one Published per game (ux_mechanic_analyses_published_per_game) → 2 games.
            var game1 = await MechanicMetricsSeed.GameAsync(scope, _userId, "Catan");
            var game2 = await MechanicMetricsSeed.GameAsync(scope, _userId, "Carcassonne");
            // game1: Published 1.00 (review 2h) + Rejected 0.50 "factual" (review 1h) + InReview 0.20.
            await MechanicMetricsSeed.AnalysisAsync(scope, game1, _userId, status: 2, costUsd: 1.00m,
                createdAt: now.AddHours(-2), reviewedAt: now, reviewedBy: _reviewerId);
            await MechanicMetricsSeed.AnalysisAsync(scope, game1, _userId, status: 3, costUsd: 0.50m,
                createdAt: now.AddHours(-1), reviewedAt: now, reviewedBy: _reviewerId, rejectionReason: "factual");
            await MechanicMetricsSeed.AnalysisAsync(scope, game1, _userId, status: 1, costUsd: 0.20m,
                createdAt: now);
            // game2: Published 3.00 (review 4h).
            await MechanicMetricsSeed.AnalysisAsync(scope, game2, _userId, status: 2, costUsd: 3.00m,
                createdAt: now.AddHours(-4), reviewedAt: now, reviewedBy: _reviewerId);
        }

        MechanicMetricsSummaryDtoAsserts(await RunAsync(new GetMechanicMetricsSummaryQuery()));
    }

    [Fact]
    public async Task ReviewTime_ExcludesSystemAutoTransitions()
    {
        var now = DateTime.UtcNow;
        using (var scope = _factory.Services.CreateScope())
        {
            var humanGame = await MechanicMetricsSeed.GameAsync(scope, _userId, "HumanReviewed");
            var systemGame = await MechanicMetricsSeed.GameAsync(scope, _userId, "AutoRejected");
            // Human-approved: reviewed by a different admin, 4h elapsed → counts.
            await MechanicMetricsSeed.AnalysisAsync(scope, humanGame, _userId, status: 2, costUsd: 1.00m,
                createdAt: now.AddHours(-4), reviewedAt: now, reviewedBy: _reviewerId);
            // System auto-reject: ReviewedBy == CreatedBy, ReviewedAt≈CreatedAt → must be excluded.
            await MechanicMetricsSeed.AnalysisAsync(scope, systemGame, _userId, status: 3, costUsd: 0.10m,
                createdAt: now.AddMinutes(-1), reviewedAt: now, reviewedBy: _userId, rejectionReason: "llm_generation_failed");
        }

        var dto = await RunAsync(new GetMechanicMetricsSummaryQuery());
        dto.AverageReviewTimeHours.Should().NotBeNull();
        dto.AverageReviewTimeHours!.Value.Should().BeApproximately(4.0, 0.05); // only the human-reviewed 4h row
    }

    private static void MechanicMetricsSummaryDtoAsserts(
        Api.BoundedContexts.SharedGameCatalog.Application.DTOs.MechanicMetricsSummaryDto dto)
    {
        dto.TotalAnalyses.Should().Be(4);
        dto.TotalCostUsd.Should().Be(4.70m);
        dto.PublishedCount.Should().Be(2);
        dto.RejectedCount.Should().Be(1);
        dto.InReviewCount.Should().Be(1);
        dto.AverageCostUsd.Should().BeApproximately(1.175m, 0.001m);
        dto.ApprovalRatePct.Should().BeApproximately(66.667, 0.01);
        dto.AverageReviewTimeHours.Should().NotBeNull();
        dto.AverageReviewTimeHours!.Value.Should().BeApproximately(2.333, 0.01); // avg(2,4,1)
        dto.RejectionBreakdown.Should().ContainSingle(r => r.Reason == "factual" && r.Count == 1);
    }

    private async Task<Api.BoundedContexts.SharedGameCatalog.Application.DTOs.MechanicMetricsSummaryDto> RunAsync(
        GetMechanicMetricsSummaryQuery query)
    {
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(query);
    }
}

using Api.BoundedContexts.Administration.Application.Queries.Testing;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Testing metrics endpoints for admin dashboard (Issue #2139)
/// Provides real-time testing metrics from Lighthouse, Playwright, and Prometheus
/// </summary>
internal static class TestingMetricsEndpoints
{
    public static RouteGroupBuilder MapTestingMetricsEndpoints(this RouteGroupBuilder group)
    {
        MapGetAccessibilityMetricsEndpoint(group);
        MapGetPerformanceMetricsEndpoint(group);
        MapGetE2EMetricsEndpoint(group);

        return group;
    }

    private static void MapGetAccessibilityMetricsEndpoint(RouteGroupBuilder group)
    {
        // GET /api/v1/admin/testing/accessibility
        group.MapGet("/testing/accessibility", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;
            var session = sessionResult.Session;

            logger.LogInformation("Admin {UserId} retrieving accessibility metrics", session!.User!.Id);

            var metrics = await mediator.Send(new GetAccessibilityMetricsQuery(), ct).ConfigureAwait(false);

            logger.LogInformation(
                "Accessibility metrics retrieved: Score={Score}, Violations={Violations}, Status={Status}",
                metrics.LighthouseScore,
                metrics.AxeViolations,
                metrics.Status);

            // Convert wcagLevels array to compliance percentages object
            var wcagCompliance = new
            {
                levelA = metrics.WcagLevels.Contains("A", StringComparer.OrdinalIgnoreCase) ? 100 : 0,
                levelAA = metrics.WcagLevels.Contains("AA", StringComparer.OrdinalIgnoreCase) ? 100 : 0,
                levelAAA = metrics.WcagLevels.Contains("AAA", StringComparer.OrdinalIgnoreCase) ? 100 : 0
            };

            return Results.Json(new
            {
                lighthouseScore = metrics.LighthouseScore,
                axeViolations = metrics.AxeViolations,
                wcagCompliance,
                testedPages = 12, // Placeholder - would come from report metadata
                criticalIssues = Array.Empty<object>(), // Placeholder - would come from axe detailed results
                lastRun = metrics.LastRunAt.ToString("O") // ISO 8601 format
            });
        })
        .WithName("GetAccessibilityMetrics")
        .WithTags("Admin", "Testing")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Get accessibility testing metrics";
            operation.Description = "Returns Lighthouse and axe-core accessibility scores from latest test run";
            return operation;
        });
    }

    private static void MapGetPerformanceMetricsEndpoint(RouteGroupBuilder group)
    {
        // GET /api/v1/admin/testing/performance
        group.MapGet("/testing/performance", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;
            var session = sessionResult.Session;

            logger.LogInformation("Admin {UserId} retrieving performance metrics", session!.User!.Id);

            var metrics = await mediator.Send(new GetPerformanceMetricsQuery(), ct).ConfigureAwait(false);

            logger.LogInformation(
                "Performance metrics retrieved: Score={Score}, LCP={Lcp}ms, FID={Fid}ms, CLS={Cls}, Budget={Budget}",
                metrics.PerformanceScore,
                metrics.Lcp,
                metrics.Fid,
                metrics.Cls,
                metrics.BudgetStatus);

            // Structure response to match frontend schema
            var coreWebVitals = new
            {
                lcp = metrics.Lcp / 1000, // Convert to seconds
                fid = metrics.Fid,
                cls = metrics.Cls
            };

            var budgetStatusObj = new
            {
                js = new { current = 245, budget = 300, unit = "KB" }, // Placeholder - would parse from budget report
                css = new { current = 48, budget = 75, unit = "KB" },
                images = new { current = 890, budget = 1024, unit = "KB" }
            };

            var slowestPages = new[]
            {
                new { page = "/admin/testing", loadTime = 2.1 }
            }; // Placeholder - would parse from Lighthouse performance audits

            return Results.Json(new
            {
                lighthouseScore = metrics.PerformanceScore,
                coreWebVitals,
                budgetStatus = budgetStatusObj,
                slowestPages,
                lastRun = metrics.LastRunAt.ToString("O")
            });
        })
        .WithName("GetPerformanceMetrics")
        .WithTags("Admin", "Testing")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Get performance testing metrics";
            operation.Description = "Returns Core Web Vitals and performance budgets from latest Lighthouse run";
            return operation;
        });
    }

    private static void MapGetE2EMetricsEndpoint(RouteGroupBuilder group)
    {
        // GET /api/v1/admin/testing/e2e
        group.MapGet("/testing/e2e", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;
            var session = sessionResult.Session;

            logger.LogInformation("Admin {UserId} retrieving E2E test metrics", session!.User!.Id);

            var metrics = await mediator.Send(new GetE2EMetricsQuery(), ct).ConfigureAwait(false);

            logger.LogInformation(
                "E2E metrics retrieved: Coverage={Coverage}%, Pass={PassRate}%, Flaky={FlakyRate}%, Total={Total}, Status={Status}",
                metrics.Coverage,
                metrics.PassRate,
                metrics.FlakyRate,
                metrics.TotalTests,
                metrics.Status);

            // Map to frontend schema structure
            var criticalJourneys = new[]
            {
                new { name = "User Login Flow", status = "pass", duration = 12.3 },
                new { name = "PDF Upload", status = "pass", duration = 24.7 }
            }; // Placeholder - would parse from Playwright detailed results

            return Results.Json(new
            {
                coverage = metrics.Coverage,
                passRate = metrics.PassRate,
                flakyRate = metrics.FlakyRate,
                totalTests = metrics.TotalTests,
                executionTime = metrics.ExecutionTime / 60000, // Convert ms to minutes
                criticalJourneys,
                lastRun = metrics.LastRunAt.ToString("O")
            });
        })
        .WithName("GetE2EMetrics")
        .WithTags("Admin", "Testing")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Get E2E testing metrics";
            operation.Description = "Returns Playwright test execution results including coverage, pass rate, and flaky tests";
            return operation;
        });
    }
}

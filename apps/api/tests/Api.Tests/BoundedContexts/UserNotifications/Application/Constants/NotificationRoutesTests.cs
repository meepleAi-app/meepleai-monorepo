using System.Reflection;
using Api.BoundedContexts.UserNotifications.Application.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Constants;

/// <summary>
/// ADR-075 (#2996): pins the BE <see cref="NotificationRoutes"/> value set and verifies the builders
/// substitute the {id} token correctly. The FE twin (notification-routes.test.ts) pins the same golden
/// set on the FE side; the cross-language hash gate (scripts/lint-cross-lang-constants.sh) is the
/// authoritative BE↔FE parity check in CI. If any of the three drift, one of them fails.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
public sealed class NotificationRoutesTests
{
    // Golden set — MUST equal the string values in apps/web/src/lib/constants/notification-routes.ts.
    private static readonly string[] GoldenRouteValues =
    {
        // parameterized templates
        "/library/games/{id}/agent",
        "/library/private/{id}/toolkit",
        "/contributions/requests/{id}",
        "/shared-games/{id}",
        "/admin/shared-games/{id}",
        "/admin/approval-queue?gameId={id}",
        "/documents/{id}",
        "/admin/share-requests/{id}",
        "/admin/mechanic-analyses/{id}/review",
        "/game-nights/{id}",
        "/games/{id}",
        // static routes
        "/dashboard",
        "/account/suspended",
        "/contributions",
        "/contributions/requests?status=pending",
        "/admin/knowledge-base/queue",
        "/admin/knowledge-base/mechanic-extractor/dashboard",
        "/users/me/badges",
        "/settings/subscription",
        "/admin/agents/usage",
        "/admin/agents/strategy",
        "/admin/share-requests?sort=oldest",
        "/settings/notifications",
        "/library",
        "/achievements",
        "/sessions",
    };

    private static IEnumerable<string> ReflectedConstValues() =>
        typeof(NotificationRoutes)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void RouteConstants_MatchGoldenValueSet()
    {
        ReflectedConstValues().Should().BeEquivalentTo(GoldenRouteValues);
    }

    [Fact]
    public void Builders_SubstituteIdToken_ProducingExactPaths()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var s = id.ToString();

        NotificationRoutes.LibraryAgent(id).Should().Be($"/library/games/{s}/agent");
        NotificationRoutes.PrivateToolkit(id).Should().Be($"/library/private/{s}/toolkit");
        NotificationRoutes.ContributionRequest(id).Should().Be($"/contributions/requests/{s}");
        NotificationRoutes.SharedGame(id).Should().Be($"/shared-games/{s}");
        NotificationRoutes.AdminSharedGame(id).Should().Be($"/admin/shared-games/{s}");
        NotificationRoutes.AdminApprovalQueue(id).Should().Be($"/admin/approval-queue?gameId={s}");
        NotificationRoutes.Document(id).Should().Be($"/documents/{s}");
        NotificationRoutes.AdminShareRequest(id).Should().Be($"/admin/share-requests/{s}");
        NotificationRoutes.AdminMechanicAnalysisReview(id).Should().Be($"/admin/mechanic-analyses/{s}/review");
        NotificationRoutes.GameNight(id).Should().Be($"/game-nights/{s}");
        NotificationRoutes.Game(id).Should().Be($"/games/{s}");
    }
}

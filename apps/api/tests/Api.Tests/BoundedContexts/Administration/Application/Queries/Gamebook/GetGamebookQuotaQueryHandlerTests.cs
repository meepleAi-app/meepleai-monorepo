using Api.BoundedContexts.Administration.Application.Queries.Gamebook;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries.Gamebook;

/// <summary>#2750 (C14): maps the tier gamebook quota snapshot to the FE-facing DTO.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public class GetGamebookQuotaQueryHandlerTests
{
    [Fact]
    public async Task Handle_MapsSnapshotToDto_WithIsoResetDate()
    {
        var userId = Guid.NewGuid();
        var reset = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var tierService = new Mock<ITierEnforcementService>();
        tierService
            .Setup(s => s.GetGamebookQuotaSnapshotAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GamebookQuotaSnapshot(
                TranslationsThisMonth: 12,
                MaxTranslationsPerMonth: 50,
                ResetDate: reset,
                Tier: "free"));

        var handler = new GetGamebookQuotaQueryHandler(tierService.Object);

        var dto = await handler.Handle(new GetGamebookQuotaQuery(userId), CancellationToken.None);

        dto.Used.Should().Be(12);
        dto.Total.Should().Be(50);
        dto.Tier.Should().Be("free");
        // FE QuotaInfo.resetDate is a Zod z.string().datetime() — must be ISO-8601 UTC with millis.
        dto.ResetDate.Should().Be("2026-08-01T00:00:00.000Z");
    }

    [Fact]
    public void Constructor_NullService_Throws()
    {
        var act = () => new GetGamebookQuotaQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

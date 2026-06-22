using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Unit tests for <see cref="GetWikidataDeadLetterAttemptsQueryHandler"/>.
/// Issue #1823 Wave 3 M13.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class GetWikidataDeadLetterAttemptsQueryHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IWikidataCoverEnrichmentAttemptRepository> _attempts = new();

    private GetWikidataDeadLetterAttemptsQueryHandler Sut() => new(_attempts.Object);

    // Task 3.3 (#2254/#2255) — IncludeAcknowledged now lives on the query DTO
    // (default false) and propagates straight to the repo. The DTO also carries
    // the F5 ack metadata + F6 admin attribution fields surfaced via the repo
    // LEFT JOIN on Users.

    [Fact]
    public async Task Handle_EmptyResult_ReturnsZeroItems()
    {
        _attempts.Setup(r => r.GetDeadLettersAsync(0, 50, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeadLetterPage(Array.Empty<DeadLetterRow>(), 0));

        var result = await Sut().Handle(new GetWikidataDeadLetterAttemptsQuery(0, 50, null), default);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Skip.Should().Be(0);
        result.Take.Should().Be(50);
    }

    [Fact]
    public async Task Handle_HappyPath_MapsRepoRowsToDto()
    {
        var row = new DeadLetterRow(
            Id: Guid.NewGuid(),
            SharedGameId: Guid.NewGuid(),
            GameTitle: "Brass Birmingham",
            AttemptedAt: FixedNow.AddHours(-1),
            DeadLetteredAt: FixedNow,
            Reason: "r2-upload-error",
            Details: "503",
            RetryCount: 3,
            AcknowledgedAt: null,
            AcknowledgedBy: null,
            AcknowledgedByFullName: null,
            TriggeredByAdminUserId: null,
            TriggeredByAdminFullName: null);

        _attempts.Setup(r => r.GetDeadLettersAsync(0, 50, "r2-upload-error", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeadLetterPage(new[] { row }, TotalCount: 1));

        var result = await Sut().Handle(
            new GetWikidataDeadLetterAttemptsQuery(0, 50, "r2-upload-error"),
            default);

        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        var dto = result.Items[0];
        dto.Id.Should().Be(row.Id);
        dto.SharedGameId.Should().Be(row.SharedGameId);
        dto.GameTitle.Should().Be("Brass Birmingham");
        dto.Reason.Should().Be("r2-upload-error");
        dto.Details.Should().Be("503");
        dto.RetryCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_NegativeSkip_ClampsToZero()
    {
        _attempts.Setup(r => r.GetDeadLettersAsync(0, 50, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeadLetterPage(Array.Empty<DeadLetterRow>(), 0));

        await Sut().Handle(new GetWikidataDeadLetterAttemptsQuery(-5, 50, null), default);

        _attempts.Verify(r => r.GetDeadLettersAsync(0, 50, null, false, It.IsAny<CancellationToken>()), Times.Once,
            "handler must clamp negative skip to 0 before delegating to the repo");
    }

    [Theory]
    [InlineData(0, 1)]      // take=0 clamps to 1
    [InlineData(-10, 1)]    // take=-10 clamps to 1
    [InlineData(1000, 200)] // take=1000 clamps to 200
    public async Task Handle_TakeOutOfRange_Clamped(int requestTake, int expectedTake)
    {
        _attempts.Setup(r => r.GetDeadLettersAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeadLetterPage(Array.Empty<DeadLetterRow>(), 0));

        await Sut().Handle(new GetWikidataDeadLetterAttemptsQuery(0, requestTake, null), default);

        _attempts.Verify(r => r.GetDeadLettersAsync(0, expectedTake, null, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullQuery_Throws()
    {
        var act = async () => await Sut().Handle(null!, default);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // Task 3.3 — IncludeAcknowledged exposed on query DTO -------------------

    [Fact]
    public async Task DefaultIncludeAcknowledgedFalse_PassedToRepo()
    {
        _attempts.Setup(r => r.GetDeadLettersAsync(0, 50, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeadLetterPage(Array.Empty<DeadLetterRow>(), 0));

        var query = new GetWikidataDeadLetterAttemptsQuery(0, 50, null, IncludeAcknowledged: false);
        await Sut().Handle(query, CancellationToken.None);

        _attempts.Verify(
            r => r.GetDeadLettersAsync(0, 50, null, false, It.IsAny<CancellationToken>()),
            Times.Once,
            "default IncludeAcknowledged=false must propagate straight to the repo");
    }

    [Fact]
    public async Task IncludeAcknowledgedTrue_PassedThrough()
    {
        _attempts.Setup(r => r.GetDeadLettersAsync(0, 50, null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeadLetterPage(Array.Empty<DeadLetterRow>(), 0));

        var query = new GetWikidataDeadLetterAttemptsQuery(0, 50, null, IncludeAcknowledged: true);
        await Sut().Handle(query, CancellationToken.None);

        _attempts.Verify(
            r => r.GetDeadLettersAsync(0, 50, null, true, It.IsAny<CancellationToken>()),
            Times.Once,
            "IncludeAcknowledged=true must reach the repo so the audit view sees ack'd rows");
    }

    [Fact]
    public async Task DtoIncludesAckAndAdminFields()
    {
        var ackAt = FixedNow.AddMinutes(-15);
        var ackBy = Guid.NewGuid();
        var triggeredBy = Guid.NewGuid();
        var row = new DeadLetterRow(
            Id: Guid.NewGuid(),
            SharedGameId: Guid.NewGuid(),
            GameTitle: "Game",
            AttemptedAt: FixedNow.AddHours(-1),
            DeadLetteredAt: FixedNow,
            Reason: "r2-upload-error",
            Details: null,
            RetryCount: 3,
            AcknowledgedAt: ackAt,
            AcknowledgedBy: ackBy,
            AcknowledgedByFullName: "Alice",
            TriggeredByAdminUserId: triggeredBy,
            TriggeredByAdminFullName: "Bob");

        _attempts.Setup(r => r.GetDeadLettersAsync(0, 50, null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeadLetterPage(new[] { row }, 1));

        var query = new GetWikidataDeadLetterAttemptsQuery(0, 50, null, IncludeAcknowledged: true);
        var result = await Sut().Handle(query, CancellationToken.None);

        var dto = result.Items.Single();
        dto.AcknowledgedAt.Should().Be(ackAt);
        dto.AcknowledgedBy.Should().Be(ackBy);
        dto.AcknowledgedByFullName.Should().Be("Alice");
        dto.TriggeredByAdminUserId.Should().Be(triggeredBy);
        dto.TriggeredByAdminFullName.Should().Be("Bob");
    }
}

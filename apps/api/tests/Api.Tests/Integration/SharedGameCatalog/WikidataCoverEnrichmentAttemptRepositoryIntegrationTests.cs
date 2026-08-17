using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Integration tests for <see cref="WikidataCoverEnrichmentAttemptRepository"/>
/// against a real PostgreSQL via Testcontainers. Validates the LINQ → SQL
/// translation that the unit tests cannot exercise (correlated EXISTS sub-query
/// for due-for-enrichment, LEFT JOIN with IgnoreQueryFilters for dead-letter
/// page, partial-index-covered DELETE for retention).
///
/// Issue #1823 Wave 3 M16 (per Lisa Crispin spec-panel recommendation).
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
[Collection("Integration-GroupD")]
public class WikidataCoverEnrichmentAttemptRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext? _dbContext;
    private string? _connectionString;
    private readonly string _databaseName = $"test_wcea_repo_{Guid.NewGuid():N}";
    private WikidataCoverEnrichmentAttemptRepository _sut = null!;

    private static readonly DateTime FixedNow = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);

    public WikidataCoverEnrichmentAttemptRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(_connectionString);
        _dbContext = await Api.Tests.Infrastructure.TestHelpers.CreateDbContextAndMigrateAsync(_connectionString);
        _sut = new WikidataCoverEnrichmentAttemptRepository(_dbContext, new Mock<IDomainEventCollector>().Object);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ──────────────────────────────────────────────────────────────────────
    // GetGameIdsDueForEnrichmentAsync — correlated EXISTS sub-query
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGameIdsDueForEnrichmentAsync_NeverAttempted_ReturnsGame()
    {
        var game = SeedGame(qid: "Q1");
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var due = await _sut.GetGameIdsDueForEnrichmentAsync(
            limit: 10, nowUtc: FixedNow,
            cancellationToken: TestContext.Current.CancellationToken);

        due.Should().ContainSingle().Which.Should().Be(game.Id);
    }

    [Fact]
    public async Task GetGameIdsDueForEnrichmentAsync_GameWithoutQid_NotDue()
    {
        SeedGame(qid: null);
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var due = await _sut.GetGameIdsDueForEnrichmentAsync(
            limit: 10, nowUtc: FixedNow,
            cancellationToken: TestContext.Current.CancellationToken);

        due.Should().BeEmpty("games without WikidataQid are NOT enrichment targets");
    }

    [Fact]
    public async Task GetGameIdsDueForEnrichmentAsync_TerminalSuccess_ExcludesGame()
    {
        var game = SeedGame(qid: "Q1");
        SeedAttempt(game.Id, WikidataCoverEnrichmentOutcome.Success, reason: "success",
            attemptedAt: FixedNow.AddMinutes(-10));
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var due = await _sut.GetGameIdsDueForEnrichmentAsync(
            limit: 10, nowUtc: FixedNow,
            cancellationToken: TestContext.Current.CancellationToken);

        due.Should().BeEmpty("terminal Success keeps the game out of the rerun queue");
    }

    [Fact]
    public async Task GetGameIdsDueForEnrichmentAsync_TerminalDeadLetter_ExcludesGame()
    {
        var game = SeedGame(qid: "Q1");
        SeedAttempt(game.Id, WikidataCoverEnrichmentOutcome.DeadLetter, reason: "r2-upload-error",
            attemptedAt: FixedNow.AddMinutes(-10), deadLetteredAt: FixedNow.AddMinutes(-10));
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var due = await _sut.GetGameIdsDueForEnrichmentAsync(
            limit: 10, nowUtc: FixedNow,
            cancellationToken: TestContext.Current.CancellationToken);

        due.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGameIdsDueForEnrichmentAsync_FailedDueForRetry_IncludesGame()
    {
        var game = SeedGame(qid: "Q1");
        SeedAttempt(game.Id, WikidataCoverEnrichmentOutcome.Failed, reason: "r2-upload-error",
            attemptedAt: FixedNow.AddMinutes(-10), nextRetryAt: FixedNow.AddMinutes(-1));
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var due = await _sut.GetGameIdsDueForEnrichmentAsync(
            limit: 10, nowUtc: FixedNow,
            cancellationToken: TestContext.Current.CancellationToken);

        due.Should().ContainSingle().Which.Should().Be(game.Id);
    }

    [Fact]
    public async Task GetGameIdsDueForEnrichmentAsync_FailedNotYetDue_ExcludesGame()
    {
        var game = SeedGame(qid: "Q1");
        SeedAttempt(game.Id, WikidataCoverEnrichmentOutcome.Failed, reason: "r2-upload-error",
            attemptedAt: FixedNow.AddMinutes(-2), nextRetryAt: FixedNow.AddMinutes(10));
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var due = await _sut.GetGameIdsDueForEnrichmentAsync(
            limit: 10, nowUtc: FixedNow,
            cancellationToken: TestContext.Current.CancellationToken);

        due.Should().BeEmpty("NextRetryAt > now keeps the game in cooldown");
    }

    [Fact]
    public async Task GetGameIdsDueForEnrichmentAsync_LimitBatchSize()
    {
        for (var i = 0; i < 5; i++)
        {
            SeedGame(qid: $"Q{i}");
        }
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var due = await _sut.GetGameIdsDueForEnrichmentAsync(
            limit: 3, nowUtc: FixedNow,
            cancellationToken: TestContext.Current.CancellationToken);

        due.Should().HaveCount(3, "limit clamps the batch size");
    }

    // ──────────────────────────────────────────────────────────────────────
    // GetDeadLettersAsync — LEFT JOIN with IgnoreQueryFilters
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDeadLettersAsync_HappyPath_ReturnsRowsWithTitle()
    {
        var game = SeedGame(qid: "Q1", title: "Brass Birmingham");
        var attempt = SeedAttempt(game.Id, WikidataCoverEnrichmentOutcome.DeadLetter,
            reason: "r2-upload-error", attemptedAt: FixedNow.AddDays(-1),
            deadLetteredAt: FixedNow.AddDays(-1), details: "503");
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var page = await _sut.GetDeadLettersAsync(
            skip: 0, take: 50, reasonFilter: null,
            includeAcknowledged: false,
            cancellationToken: TestContext.Current.CancellationToken);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle();
        var row = page.Items[0];
        row.Id.Should().Be(attempt.Id);
        row.GameTitle.Should().Be("Brass Birmingham");
        row.Reason.Should().Be("r2-upload-error");
        row.Details.Should().Be("503");
    }

    [Fact]
    public async Task GetDeadLettersAsync_SoftDeletedGame_StillShowsRowWithFallbackTitle()
    {
        var game = SeedGame(qid: "Q1", title: "Lost Cities");
        SeedAttempt(game.Id, WikidataCoverEnrichmentOutcome.DeadLetter,
            reason: "license-not-whitelisted", attemptedAt: FixedNow.AddDays(-1),
            deadLetteredAt: FixedNow.AddDays(-1));
        // Soft-delete the parent game.
        game.IsDeleted = true;
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var page = await _sut.GetDeadLettersAsync(
            skip: 0, take: 50, reasonFilter: null,
            includeAcknowledged: false,
            cancellationToken: TestContext.Current.CancellationToken);

        page.TotalCount.Should().Be(1, "soft-deleting the game MUST NOT drop the attempt from the count");
        page.Items.Should().ContainSingle();
        page.Items[0].GameTitle.Should().Be("Lost Cities",
            "IgnoreQueryFilters() on the SharedGames join surfaces the soft-deleted title rather than producing the fallback '(deleted game)'");
    }

    [Fact]
    public async Task GetDeadLettersAsync_FilterByReason_ReturnsMatchingOnly()
    {
        var g1 = SeedGame(qid: "Q1");
        var g2 = SeedGame(qid: "Q2");
        SeedAttempt(g1.Id, WikidataCoverEnrichmentOutcome.DeadLetter, reason: "r2-upload-error",
            attemptedAt: FixedNow.AddDays(-1), deadLetteredAt: FixedNow.AddDays(-1));
        SeedAttempt(g2.Id, WikidataCoverEnrichmentOutcome.DeadLetter, reason: "image-processing-error",
            attemptedAt: FixedNow.AddDays(-1), deadLetteredAt: FixedNow.AddDays(-1));
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var page = await _sut.GetDeadLettersAsync(
            skip: 0, take: 50, reasonFilter: "image-processing-error",
            includeAcknowledged: false,
            cancellationToken: TestContext.Current.CancellationToken);

        page.TotalCount.Should().Be(1);
        page.Items[0].SharedGameId.Should().Be(g2.Id);
    }

    [Fact]
    public async Task GetDeadLettersAsync_OrdersByDeadLetteredAtDesc()
    {
        var g1 = SeedGame(qid: "Q1");
        var g2 = SeedGame(qid: "Q2");
        SeedAttempt(g1.Id, WikidataCoverEnrichmentOutcome.DeadLetter, reason: "r2-upload-error",
            attemptedAt: FixedNow.AddDays(-3), deadLetteredAt: FixedNow.AddDays(-3));
        SeedAttempt(g2.Id, WikidataCoverEnrichmentOutcome.DeadLetter, reason: "r2-upload-error",
            attemptedAt: FixedNow.AddDays(-1), deadLetteredAt: FixedNow.AddDays(-1));
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var page = await _sut.GetDeadLettersAsync(
            skip: 0, take: 50, reasonFilter: null,
            includeAcknowledged: false,
            cancellationToken: TestContext.Current.CancellationToken);

        page.Items.Should().HaveCount(2);
        page.Items[0].SharedGameId.Should().Be(g2.Id, "most-recently dead-lettered comes first");
        page.Items[1].SharedGameId.Should().Be(g1.Id);
    }

    [Fact]
    public async Task GetDeadLettersAsync_ExcludesLiveAttempts()
    {
        var game = SeedGame(qid: "Q1");
        SeedAttempt(game.Id, WikidataCoverEnrichmentOutcome.Failed, reason: "r2-upload-error",
            attemptedAt: FixedNow.AddMinutes(-5), nextRetryAt: FixedNow.AddMinutes(5));
        SeedAttempt(game.Id, WikidataCoverEnrichmentOutcome.Skipped, reason: "qid-missing",
            attemptedAt: FixedNow.AddDays(-1));
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var page = await _sut.GetDeadLettersAsync(
            skip: 0, take: 50, reasonFilter: null,
            includeAcknowledged: false,
            cancellationToken: TestContext.Current.CancellationToken);

        page.TotalCount.Should().Be(0,
            "only DeadLetter outcomes appear on the visibility page");
    }

    // ──────────────────────────────────────────────────────────────────────
    // DeleteDeadLetteredOlderThanAsync — ExecuteDeleteAsync on partial index
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteDeadLetteredOlderThanAsync_DeletesOldDeadLettersOnly()
    {
        var g1 = SeedGame(qid: "Q1");
        var g2 = SeedGame(qid: "Q2");
        var oldDl = SeedAttempt(g1.Id, WikidataCoverEnrichmentOutcome.DeadLetter,
            reason: "r2-upload-error",
            attemptedAt: FixedNow.AddDays(-10), deadLetteredAt: FixedNow.AddDays(-10));
        var freshDl = SeedAttempt(g2.Id, WikidataCoverEnrichmentOutcome.DeadLetter,
            reason: "r2-upload-error",
            attemptedAt: FixedNow.AddDays(-2), deadLetteredAt: FixedNow.AddDays(-2));
        var successRow = SeedAttempt(g1.Id, WikidataCoverEnrichmentOutcome.Success,
            reason: "success",
            attemptedAt: FixedNow.AddDays(-30));
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Cutoff = now - 7 days. oldDl (10 days) gets swept; freshDl (2 days) stays;
        // successRow (30 days) is untouched because DeadLetteredAt is null.
        var deleted = await _sut.DeleteDeadLetteredOlderThanAsync(
            cutoffUtc: FixedNow.AddDays(-7),
            cancellationToken: TestContext.Current.CancellationToken);

        deleted.Should().Be(1);

        var remaining = await _dbContext.WikidataCoverEnrichmentAttempts
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        remaining.Should().HaveCount(2);
        remaining.Should().NotContain(a => a.Id == oldDl.Id);
        remaining.Should().Contain(a => a.Id == freshDl.Id);
        remaining.Should().Contain(a => a.Id == successRow.Id,
            "Success rows must survive the dead-letter sweep regardless of age");
    }

    [Fact]
    public async Task DeleteDeadLetteredOlderThanAsync_EmptyTable_ReturnsZero()
    {
        var deleted = await _sut.DeleteDeadLetteredOlderThanAsync(
            cutoffUtc: FixedNow,
            cancellationToken: TestContext.Current.CancellationToken);

        deleted.Should().Be(0);
    }

    // ──────────────────────────────────────────────────────────────────────
    // GetLatestBySharedGameIdAsync — single-game lookup
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLatestBySharedGameIdAsync_ReturnsMostRecentAttempt()
    {
        var game = SeedGame(qid: "Q1");
        SeedAttempt(game.Id, WikidataCoverEnrichmentOutcome.Failed, reason: "r2-upload-error",
            attemptedAt: FixedNow.AddMinutes(-30), nextRetryAt: FixedNow.AddMinutes(-15));
        var latest = SeedAttempt(game.Id, WikidataCoverEnrichmentOutcome.Failed, reason: "r2-upload-error",
            attemptedAt: FixedNow.AddMinutes(-5), nextRetryAt: FixedNow.AddMinutes(1),
            retryCount: 1);
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _sut.GetLatestBySharedGameIdAsync(
            game.Id, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Id.Should().Be(latest.Id);
        result.RetryCount.Should().Be(1);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CountDeadLettersAsync — F1 (#1823 Wave 3) re-anchor signal
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CountDeadLettersAsync_EmptyTable_ReturnsZero()
    {
        var count = await _sut.CountDeadLettersAsync(TestContext.Current.CancellationToken);
        count.Should().Be(0);
    }

    [Fact]
    public async Task CountDeadLettersAsync_CountsOnlyDeadLetteredRows()
    {
        var g1 = SeedGame(qid: "Q1");
        var g2 = SeedGame(qid: "Q2");
        var g3 = SeedGame(qid: "Q3");

        // 2 actual dead-letters across two games
        SeedAttempt(g1.Id, WikidataCoverEnrichmentOutcome.DeadLetter,
            reason: "r2-upload-error",
            attemptedAt: FixedNow.AddDays(-1), deadLetteredAt: FixedNow.AddDays(-1));
        SeedAttempt(g2.Id, WikidataCoverEnrichmentOutcome.DeadLetter,
            reason: "image-processing-error",
            attemptedAt: FixedNow.AddDays(-2), deadLetteredAt: FixedNow.AddDays(-2));

        // Failed-with-retry MUST NOT be counted
        SeedAttempt(g3.Id, WikidataCoverEnrichmentOutcome.Failed,
            reason: "r2-upload-error",
            attemptedAt: FixedNow.AddMinutes(-5), nextRetryAt: FixedNow.AddMinutes(5));

        // Success MUST NOT be counted
        SeedAttempt(g1.Id, WikidataCoverEnrichmentOutcome.Success,
            reason: "success",
            attemptedAt: FixedNow.AddDays(-30));

        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var count = await _sut.CountDeadLettersAsync(TestContext.Current.CancellationToken);

        count.Should().Be(2,
            "F1 re-anchor MUST count only rows with DeadLetteredAt != NULL");
    }

    // ──────────────────────────────────────────────────────────────────────
    // F5 (#2254) — includeAcknowledged filter + Users JOIN for AcknowledgedByFullName
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDeadLettersAsync_DefaultExcludesAcknowledgedRows()
    {
        var g1 = SeedGame(qid: "Q1", title: "Acked Game");
        var g2 = SeedGame(qid: "Q2", title: "Open Game");
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dl1 = WikidataCoverEnrichmentAttempt.RecordDeadLetter(
            g1.Id, "r2-upload-error", details: null, retryCount: 3,
            attemptedAt: FixedNow.AddDays(-2));
        var dl2 = WikidataCoverEnrichmentAttempt.RecordDeadLetter(
            g2.Id, "r2-upload-error", details: null, retryCount: 3,
            attemptedAt: FixedNow.AddDays(-1));
        dl1.Acknowledge(Guid.NewGuid(), FixedNow.AddDays(-1));

        await _sut.AddAsync(dl1, TestContext.Current.CancellationToken);
        await _sut.AddAsync(dl2, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var page = await _sut.GetDeadLettersAsync(
            skip: 0, take: 50, reasonFilter: null,
            includeAcknowledged: false,
            cancellationToken: TestContext.Current.CancellationToken);

        page.TotalCount.Should().Be(1,
            "default view hides acknowledged dead-letters so operators see only open work");
        page.Items[0].SharedGameId.Should().Be(g2.Id);
    }

    [Fact]
    public async Task GetDeadLettersAsync_IncludeAcknowledgedTrue_IncludesThem()
    {
        var g1 = SeedGame(qid: "Q1", title: "Acked Game");
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dl = WikidataCoverEnrichmentAttempt.RecordDeadLetter(
            g1.Id, "r2-upload-error", details: null, retryCount: 3,
            attemptedAt: FixedNow.AddDays(-1));
        dl.Acknowledge(Guid.NewGuid(), FixedNow);
        await _sut.AddAsync(dl, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var page = await _sut.GetDeadLettersAsync(
            skip: 0, take: 50, reasonFilter: null,
            includeAcknowledged: true,
            cancellationToken: TestContext.Current.CancellationToken);

        page.TotalCount.Should().Be(1);
        page.Items[0].AcknowledgedAt.Should().NotBeNull();
        page.Items[0].AcknowledgedBy.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDeadLettersAsync_PopulatesAcknowledgedByDisplayName_FromUsersJoin()
    {
        // User entity uses DisplayName (nullable) rather than FullName; repo
        // exposes it via DeadLetterRow.AcknowledgedByFullName (semantic name
        // chosen by the F5 spec — the column populates from DisplayName).
        var admin = SeedUser(displayName: "Alice Admin");
        var g1 = SeedGame(qid: "Q1", title: "Game 1");
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dl = WikidataCoverEnrichmentAttempt.RecordDeadLetter(
            g1.Id, "r2-upload-error", details: null, retryCount: 3,
            attemptedAt: FixedNow.AddDays(-1));
        dl.Acknowledge(admin.Id, FixedNow);
        await _sut.AddAsync(dl, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var page = await _sut.GetDeadLettersAsync(
            skip: 0, take: 50, reasonFilter: null,
            includeAcknowledged: true,
            cancellationToken: TestContext.Current.CancellationToken);

        page.Items[0].AcknowledgedByFullName.Should().Be("Alice Admin",
            "LEFT JOIN on Users.DisplayName populates the denormalized admin name on the row");
    }

    // ──────────────────────────────────────────────────────────────────────
    // F6 (#2255) — GetAttemptsByGameIdAsync timeline rows with admin LEFT JOIN
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAttemptsByGameIdAsync_PopulatesTriggeredByAdminFullName()
    {
        var admin = SeedUser(displayName: "Bob Admin");
        var g1 = SeedGame(qid: "Q1", title: "Game 1");
        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var attempt = WikidataCoverEnrichmentAttempt.RecordSuccess(
            g1.Id, retryCount: 0, attemptedAt: FixedNow,
            triggeredByAdminUserId: admin.Id);
        await _sut.AddAsync(attempt, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var rows = await _sut.GetAttemptsByGameIdAsync(
            g1.Id, limit: 50,
            cancellationToken: TestContext.Current.CancellationToken);

        rows.Should().HaveCount(1);
        rows[0].TriggeredByAdminUserId.Should().Be(admin.Id);
        rows[0].TriggeredByAdminFullName.Should().Be("Bob Admin",
            "LEFT JOIN on Users.DisplayName surfaces the admin attribution on the F3 timeline drawer");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    private UserEntity SeedUser(string displayName)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid():N}@test.local",
            DisplayName = displayName,
            Role = "admin",
            Tier = "premium",
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext!.Users.Add(user);
        return user;
    }

    private SharedGameEntity SeedGame(string? qid, string? title = null)
    {
        var entity = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = title ?? $"Game {qid ?? Guid.NewGuid().ToString("N")}",
            YearPublished = 2020,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            Description = string.Empty,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WikidataQid = qid,
        };
        _dbContext!.SharedGames.Add(entity);
        return entity;
    }

    private WikidataCoverEnrichmentAttemptEntity SeedAttempt(
        Guid sharedGameId,
        WikidataCoverEnrichmentOutcome outcome,
        string reason,
        DateTime attemptedAt,
        DateTime? nextRetryAt = null,
        DateTime? deadLetteredAt = null,
        string? details = null,
        int retryCount = 0)
    {
        var entity = new WikidataCoverEnrichmentAttemptEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            AttemptedAt = attemptedAt,
            Outcome = (int)outcome,
            Reason = reason,
            Details = details,
            RetryCount = retryCount,
            NextRetryAt = nextRetryAt,
            DeadLetteredAt = deadLetteredAt,
        };
        _dbContext!.WikidataCoverEnrichmentAttempts.Add(entity);
        return entity;
    }
}

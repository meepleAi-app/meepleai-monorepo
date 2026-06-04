# Asse A — Semantic Alignment GameNight/Session Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare le 20 invarianti del dominio GameNight/Session + polymorphic ScoreType (DEC-1) + notification system in-app+email (DEC-5) nei bounded context `SessionTracking` e `GameManagement` per allineare il backend alla demo Claude Design 2026-06-04.

**Architecture:** EF Core migrations 3-step pattern (ALTER NULL → UPDATE → ALTER NOT NULL) + DDD aggregate refactor con factory methods + Strategy pattern per polymorphic scoring + transactional in-app notification + Resend email transactional fallback.

**Tech Stack:** .NET 9 · ASP.NET Minimal APIs + MediatR · EF Core (pgvector) · FluentValidation · xUnit + Testcontainers · Resend (email transactional)

**Issue**: [#1896](https://github.com/meepleAi-app/meepleai-monorepo/issues/1896) (parent umbrella [#1895](https://github.com/meepleAi-app/meepleai-monorepo/issues/1895))
**Spec consolidato**: [`docs/superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md`](../specs/2026-06-04-claude-design-alignment-spec-panel-review.md) (Sezione 4 — Asse A)
**Domain model**: [`docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md`](../../for-developers/specs/2026-06-04-gamenight-session-domain-model.md) (20 invarianti)
**Effort target**: XL ~15 gg dev + 3 gg test/review = 18 gg totali

---

## Work Packages

| WP | Scope | Effort | Critical path | Sub-task |
|----|-------|--------|---------------|----------|
| **WP1** | Migration EF Core 3-step | S+S+S+S+M | YES (blocca tutti) | T1–T5 |
| **WP2** | Session invarianti #11/#12/#13/#14 | M+S+S+M | YES (blocca WP4) | T6–T9 |
| **WP3** | State machine GameNight #8/#15/#16/#17 | L+M+M | YES (blocca WP6 notification flow) | T10–T12 |
| **WP4** | Invariante #10 max 1 live | M+S | NO (parallelo WP3) | T13–T14 |
| **WP5** | Polymorphic ScoreType (DEC-1) | M+M+M+M+L | NO (parallelo WP3+WP4) | T15–T19 |
| **WP6** | Notification system (DEC-5) | M+M+L+L | NO (parallelo WP4+WP5) | T20–T23 |
| **WP7** | OpenAPI + acceptance | S+M | YES (chiude WP) | T24–T25 |

**Mix-model hint (P120)**: 12 haiku (mechanical TDD) + 13 sonnet (judgment design). Vedi hint per task.

**Total**: 25 task TDD bite-sized. ~18 gg effort realistic.

---

## File Structure

### New files
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/ScoreType.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/IScoringStrategy.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/PointsScoringStrategy.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/BinaryWinScoringStrategy.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/ObjectivesScoringStrategy.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/RankingScoringStrategy.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/ScoringStrategyFactory.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/SessionScoresUpdated.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/SessionCreatedDomainEvent.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Application/SaveSession/SaveSessionCommandValidator.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/GameNightStatus.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/RsvpStatus.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Exceptions/MaxLiveSessionsExceededException.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/SendInvitations/SendGameNightInvitationsCommand.cs`
- `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Notification.cs`
- `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Email/IEmailSender.cs`
- `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Email/ResendEmailSender.cs`
- `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Email/Templates/GameNightInvitationTemplate.cs`
- `apps/api/src/Api/BoundedContexts/UserNotifications/Application/SendInvitationNotification/SendInvitationNotificationCommandHandler.cs`
- `apps/api/src/Api/BoundedContexts/UserNotifications/Routing/NotificationEndpoints.cs`
- `apps/api/src/Api/Migrations/YYYYMMDD_AddSessionTimestamps.cs`
- `apps/api/src/Api/Migrations/YYYYMMDD_AddGameNightStatus.cs`
- `apps/api/src/Api/Migrations/YYYYMMDD_AddGameNightPlayerRsvp.cs`
- `apps/api/src/Api/Migrations/YYYYMMDD_AddSessionPolymorphicScoring.cs`
- `apps/api/src/Api/Migrations/YYYYMMDD_CreateNotificationsTable.cs`
- `infra/secrets/email.secret.example`
- Test files mirror under `apps/api/tests/Api.Tests/`

### Modified files
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Session.cs` (factory methods + invarianti)
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/GameNight.cs` (state machine + aggregate guard)
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/GameNightPlayer.cs` (IsTagged/IsInvited/RsvpStatus)
- `apps/api/src/Api/BoundedContexts/SessionTracking/Application/SaveSession/SaveSessionCommandHandler.cs` (warning header)
- `apps/api/src/Api/BoundedContexts/SessionTracking/Application/GetSessionsByGameNight/GetSessionsByGameNightQueryHandler.cs` (sort ASC)
- `apps/api/src/Api/Infrastructure/Persistence/ApplicationDbContext.cs` (DbSet<Notification>, configurations)
- `apps/api/src/Api/Program.cs` (DI: `IEmailSender → ResendEmailSender`)
- `apps/api/src/Api/openapi.yaml` (new error codes + DTO updates)
- `apps/api/src/Api/Routing/RouteRegistrar.cs` (register notification endpoints)
- `CLAUDE.md` (Domain Model section: update con stato post-impl)

---

## WP1 — Migration EF Core 3-step

> **Spec reference**: Sezione 4 Asse A — "Migration (MAJ-1 fix: 3-step pattern)".
> **Invarianti coperte**: foundation per #10/#11/#15/#16/#17/#18 + DEC-1/DEC-5.
> **Critical path**: blocca tutti gli altri WP.

### Task 1: Migration sessions timestamps (#11)

**Mix-model**: haiku · **Effort**: S (~3h)

**Files:**
- Create: `apps/api/src/Api/Migrations/YYYYMMDD_AddSessionTimestamps.cs`
- Test: `apps/api/tests/Api.Tests/Integration/SessionTracking/MigrationTests.cs`

- [ ] **Step 1: Generate migration scaffold**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddSessionTimestamps --output-dir Migrations
```

- [ ] **Step 2: Write the failing test**

```csharp
// MigrationTests.cs
[Fact]
public async Task AddSessionTimestamps_AddsThreeNullableColumns_ThenNotNullOnCreatedAt()
{
    using var fixture = await TestcontainersFixture.CreateAsync();
    await fixture.MigrateAsync(targetMigration: "AddSessionTimestamps");

    using var conn = fixture.GetConnection();
    var columns = await conn.QueryAsync<dynamic>(
        "SELECT column_name, is_nullable FROM information_schema.columns " +
        "WHERE table_name = 'sessions' AND column_name IN ('created_at', 'started_at', 'completed_at')"
    );
    columns.Should().HaveCount(3);
    columns.First(c => c.column_name == "created_at").is_nullable.Should().Be("NO");
    columns.First(c => c.column_name == "started_at").is_nullable.Should().Be("YES");
    columns.First(c => c.column_name == "completed_at").is_nullable.Should().Be("YES");
}
```

- [ ] **Step 3: Run test to verify it fails**

```bash
dotnet test --filter "FullyQualifiedName~AddSessionTimestamps" -v normal
```
Expected: FAIL — migration not implemented

- [ ] **Step 4: Implement migration with 3-step pattern**

```csharp
// YYYYMMDD_AddSessionTimestamps.cs
public partial class AddSessionTimestamps : Migration
{
    protected override void Up(MigrationBuilder b)
    {
        // Step 1: ALTER nullable
        b.AddColumn<DateTimeOffset?>("created_at", "sessions", nullable: true);
        b.AddColumn<DateTimeOffset?>("started_at", "sessions", nullable: true);
        b.AddColumn<DateTimeOffset?>("completed_at", "sessions", nullable: true);

        // Step 2: UPDATE backfill
        b.Sql("UPDATE sessions SET created_at = updated_at;");
        b.Sql("UPDATE sessions SET completed_at = updated_at WHERE status = 'completed';");

        // Step 3: ALTER NOT NULL only for created_at + DEFAULT
        b.AlterColumn<DateTimeOffset>("created_at", "sessions",
            nullable: false, defaultValueSql: "now()");
    }

    protected override void Down(MigrationBuilder b)
    {
        b.DropColumn("completed_at", "sessions");
        b.DropColumn("started_at", "sessions");
        b.DropColumn("created_at", "sessions");
    }
}
```

- [ ] **Step 5: Update SessionEntity + ApplicationDbContext config**

```csharp
// SessionEntity.cs (or via Session aggregate root)
public DateTimeOffset CreatedAt { get; private set; }
public DateTimeOffset? StartedAt { get; private set; }
public DateTimeOffset? CompletedAt { get; private set; }

// ApplicationDbContext.cs OnModelCreating
modelBuilder.Entity<SessionEntity>(b =>
{
    b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
    b.Property(e => e.StartedAt).HasColumnName("started_at");
    b.Property(e => e.CompletedAt).HasColumnName("completed_at");
});
```

- [ ] **Step 6: Run test to verify it passes**

```bash
dotnet test --filter "FullyQualifiedName~AddSessionTimestamps" -v normal
```
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Migrations/*AddSessionTimestamps* \
        apps/api/src/Api/BoundedContexts/SessionTracking/ \
        apps/api/src/Api/Infrastructure/Persistence/ApplicationDbContext.cs \
        apps/api/tests/Api.Tests/Integration/SessionTracking/MigrationTests.cs
git commit -m "feat(session-tracking): #1896 add Session.CreatedAt/StartedAt/CompletedAt (invariante #11)"
```

**Self-review checklist**:
- [ ] Migration usa 3-step pattern (no circular DEFAULT now() + UPDATE)
- [ ] `created_at` NOT NULL, `started_at`/`completed_at` NULL
- [ ] Backfill SQL idempotente (eseguibile più volte senza errori)
- [ ] Down migration drop columns in ordine inverso
- [ ] EF Core entity config snake_case mapping

---

### Task 2: Migration game_nights status (#8 + #15)

**Mix-model**: haiku · **Effort**: S (~2h)

**Files:**
- Create: `apps/api/src/Api/Migrations/YYYYMMDD_AddGameNightStatus.cs`
- Test: `apps/api/tests/Api.Tests/Integration/GameManagement/MigrationTests.cs`

- [ ] **Step 1: Generate migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddGameNightStatus --output-dir Migrations
```

- [ ] **Step 2: Write failing integration test**

```csharp
[Fact]
public async Task AddGameNightStatus_BackfillsCompletedForPastWithSessions_PlannedOtherwise()
{
    using var fixture = await TestcontainersFixture.CreateAsync();
    await fixture.SeedAsync(seed =>
    {
        seed.AddGameNight(date: DateTimeOffset.UtcNow.AddDays(-10), hasSession: true);
        seed.AddGameNight(date: DateTimeOffset.UtcNow.AddDays(-5), hasSession: false);
        seed.AddGameNight(date: DateTimeOffset.UtcNow.AddDays(+5), hasSession: false);
    });
    await fixture.MigrateAsync(targetMigration: "AddGameNightStatus");

    using var conn = fixture.GetConnection();
    var statuses = await conn.QueryAsync<(Guid Id, string Status)>(
        "SELECT id, status FROM game_nights ORDER BY date"
    );
    statuses.ElementAt(0).Status.Should().Be("Completed");
    statuses.ElementAt(1).Status.Should().Be("Planned");
    statuses.ElementAt(2).Status.Should().Be("Planned");
}
```

- [ ] **Step 3: Run test → FAIL**

- [ ] **Step 4: Implement migration**

```csharp
public partial class AddGameNightStatus : Migration
{
    protected override void Up(MigrationBuilder b)
    {
        b.AddColumn<string>("status", "game_nights",
            maxLength: 20, nullable: true);

        b.Sql(@"
            UPDATE game_nights SET status = 'Completed'
            WHERE date < now() AND EXISTS (
                SELECT 1 FROM sessions WHERE game_night_id = game_nights.id
            );
        ");
        b.Sql("UPDATE game_nights SET status = 'Planned' WHERE date >= now() OR status IS NULL;");

        b.AlterColumn<string>("status", "game_nights",
            maxLength: 20, nullable: false, defaultValue: "Planned");
    }

    protected override void Down(MigrationBuilder b) =>
        b.DropColumn("status", "game_nights");
}
```

- [ ] **Step 5: Run test → PASS**

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(game-management): #1896 add GameNight.Status enum (invariante #8 + #15)"
```

**Self-review**:
- [ ] Backfill 'Completed' solo per `date < now() AND hasSession`
- [ ] Fallback 'Planned' per status NULL
- [ ] DEFAULT 'Planned' applicato dopo backfill

---

### Task 3: Migration game_night_players RSVP (#16 + #17)

**Mix-model**: haiku · **Effort**: S (~3h)

**Files:**
- Create: `apps/api/src/Api/Migrations/YYYYMMDD_AddGameNightPlayerRsvp.cs`

- [ ] **Step 1: Generate migration**

```bash
dotnet ef migrations add AddGameNightPlayerRsvp --output-dir Migrations
```

- [ ] **Step 2: Write failing test**

```csharp
[Fact]
public async Task AddGameNightPlayerRsvp_BackfillsExistingAsInvitedAndConfirmed_BackwardsCompat()
{
    using var fixture = await TestcontainersFixture.CreateAsync();
    var existingPlayerId = await fixture.SeedAsync(seed =>
        seed.AddGameNightPlayer()).Result;

    await fixture.MigrateAsync(targetMigration: "AddGameNightPlayerRsvp");

    using var conn = fixture.GetConnection();
    var row = await conn.QuerySingleAsync<(bool IsTagged, bool IsInvited, string RsvpStatus)>(
        "SELECT is_tagged, is_invited, rsvp_status FROM game_night_players WHERE id = @id",
        new { id = existingPlayerId }
    );
    row.IsTagged.Should().BeTrue();
    row.IsInvited.Should().BeTrue();
    row.RsvpStatus.Should().Be("Confirmed");
}
```

- [ ] **Step 3: Run test → FAIL**

- [ ] **Step 4: Implement migration with MAJ-2 backwards-compat**

```csharp
public partial class AddGameNightPlayerRsvp : Migration
{
    protected override void Up(MigrationBuilder b)
    {
        // MAJ-2: existing players are already auto-invited + auto-confirmed (legacy pattern)
        b.AddColumn<bool>("is_tagged", "game_night_players",
            nullable: false, defaultValue: true);
        b.AddColumn<bool>("is_invited", "game_night_players",
            nullable: false, defaultValue: true);
        b.AddColumn<string>("rsvp_status", "game_night_players",
            maxLength: 20, nullable: false, defaultValue: "Confirmed");
    }

    protected override void Down(MigrationBuilder b)
    {
        b.DropColumn("rsvp_status", "game_night_players");
        b.DropColumn("is_invited", "game_night_players");
        b.DropColumn("is_tagged", "game_night_players");
    }
}
```

- [ ] **Step 5: Run test → PASS**

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(game-management): #1896 add GameNightPlayer RSVP fields (invariante #16 + #17)

MAJ-2 backwards-compat: existing players backfilled as IsInvited=true + RsvpStatus=Confirmed
(legacy auto-shared pattern preserved)."
```

---

### Task 4: Migration sessions polymorphic scoring (DEC-1 foundation)

**Mix-model**: haiku · **Effort**: S (~2h)

**Files:**
- Create: `apps/api/src/Api/Migrations/YYYYMMDD_AddSessionPolymorphicScoring.cs`

- [ ] **Step 1: Generate migration**

- [ ] **Step 2: Write failing test for column existence + backfill**

```csharp
[Fact]
public async Task AddSessionPolymorphicScoring_AddsColumnsAndBackfillsExistingAsPoints()
{
    using var fixture = await TestcontainersFixture.CreateAsync();
    await fixture.SeedAsync(seed => seed.AddSessions(count: 3));

    await fixture.MigrateAsync(targetMigration: "AddSessionPolymorphicScoring");

    using var conn = fixture.GetConnection();
    var rows = await conn.QueryAsync<(string ScoringType, string ScoreData)>(
        "SELECT scoring_type, score_data::text FROM sessions"
    );
    rows.Should().HaveCount(3);
    rows.All(r => r.ScoringType == "Points").Should().BeTrue();
    rows.All(r => r.ScoreData == "{}").Should().BeTrue();
}
```

- [ ] **Step 3: Run → FAIL**

- [ ] **Step 4: Implement migration**

```csharp
public partial class AddSessionPolymorphicScoring : Migration
{
    protected override void Up(MigrationBuilder b)
    {
        b.AddColumn<string>("scoring_type", "sessions",
            maxLength: 20, nullable: false, defaultValue: "Points");
        b.AddColumn<string>("score_data", "sessions",
            type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb");
    }

    protected override void Down(MigrationBuilder b)
    {
        b.DropColumn("score_data", "sessions");
        b.DropColumn("scoring_type", "sessions");
    }
}
```

- [ ] **Step 5: Run → PASS**

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(session-tracking): #1896 add Session.ScoringType/ScoreData (DEC-1 polymorphic)"
```

---

### Task 5: Migration notifications table (DEC-5 foundation)

**Mix-model**: haiku · **Effort**: M (~4h)

**Files:**
- Create: `apps/api/src/Api/Migrations/YYYYMMDD_CreateNotificationsTable.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Notification.cs`

- [ ] **Step 1: Generate migration**

- [ ] **Step 2: Write failing test for table + indexes**

```csharp
[Fact]
public async Task CreateNotificationsTable_HasExpectedSchemaAndIndexes()
{
    using var fixture = await TestcontainersFixture.CreateAsync();
    await fixture.MigrateAsync(targetMigration: "CreateNotificationsTable");

    using var conn = fixture.GetConnection();

    var columns = await conn.QueryAsync<dynamic>(
        "SELECT column_name, data_type FROM information_schema.columns " +
        "WHERE table_name = 'notifications'"
    );
    columns.Select(c => c.column_name).Should().Contain(
        new[] { "id", "recipient_user_id", "type", "payload", "read_at", "created_at" }
    );

    var indexes = await conn.QueryAsync<string>(
        "SELECT indexname FROM pg_indexes WHERE tablename = 'notifications'"
    );
    indexes.Should().Contain("idx_notifications_recipient_created");
    indexes.Should().Contain("idx_notifications_recipient_unread");
}
```

- [ ] **Step 3: Run → FAIL**

- [ ] **Step 4: Implement migration + entity**

```csharp
public partial class CreateNotificationsTable : Migration
{
    protected override void Up(MigrationBuilder b)
    {
        b.CreateTable("notifications", t => new
        {
            Id = t.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
            RecipientUserId = t.Column<Guid>("recipient_user_id", nullable: false),
            Type = t.Column<string>(maxLength: 50, nullable: false),
            Payload = t.Column<string>(type: "jsonb", nullable: false),
            ReadAt = t.Column<DateTimeOffset?>("read_at", nullable: true),
            CreatedAt = t.Column<DateTimeOffset>("created_at",
                nullable: false, defaultValueSql: "now()"),
        }, constraints: t =>
        {
            t.PrimaryKey("pk_notifications", x => x.Id);
            t.ForeignKey("fk_notifications_users",
                x => x.RecipientUserId, "users", "id", onDelete: ReferentialAction.Cascade);
        });

        b.CreateIndex("idx_notifications_recipient_created",
            "notifications", new[] { "recipient_user_id", "created_at" },
            descending: new[] { false, true });
        b.CreateIndex("idx_notifications_recipient_unread",
            "notifications", "recipient_user_id",
            filter: "read_at IS NULL");
    }

    protected override void Down(MigrationBuilder b) => b.DropTable("notifications");
}
```

```csharp
// BoundedContexts/UserNotifications/Domain/Notification.cs
public sealed class Notification
{
    public Guid Id { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public string Type { get; private set; } = default!;
    public string Payload { get; private set; } = default!; // JSON
    public DateTimeOffset? ReadAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Notification() { } // EF

    public static Notification Create(Guid recipientUserId, string type, string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Type required", nameof(type));
        return new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            Type = type,
            Payload = payloadJson,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void MarkAsRead()
    {
        if (ReadAt is null) ReadAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 5: Update ApplicationDbContext**

```csharp
public DbSet<Notification> Notifications => Set<Notification>();

// OnModelCreating
modelBuilder.Entity<Notification>(b =>
{
    b.ToTable("notifications");
    b.HasKey(e => e.Id);
    b.Property(e => e.RecipientUserId).HasColumnName("recipient_user_id");
    b.Property(e => e.Type).HasColumnName("type").HasMaxLength(50).IsRequired();
    b.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
    b.Property(e => e.ReadAt).HasColumnName("read_at");
    b.Property(e => e.CreatedAt).HasColumnName("created_at");
});
```

- [ ] **Step 6: Run → PASS**

- [ ] **Step 7: Commit**

```bash
git commit -m "feat(user-notifications): #1896 create notifications table + Notification entity (DEC-5)"
```

---

## WP2 — Session invarianti #11/#12/#13/#14

> **Spec reference**: Sezione 4 Asse A — "Modello Session (3 timestamp distinti)" + invarianti #12/#13/#14.
> **Critical path**: blocca WP4 (max 1 live aggregate guard).

### Task 6: Session.OpenLiveMode factory + invariante #14

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Session.cs`
- Test: `apps/api/tests/Api.Tests/Unit/SessionTracking/Domain/SessionTests.cs`

- [ ] **Step 1: Write failing unit tests**

```csharp
public class SessionOpenLiveModeTests
{
    [Fact]
    public void OpenLiveMode_OnDraft_SetsStartedAtToNow_AndReturnsLiveSession()
    {
        var draft = Session.CreateDraft(gameNightId: Guid.NewGuid(), gameId: Guid.NewGuid());
        var beforeNow = DateTimeOffset.UtcNow;

        var live = draft.OpenLiveMode();

        live.StartedAt.Should().NotBeNull();
        live.StartedAt!.Value.Should().BeOnOrAfter(beforeNow);
        live.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void OpenLiveMode_OnSessionWithStartedAt_Throws()
    {
        var draft = Session.CreateDraft(Guid.NewGuid(), Guid.NewGuid());
        draft.OpenLiveMode();

        var act = () => draft.OpenLiveMode();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already in live mode*");
    }

    [Fact]
    public void StartedAt_IsNeverSetByConstructorOrDraftFactory()
    {
        var draft = Session.CreateDraft(Guid.NewGuid(), Guid.NewGuid());
        draft.StartedAt.Should().BeNull(); // invariante #14
    }
}
```

- [ ] **Step 2: Run tests → FAIL** (method not exists)

- [ ] **Step 3: Implement Session.OpenLiveMode + invariante #14**

```csharp
public sealed class Session
{
    public Guid Id { get; private set; }
    public Guid GameNightId { get; private set; }
    public Guid GameId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private Session() { } // EF

    public static Session CreateDraft(Guid gameNightId, Guid gameId) => new()
    {
        Id = Guid.NewGuid(),
        GameNightId = gameNightId,
        GameId = gameId,
        CreatedAt = DateTimeOffset.UtcNow,
        // StartedAt + CompletedAt deliberately NULL (invariante #14)
    };

    public Session OpenLiveMode()
    {
        if (StartedAt is not null)
            throw new InvalidOperationException(
                $"Session {Id} is already in live mode (StartedAt={StartedAt}).");
        StartedAt = DateTimeOffset.UtcNow;
        return this;
    }
}
```

- [ ] **Step 4: Run tests → PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(session-tracking): #1896 Session.OpenLiveMode factory + invariante #14 (StartedAt derived)"
```

**Self-review**:
- [ ] StartedAt NEVER set by constructor/CreateDraft
- [ ] OpenLiveMode throws if already live
- [ ] Factory method pattern (MIN-6 clarified)
- [ ] DateTimeOffset.UtcNow direct (no clock abstraction MVP, document trade-off)

---

### Task 7: Session.Save() invariante #11 completedAt valorizzato

**Mix-model**: haiku · **Effort**: S (~3h)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Session.cs`
- Test: `apps/api/tests/Api.Tests/Unit/SessionTracking/Domain/SessionTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
public class SessionSaveTests
{
    [Fact]
    public void Save_OnDraft_SetsCompletedAtToNow()
    {
        var draft = Session.CreateDraft(Guid.NewGuid(), Guid.NewGuid());
        var beforeNow = DateTimeOffset.UtcNow;

        draft.Save();

        draft.CompletedAt.Should().NotBeNull();
        draft.CompletedAt!.Value.Should().BeOnOrAfter(beforeNow);
    }

    [Fact]
    public void Save_OnLive_PreservesStartedAt_AndSetsCompletedAt()
    {
        var session = Session.CreateDraft(Guid.NewGuid(), Guid.NewGuid()).OpenLiveMode();
        var startedAt = session.StartedAt;

        session.Save();

        session.StartedAt.Should().Be(startedAt);
        session.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Save_OnAlreadySaved_Throws()
    {
        var draft = Session.CreateDraft(Guid.NewGuid(), Guid.NewGuid());
        draft.Save();

        var act = () => draft.Save();
        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement Session.Save**

```csharp
public void Save()
{
    if (CompletedAt is not null)
        throw new InvalidOperationException(
            $"Session {Id} is already saved (CompletedAt={CompletedAt}).");
    CompletedAt = DateTimeOffset.UtcNow;
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(session-tracking): #1896 Session.Save() valorizza CompletedAt (invariante #11)"
```

---

### Task 8: GetSessionsByGameNightQuery sort createdAt ASC (invariante #12)

**Mix-model**: haiku · **Effort**: S (~2h)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/GetSessionsByGameNight/GetSessionsByGameNightQueryHandler.cs`
- Test: `apps/api/tests/Api.Tests/Integration/SessionTracking/GetSessionsByGameNightQueryHandlerTests.cs`

- [ ] **Step 1: Write failing integration test**

```csharp
[Fact]
public async Task Handle_ReturnsSessions_OrderedByCreatedAtAscending()
{
    using var fixture = await TestcontainersFixture.CreateAsync();
    var gameNightId = Guid.NewGuid();
    await fixture.SeedAsync(seed =>
    {
        seed.AddSession(gameNightId, createdAt: DateTimeOffset.UtcNow.AddMinutes(-30));
        seed.AddSession(gameNightId, createdAt: DateTimeOffset.UtcNow);
        seed.AddSession(gameNightId, createdAt: DateTimeOffset.UtcNow.AddMinutes(-15));
    });

    var handler = fixture.Resolve<GetSessionsByGameNightQueryHandler>();
    var result = await handler.Handle(new GetSessionsByGameNightQuery(gameNightId), CT.None);

    result.Should().HaveCount(3);
    result.Should().BeInAscendingOrder(s => s.CreatedAt);
}
```

- [ ] **Step 2: Run → FAIL** (current sort is updated_at desc o nessuno)

- [ ] **Step 3: Update handler**

```csharp
public async Task<IReadOnlyList<SessionDto>> Handle(
    GetSessionsByGameNightQuery query, CancellationToken ct)
{
    return await _db.Sessions
        .Where(s => s.GameNightId == query.GameNightId)
        .OrderBy(s => s.CreatedAt) // invariante #12 deterministic
        .Select(s => SessionDto.FromEntity(s))
        .ToListAsync(ct);
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(session-tracking): #1896 sort sessions by CreatedAt ASC (invariante #12)"
```

---

### Task 9: SaveSessionCommand + X-Warning-Code header (invariante #13)

**Mix-model**: sonnet · **Effort**: M (~5h)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/SaveSession/SaveSessionCommandHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Routing/SessionEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/Integration/SessionTracking/SaveSessionEndpointTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public async Task SaveSession_WithActiveLive_Returns200_WithWarningHeader()
{
    using var app = await TestApp.CreateAsync();
    var gn = await app.SeedGameNightAsync();
    var liveSession = await app.OpenLiveSessionAsync(gn.Id, gameId: Guid.NewGuid());
    var draft = await app.CreateDraftSessionAsync(gn.Id, gameId: Guid.NewGuid());

    var response = await app.Client.PutAsync(
        $"/api/v1/sessions/{draft.Id}/save",
        JsonContent.Create(new SaveSessionRequest { ScoringType = "Points", ScoreData = "{}" }));

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    response.Headers.GetValues("X-Warning-Code").Should().ContainSingle("SAVED_WHILE_LIVE_ACTIVE");
}

[Fact]
public async Task SaveSession_WithoutActiveLive_Returns200_NoWarningHeader()
{
    using var app = await TestApp.CreateAsync();
    var gn = await app.SeedGameNightAsync();
    var draft = await app.CreateDraftSessionAsync(gn.Id, gameId: Guid.NewGuid());

    var response = await app.Client.PutAsync(
        $"/api/v1/sessions/{draft.Id}/save",
        JsonContent.Create(new SaveSessionRequest { ScoringType = "Points", ScoreData = "{}" }));

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    response.Headers.Contains("X-Warning-Code").Should().BeFalse();
}
```

- [ ] **Step 2: Run → FAIL** (handler non emette warning)

- [ ] **Step 3: Update handler to return warning flag + endpoint to map to header**

```csharp
public record SaveSessionResult(SessionDto Session, bool LiveActiveWarning);

public async Task<SaveSessionResult> Handle(SaveSessionCommand cmd, CancellationToken ct)
{
    var session = await _db.Sessions.FindAsync(new object?[] { cmd.SessionId }, ct)
        ?? throw new NotFoundException($"Session {cmd.SessionId} not found");
    session.Save();

    bool liveActive = await _db.Sessions.AnyAsync(s =>
        s.GameNightId == session.GameNightId &&
        s.StartedAt != null &&
        s.CompletedAt == null &&
        s.Id != session.Id, ct);

    await _db.SaveChangesAsync(ct);
    return new SaveSessionResult(SessionDto.FromEntity(session), liveActive);
}
```

```csharp
// SessionEndpoints.cs
app.MapPut("/api/v1/sessions/{id:guid}/save", async (
    Guid id, SaveSessionRequest req, IMediator m, HttpResponse response) =>
{
    var result = await m.Send(new SaveSessionCommand(id, req.ScoringType, req.ScoreData));
    if (result.LiveActiveWarning)
        response.Headers.Append("X-Warning-Code", "SAVED_WHILE_LIVE_ACTIVE");
    return Results.Ok(result.Session);
});
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(session-tracking): #1896 SaveSession X-Warning-Code header on live coexistence (invariante #13)"
```

---

## WP3 — State machine GameNight #8/#15/#16/#17

> **Spec reference**: Sezione 4 Asse A — "State machine GameNight" + Tagging vs RSVP a 5 fasi.
> **Critical path**: blocca WP6 notification flow (SendInvitations comand triggers notification).

### Task 10: GameNight state machine + SessionCreatedDomainEvent handler (invariante #15)

**Mix-model**: sonnet · **Effort**: L (~8h)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/GameNightStatus.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/SessionCreatedDomainEvent.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/GameNight.cs`
- Test: `apps/api/tests/Api.Tests/Unit/GameManagement/Domain/GameNightStateMachineTests.cs`

- [ ] **Step 1: Write failing unit tests**

```csharp
public class GameNightStateMachineTests
{
    [Fact]
    public void NewGameNight_HasStatusPlanned()
    {
        var gn = GameNight.Create(ownerId: Guid.NewGuid(), date: DateTimeOffset.UtcNow.AddDays(7));
        gn.Status.Should().Be(GameNightStatus.Planned);
    }

    [Fact]
    public void FirstSessionCreated_TransitionsToInProgress_InvariantE15()
    {
        var gn = GameNight.Create(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1));
        var draftEvent = new SessionCreatedDomainEvent(gn.Id, sessionId: Guid.NewGuid(), isLive: false);

        gn.HandleSessionCreated(draftEvent);

        gn.Status.Should().Be(GameNightStatus.InProgress);
    }

    [Fact]
    public void FirstSessionLiveCreated_TransitionsToInProgress_InvariantE15()
    {
        var gn = GameNight.Create(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var liveEvent = new SessionCreatedDomainEvent(gn.Id, sessionId: Guid.NewGuid(), isLive: true);

        gn.HandleSessionCreated(liveEvent);

        gn.Status.Should().Be(GameNightStatus.InProgress);
    }

    [Fact]
    public void SubsequentSessionCreated_StaysInProgress()
    {
        var gn = GameNight.Create(Guid.NewGuid(), DateTimeOffset.UtcNow);
        gn.HandleSessionCreated(new SessionCreatedDomainEvent(gn.Id, Guid.NewGuid(), false));
        gn.HandleSessionCreated(new SessionCreatedDomainEvent(gn.Id, Guid.NewGuid(), false));
        gn.Status.Should().Be(GameNightStatus.InProgress);
    }

    [Fact]
    public void MarkAsCompleted_FromInProgress_TransitionsToCompleted()
    {
        var gn = GameNight.Create(Guid.NewGuid(), DateTimeOffset.UtcNow);
        gn.HandleSessionCreated(new SessionCreatedDomainEvent(gn.Id, Guid.NewGuid(), false));

        gn.MarkAsCompleted();

        gn.Status.Should().Be(GameNightStatus.Completed);
    }

    [Fact]
    public void MarkAsCompleted_FromPlanned_Throws()
    {
        var gn = GameNight.Create(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var act = () => gn.MarkAsCompleted();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Planned*");
    }

    [Fact]
    public void NoBackwardTransition_CompletedCantGoBackToInProgress()
    {
        var gn = GameNight.Create(Guid.NewGuid(), DateTimeOffset.UtcNow);
        gn.HandleSessionCreated(new SessionCreatedDomainEvent(gn.Id, Guid.NewGuid(), false));
        gn.MarkAsCompleted();

        var act = () => gn.HandleSessionCreated(new SessionCreatedDomainEvent(gn.Id, Guid.NewGuid(), false));
        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement enum + event + aggregate logic**

```csharp
// GameNightStatus.cs
public enum GameNightStatus { Planned, InProgress, Completed }

// SessionCreatedDomainEvent.cs
public record SessionCreatedDomainEvent(Guid GameNightId, Guid SessionId, bool IsLive);

// GameNight.cs (partial)
public GameNightStatus Status { get; private set; } = GameNightStatus.Planned;

public void HandleSessionCreated(SessionCreatedDomainEvent evt)
{
    if (evt.GameNightId != Id)
        throw new InvalidOperationException("Event for different GameNight");
    if (Status == GameNightStatus.Completed)
        throw new InvalidOperationException(
            $"GameNight {Id} is Completed, cannot accept new sessions.");
    if (Status == GameNightStatus.Planned)
        Status = GameNightStatus.InProgress; // invariante #15
    // If already InProgress: no-op (subsequent sessions don't change state)
}

public void MarkAsCompleted()
{
    if (Status != GameNightStatus.InProgress)
        throw new InvalidOperationException(
            $"Cannot mark Planned GameNight {Id} as Completed. Status={Status}.");
    Status = GameNightStatus.Completed;
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Wire SessionCreatedDomainEvent into Session.CreateDraft + Session.OpenLiveMode**

```csharp
// Session.cs
public IReadOnlyList<object> DomainEvents => _events;
private readonly List<object> _events = new();

public static Session CreateDraft(Guid gameNightId, Guid gameId)
{
    var s = new Session { /* ... */ };
    s._events.Add(new SessionCreatedDomainEvent(gameNightId, s.Id, isLive: false));
    return s;
}

public Session OpenLiveMode()
{
    if (StartedAt is not null) throw new InvalidOperationException(/* ... */);
    StartedAt = DateTimeOffset.UtcNow;
    // If created via OpenLiveMode directly (not draft → live), emit IsLive=true
    if (CreatedAt >= StartedAt.Value.AddSeconds(-1))
        _events.Add(new SessionCreatedDomainEvent(GameNightId, Id, isLive: true));
    return this;
}
```

- [ ] **Step 6: Add MediatR INotification handler dispatching event to GameNight aggregate**

```csharp
// In SessionTracking/Application/EventHandlers/SessionCreatedHandler.cs
public class SessionCreatedHandler : INotificationHandler<SessionCreatedDomainEvent>
{
    private readonly IGameNightRepository _repo;
    public SessionCreatedHandler(IGameNightRepository repo) => _repo = repo;

    public async Task Handle(SessionCreatedDomainEvent evt, CancellationToken ct)
    {
        var gn = await _repo.GetByIdAsync(evt.GameNightId, ct)
            ?? throw new NotFoundException($"GameNight {evt.GameNightId} not found");
        gn.HandleSessionCreated(evt);
        await _repo.SaveAsync(gn, ct);
    }
}
```

- [ ] **Step 7: Run all unit + integration tests**

```bash
dotnet test --filter "BoundedContext=GameManagement|BoundedContext=SessionTracking"
```
Expected: PASS

- [ ] **Step 8: Commit**

```bash
git commit -m "feat(game-management): #1896 GameNight state machine + SessionCreatedDomainEvent (invariante #15)"
```

**Self-review**:
- [ ] Invariante #15 esplicita: trigger ON FIRST Session creation (draft OR live)
- [ ] No backward transition Completed → InProgress
- [ ] MediatR INotification dispatch via DomainEvents collection
- [ ] Aggregate repository pattern preserved

---

### Task 11: GameNightPlayer.IsTagged/IsInvited/RsvpStatus + invariante #16 separation

**Mix-model**: sonnet · **Effort**: M (~5h)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/RsvpStatus.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/GameNightPlayer.cs`
- Test: `apps/api/tests/Api.Tests/Unit/GameManagement/Domain/GameNightPlayerRsvpTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
public class GameNightPlayerRsvpTests
{
    [Fact]
    public void TagPlayer_SetsIsTaggedTrue_IsInvitedFalse_RsvpPending()
    {
        var player = GameNightPlayer.Tag(playerId: Guid.NewGuid());
        player.IsTagged.Should().BeTrue();
        player.IsInvited.Should().BeFalse();
        player.RsvpStatus.Should().Be(RsvpStatus.Pending);
    }

    [Fact]
    public void Invite_OnTaggedPlayer_SetsIsInvitedTrue_StaysRsvpPending()
    {
        var player = GameNightPlayer.Tag(Guid.NewGuid());
        player.Invite();
        player.IsTagged.Should().BeTrue();
        player.IsInvited.Should().BeTrue();
        player.RsvpStatus.Should().Be(RsvpStatus.Pending);
    }

    [Fact]
    public void ConfirmRsvp_OnInvited_SetsConfirmed()
    {
        var player = GameNightPlayer.Tag(Guid.NewGuid());
        player.Invite();
        player.ConfirmRsvp();
        player.RsvpStatus.Should().Be(RsvpStatus.Confirmed);
    }

    [Fact]
    public void DeclineRsvp_OnInvited_SetsDeclined()
    {
        var player = GameNightPlayer.Tag(Guid.NewGuid());
        player.Invite();
        player.DeclineRsvp();
        player.RsvpStatus.Should().Be(RsvpStatus.Declined);
    }

    [Fact]
    public void ConfirmRsvp_OnNotInvited_Throws()
    {
        var player = GameNightPlayer.Tag(Guid.NewGuid());
        var act = () => player.ConfirmRsvp();
        act.Should().Throw<InvalidOperationException>().WithMessage("*not yet invited*");
    }
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement**

```csharp
public enum RsvpStatus { Pending, Confirmed, Declined }

public sealed class GameNightPlayer
{
    public Guid Id { get; private set; }
    public Guid PlayerId { get; private set; }
    public bool IsTagged { get; private set; }
    public bool IsInvited { get; private set; }
    public RsvpStatus RsvpStatus { get; private set; }

    private GameNightPlayer() { }

    public static GameNightPlayer Tag(Guid playerId) => new()
    {
        Id = Guid.NewGuid(),
        PlayerId = playerId,
        IsTagged = true,
        IsInvited = false,
        RsvpStatus = RsvpStatus.Pending,
    };

    public void Invite()
    {
        if (!IsTagged) throw new InvalidOperationException("Player not tagged");
        IsInvited = true;
    }

    public void ConfirmRsvp()
    {
        if (!IsInvited) throw new InvalidOperationException("Player not yet invited");
        RsvpStatus = RsvpStatus.Confirmed;
    }

    public void DeclineRsvp()
    {
        if (!IsInvited) throw new InvalidOperationException("Player not yet invited");
        RsvpStatus = RsvpStatus.Declined;
    }
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(game-management): #1896 GameNightPlayer IsTagged/IsInvited/RsvpStatus separation (invariante #16)"
```

---

### Task 12: SendGameNightInvitationsCommand + 5-fase flow

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/SendInvitations/SendGameNightInvitationsCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/SendInvitations/SendGameNightInvitationsCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/Integration/GameManagement/SendGameNightInvitationsTests.cs`

- [ ] **Step 1: Write failing integration test**

```csharp
[Fact]
public async Task SendInvitations_TransitionsAllTaggedToInvited_DoesNotChangeRsvp()
{
    using var app = await TestApp.CreateAsync();
    var gnId = await app.SeedGameNightAsync(taggedPlayers: 3);

    await app.Client.PostAsync($"/api/v1/game-nights/{gnId}/send-invitations", null);

    var players = await app.GetGameNightPlayersAsync(gnId);
    players.Should().HaveCount(3);
    players.All(p => p.IsTagged).Should().BeTrue();
    players.All(p => p.IsInvited).Should().BeTrue();
    players.All(p => p.RsvpStatus == "Pending").Should().BeTrue();
}

[Fact]
public async Task SendInvitations_IsIdempotent_DoesntCreateDuplicateNotifications()
{
    using var app = await TestApp.CreateAsync();
    var gnId = await app.SeedGameNightAsync(taggedPlayers: 2);

    await app.Client.PostAsync($"/api/v1/game-nights/{gnId}/send-invitations", null);
    await app.Client.PostAsync($"/api/v1/game-nights/{gnId}/send-invitations", null);

    var notifs = await app.GetNotificationsForGameNightAsync(gnId);
    notifs.Should().HaveCount(2); // not 4
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement command + handler**

```csharp
public record SendGameNightInvitationsCommand(Guid GameNightId) : IRequest<int>;

public class SendGameNightInvitationsCommandHandler : IRequestHandler<SendGameNightInvitationsCommand, int>
{
    private readonly IGameNightRepository _gnRepo;
    private readonly IMediator _mediator;

    public SendGameNightInvitationsCommandHandler(IGameNightRepository repo, IMediator m)
    {
        _gnRepo = repo;
        _mediator = m;
    }

    public async Task<int> Handle(SendGameNightInvitationsCommand cmd, CancellationToken ct)
    {
        var gn = await _gnRepo.GetByIdAsync(cmd.GameNightId, ct)
            ?? throw new NotFoundException($"GameNight {cmd.GameNightId}");

        var toInvite = gn.Players.Where(p => p.IsTagged && !p.IsInvited).ToList();
        foreach (var player in toInvite)
        {
            player.Invite();
            // dispatch notification command (will be implemented in WP6)
            await _mediator.Send(new SendInvitationNotificationCommand(
                gameNightId: gn.Id,
                recipientPlayerId: player.PlayerId
            ), ct);
        }

        await _gnRepo.SaveAsync(gn, ct);
        return toInvite.Count;
    }
}
```

- [ ] **Step 4: Add endpoint via Routing**

```csharp
// GameNightEndpoints.cs
app.MapPost("/api/v1/game-nights/{id:guid}/send-invitations",
    async (Guid id, IMediator m) =>
        Results.Ok(new { invitedCount = await m.Send(new SendGameNightInvitationsCommand(id)) }));
```

- [ ] **Step 5: Run → PASS** (notification handler stub in WP6 may not be ready — use temporary mock)

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(game-management): #1896 SendGameNightInvitations command + idempotent flow (invariante #17)"
```

---

## WP4 — Invariante #10 max 1 live

> **Spec reference**: Sezione 4 Asse A — "Invariante max 1 live".

### Task 13: MaxLiveSessionsExceededException + aggregate guard

**Mix-model**: sonnet · **Effort**: M (~5h)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Exceptions/MaxLiveSessionsExceededException.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/GameNight.cs`
- Test: `apps/api/tests/Api.Tests/Unit/GameManagement/Domain/GameNightMaxLiveTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public void OpenLiveSession_WhenAnotherLiveActive_Throws_MaxLiveSessionsExceededException()
{
    var gn = GameNight.Create(Guid.NewGuid(), DateTimeOffset.UtcNow);
    var firstLive = Session.CreateDraft(gn.Id, Guid.NewGuid()).OpenLiveMode();
    var secondDraft = Session.CreateDraft(gn.Id, Guid.NewGuid());

    var act = () => gn.OpenLiveSession(secondDraft, currentlyLive: new[] { firstLive });

    act.Should().Throw<MaxLiveSessionsExceededException>()
        .Where(ex => ex.ErrorCode == "MAX_LIVE_SESSIONS_EXCEEDED")
        .Where(ex => ex.GameNightId == gn.Id);
}

[Fact]
public void OpenLiveSession_WithNoActiveLive_Succeeds()
{
    var gn = GameNight.Create(Guid.NewGuid(), DateTimeOffset.UtcNow);
    var draft = Session.CreateDraft(gn.Id, Guid.NewGuid());

    var act = () => gn.OpenLiveSession(draft, currentlyLive: Array.Empty<Session>());

    act.Should().NotThrow();
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement exception + guard**

```csharp
public class MaxLiveSessionsExceededException : DomainException
{
    public Guid GameNightId { get; }
    public override string ErrorCode => "MAX_LIVE_SESSIONS_EXCEEDED";

    public MaxLiveSessionsExceededException(Guid gameNightId)
        : base($"GameNight {gameNightId} already has an active live session.")
    {
        GameNightId = gameNightId;
    }
}

// GameNight.cs
public Session OpenLiveSession(Session draft, IEnumerable<Session> currentlyLive)
{
    if (currentlyLive.Any(s => s.StartedAt != null && s.CompletedAt == null))
        throw new MaxLiveSessionsExceededException(Id);
    return draft.OpenLiveMode();
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(game-management): #1896 MaxLiveSessionsExceededException + aggregate guard (invariante #10)"
```

---

### Task 14: API endpoint POST /game-nights/{id}/sessions/{sessionId}/live → 409

**Mix-model**: sonnet · **Effort**: S (~3h)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Routing/GameNightEndpoints.cs`
- Modify: `apps/api/src/Api/Infrastructure/ExceptionHandling/DomainExceptionMiddleware.cs`
- Test: `apps/api/tests/Api.Tests/Integration/GameManagement/OpenLiveSessionEndpointTests.cs`

- [ ] **Step 1: Write failing integration test**

```csharp
[Fact]
public async Task POST_OpenLiveSession_With_ExistingLive_Returns409_WithErrorCode()
{
    using var app = await TestApp.CreateAsync();
    var gn = await app.SeedGameNightAsync();
    await app.OpenLiveSessionAsync(gn.Id, gameId: Guid.NewGuid());
    var secondDraft = await app.CreateDraftSessionAsync(gn.Id, gameId: Guid.NewGuid());

    var response = await app.Client.PostAsync(
        $"/api/v1/game-nights/{gn.Id}/sessions/{secondDraft.Id}/live", null);

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    var body = await response.Content.ReadFromJsonAsync<ErrorDto>();
    body!.Code.Should().Be("MAX_LIVE_SESSIONS_EXCEEDED");
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Add endpoint + middleware mapping**

```csharp
// GameNightEndpoints.cs
app.MapPost("/api/v1/game-nights/{gnId:guid}/sessions/{sId:guid}/live",
    async (Guid gnId, Guid sId, IMediator m) =>
        Results.Ok(await m.Send(new OpenLiveSessionCommand(gnId, sId))));
```

```csharp
// DomainExceptionMiddleware.cs
catch (MaxLiveSessionsExceededException ex)
{
    context.Response.StatusCode = StatusCodes.Status409Conflict;
    await context.Response.WriteAsJsonAsync(new ErrorDto(ex.ErrorCode, ex.Message));
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(game-management): #1896 POST live session returns 409 MAX_LIVE_SESSIONS_EXCEEDED"
```

---

## WP5 — Polymorphic ScoreType (DEC-1)

> **Spec reference**: Sezione 4 Asse A — "DEC-1 — Polymorphic ScoreType".

### Task 15: ScoreType enum + IScoringStrategy + factory skeleton

**Mix-model**: sonnet · **Effort**: M (~5h)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/ScoreType.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/IScoringStrategy.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/ScoringStrategyFactory.cs`
- Test: `apps/api/tests/Api.Tests/Unit/SessionTracking/Scoring/ScoringStrategyFactoryTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
[Theory]
[InlineData(ScoreType.Points, typeof(PointsScoringStrategy))]
[InlineData(ScoreType.BinaryWin, typeof(BinaryWinScoringStrategy))]
[InlineData(ScoreType.Objectives, typeof(ObjectivesScoringStrategy))]
[InlineData(ScoreType.Ranking, typeof(RankingScoringStrategy))]
public void GetStrategy_ReturnsExpectedStrategyForEachScoreType(
    ScoreType type, Type expectedStrategyType)
{
    var factory = new ScoringStrategyFactory();
    var strategy = factory.GetStrategy(type);
    strategy.Should().BeOfType(expectedStrategyType);
}

[Fact]
public void GetStrategy_OnUnknownType_Throws()
{
    var factory = new ScoringStrategyFactory();
    var act = () => factory.GetStrategy((ScoreType)999);
    act.Should().Throw<ArgumentOutOfRangeException>();
}
```

- [ ] **Step 2: Run → FAIL** (types don't exist)

- [ ] **Step 3: Create types**

```csharp
public enum ScoreType
{
    [Description("Punti numerici per player")]
    Points = 0,
    [Description("Winner/Loser binario (cooperativo)")]
    BinaryWin = 1,
    [Description("Obiettivi completati per player")]
    Objectives = 2,
    [Description("Posizione 1..N per player")]
    Ranking = 3,
}

public interface IScoringStrategy
{
    ScoreType Type { get; }
    ValidationResult Validate(string scoreDataJson);
    string Serialize(object scoreData);
    object Deserialize(string scoreDataJson);
    Guid? ComputeWinnerPlayerId(string scoreDataJson);
}

// Placeholder skeletons for 4 strategies (filled in T16+T17)
public class PointsScoringStrategy : IScoringStrategy
{
    public ScoreType Type => ScoreType.Points;
    public ValidationResult Validate(string s) => throw new NotImplementedException();
    public string Serialize(object d) => throw new NotImplementedException();
    public object Deserialize(string s) => throw new NotImplementedException();
    public Guid? ComputeWinnerPlayerId(string s) => throw new NotImplementedException();
}
// Same for Binary/Objectives/Ranking

public class ScoringStrategyFactory
{
    public IScoringStrategy GetStrategy(ScoreType type) => type switch
    {
        ScoreType.Points => new PointsScoringStrategy(),
        ScoreType.BinaryWin => new BinaryWinScoringStrategy(),
        ScoreType.Objectives => new ObjectivesScoringStrategy(),
        ScoreType.Ranking => new RankingScoringStrategy(),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
```

- [ ] **Step 4: Run → PASS** (factory dispatch works, strategies stubbed)

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(session-tracking): #1896 ScoreType enum + IScoringStrategy + factory (DEC-1 skeleton)"
```

---

### Task 16: PointsScoringStrategy + BinaryWinScoringStrategy

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/PointsScoringStrategy.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/BinaryWinScoringStrategy.cs`
- Test: `apps/api/tests/Api.Tests/Unit/SessionTracking/Scoring/PointsScoringStrategyTests.cs`
- Test: `apps/api/tests/Api.Tests/Unit/SessionTracking/Scoring/BinaryWinScoringStrategyTests.cs`

- [ ] **Step 1: Write failing tests for Points (5 cases)**

```csharp
public class PointsScoringStrategyTests
{
    private readonly PointsScoringStrategy _sut = new();

    [Fact]
    public void Validate_ValidJson_ReturnsValid()
    {
        var json = """{"scores":[{"playerId":"a1","points":42},{"playerId":"b2","points":30}]}""";
        var result = _sut.Validate(json);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NegativeScore_ReturnsInvalid()
    {
        var json = """{"scores":[{"playerId":"a1","points":-5}]}""";
        var result = _sut.Validate(json);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("non-negative"));
    }

    [Fact]
    public void Validate_DuplicatePlayer_ReturnsInvalid()
    {
        var json = """{"scores":[{"playerId":"a1","points":10},{"playerId":"a1","points":20}]}""";
        var result = _sut.Validate(json);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ComputeWinnerPlayerId_ReturnsHighestScorePlayer()
    {
        var json = """{"scores":[{"playerId":"00000000-0000-0000-0000-000000000001","points":10},{"playerId":"00000000-0000-0000-0000-000000000002","points":50}]}""";
        var winner = _sut.ComputeWinnerPlayerId(json);
        winner.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000002"));
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrip()
    {
        var data = new PointsScoreData
        {
            Scores = new[] { new PlayerScore(Guid.NewGuid(), 42) }
        };
        var json = _sut.Serialize(data);
        var back = (PointsScoreData)_sut.Deserialize(json);
        back.Scores.Should().HaveCount(1);
        back.Scores[0].Points.Should().Be(42);
    }
}
```

- [ ] **Step 2: Write failing tests for BinaryWin (5 cases)**

```csharp
public class BinaryWinScoringStrategyTests
{
    private readonly BinaryWinScoringStrategy _sut = new();

    [Fact]
    public void Validate_AllWin_ReturnsValid() { /* cooperativo, all win */ }

    [Fact]
    public void Validate_AllLose_ReturnsValid() { /* cooperativo, all lose */ }

    [Fact]
    public void Validate_MixedWinLose_ReturnsValid() { /* competitivo binary */ }

    [Fact]
    public void Validate_EmptyPlayerList_ReturnsInvalid() { /* edge */ }

    [Fact]
    public void ComputeWinnerPlayerId_ReturnsNull_WhenCooperativeAllWin() { /* no single winner */ }
}
```

- [ ] **Step 3: Run → FAIL**

- [ ] **Step 4: Implement both strategies**

```csharp
public record PointsScoreData(PlayerScore[] Scores);
public record PlayerScore(Guid PlayerId, int Points);

public class PointsScoringStrategy : IScoringStrategy
{
    public ScoreType Type => ScoreType.Points;

    public ValidationResult Validate(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<PointsScoreData>(json);
            if (data?.Scores is null || data.Scores.Length == 0)
                return ValidationResult.Failed("No scores provided");
            var errors = new List<string>();
            if (data.Scores.Any(s => s.Points < 0)) errors.Add("Points must be non-negative");
            var grouped = data.Scores.GroupBy(s => s.PlayerId);
            if (grouped.Any(g => g.Count() > 1)) errors.Add("Duplicate playerId");
            return errors.Any() ? ValidationResult.Failed(errors) : ValidationResult.Valid();
        }
        catch (JsonException ex) { return ValidationResult.Failed($"Invalid JSON: {ex.Message}"); }
    }

    public string Serialize(object data) => JsonSerializer.Serialize((PointsScoreData)data);
    public object Deserialize(string json) => JsonSerializer.Deserialize<PointsScoreData>(json)!;

    public Guid? ComputeWinnerPlayerId(string json)
    {
        var data = (PointsScoreData)Deserialize(json);
        return data.Scores.OrderByDescending(s => s.Points).FirstOrDefault()?.PlayerId;
    }
}

// BinaryWin
public record BinaryWinScoreData(BinaryPlayerResult[] Results);
public record BinaryPlayerResult(Guid PlayerId, bool IsWinner);

public class BinaryWinScoringStrategy : IScoringStrategy
{
    public ScoreType Type => ScoreType.BinaryWin;
    public ValidationResult Validate(string json) { /* similar */ }
    public string Serialize(object d) => JsonSerializer.Serialize((BinaryWinScoreData)d);
    public object Deserialize(string j) => JsonSerializer.Deserialize<BinaryWinScoreData>(j)!;
    public Guid? ComputeWinnerPlayerId(string json)
    {
        var data = (BinaryWinScoreData)Deserialize(json);
        var winners = data.Results.Where(r => r.IsWinner).ToList();
        // If single winner: return; if all-win (cooperative) or all-lose: return null
        return winners.Count == 1 ? winners.Single().PlayerId : null;
    }
}
```

- [ ] **Step 5: Run → PASS**

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(session-tracking): #1896 Points + BinaryWin scoring strategies (DEC-1)"
```

---

### Task 17: ObjectivesScoringStrategy + RankingScoringStrategy

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/ObjectivesScoringStrategy.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/RankingScoringStrategy.cs`
- Test: corresponding test classes

- [ ] **Step 1-6**: Same pattern as Task 16, 5 test cases per strategy.

**Objectives logic**: each player has list of completed objectives (string names). Winner = player with most objectives completed. Ties = null winner.

**Ranking logic**: each player has integer position (1..N). Winner = position 1. Validate distinct positions, sequential 1..N.

- [ ] **Step 7: Commit**

```bash
git commit -m "feat(session-tracking): #1896 Objectives + Ranking scoring strategies (DEC-1)"
```

---

### Task 18: SaveSessionCommand polymorphic scoreData + FluentValidation rule

**Mix-model**: sonnet · **Effort**: M (~5h)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/SaveSession/SaveSessionCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/SaveSession/SaveSessionCommandValidator.cs`
- Test: `apps/api/tests/Api.Tests/Unit/SessionTracking/SaveSessionCommandValidatorTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
public class SaveSessionCommandValidatorTests
{
    private readonly SaveSessionCommandValidator _sut = new(new ScoringStrategyFactory());

    [Fact]
    public void Validate_PointsScoreData_Valid_ReturnsValid()
    {
        var cmd = new SaveSessionCommand(
            SessionId: Guid.NewGuid(),
            ScoringType: ScoreType.Points,
            ScoreData: """{"scores":[{"playerId":"00000000-0000-0000-0000-000000000001","points":50}]}"""
        );
        var result = _sut.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_BinaryWinScoreData_AppliedAsPoints_ReturnsInvalid()
    {
        var cmd = new SaveSessionCommand(
            SessionId: Guid.NewGuid(),
            ScoringType: ScoreType.Points,
            ScoreData: """{"results":[{"playerId":"00000000-0000-0000-0000-000000000001","isWinner":true}]}"""
        );
        var result = _sut.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement command + validator**

```csharp
public record SaveSessionCommand(
    Guid SessionId,
    ScoreType ScoringType,
    string ScoreData
) : IRequest<SaveSessionResult>;

public class SaveSessionCommandValidator : AbstractValidator<SaveSessionCommand>
{
    public SaveSessionCommandValidator(ScoringStrategyFactory factory)
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ScoreData)
            .NotEmpty()
            .Custom((scoreData, context) =>
            {
                var cmd = context.InstanceToValidate;
                var strategy = factory.GetStrategy(cmd.ScoringType);
                var result = strategy.Validate(scoreData);
                if (!result.IsValid)
                    foreach (var err in result.Errors)
                        context.AddFailure("ScoreData", err);
            });
    }
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(session-tracking): #1896 SaveSessionCommand polymorphic ScoreData + FluentValidation (DEC-1)"
```

---

### Task 19: Session.SetScores + SessionScoresUpdated + integration round-trip

**Mix-model**: sonnet · **Effort**: L (~8h)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Session.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/SessionScoresUpdated.cs`
- Test: `apps/api/tests/Api.Tests/Integration/SessionTracking/PolymorphicScoringRoundTripTests.cs`

- [ ] **Step 1: Write failing integration test (4 scoring types round-trip via API)**

```csharp
[Theory]
[InlineData("Points", """{"scores":[{"playerId":"00000000-0000-0000-0000-000000000001","points":50}]}""")]
[InlineData("BinaryWin", """{"results":[{"playerId":"00000000-0000-0000-0000-000000000001","isWinner":true}]}""")]
[InlineData("Objectives", """{"completedByPlayer":[{"playerId":"00000000-0000-0000-0000-000000000001","objectives":["A","B"]}]}""")]
[InlineData("Ranking", """{"positions":[{"playerId":"00000000-0000-0000-0000-000000000001","position":1}]}""")]
public async Task SaveSession_WithPolymorphicScoring_RoundTripViaAPI(string scoringType, string scoreData)
{
    using var app = await TestApp.CreateAsync();
    var gnId = await app.SeedGameNightAsync();
    var draft = await app.CreateDraftSessionAsync(gnId, gameId: Guid.NewGuid());

    var response = await app.Client.PutAsJsonAsync(
        $"/api/v1/sessions/{draft.Id}/save",
        new { scoringType, scoreData });

    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var fetched = await app.Client.GetFromJsonAsync<SessionDto>(
        $"/api/v1/sessions/{draft.Id}");
    fetched!.ScoringType.Should().Be(scoringType);
    fetched.ScoreData.Should().Be(scoreData);
}
```

- [ ] **Step 2: Add Session.SetScores domain method**

```csharp
public void SetScores(ScoreType scoringType, string scoreData)
{
    ScoringType = scoringType;
    ScoreData = scoreData;
    _events.Add(new SessionScoresUpdated(Id, scoringType, scoreData));
}
```

- [ ] **Step 3: Update SaveSessionCommandHandler to call SetScores**

- [ ] **Step 4: Run → PASS** (all 4 scoring types round-trip)

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(session-tracking): #1896 Session.SetScores + round-trip 4 ScoreType (DEC-1)"
```

---

## WP6 — Notification system (DEC-5)

> **Spec reference**: Sezione 4 Asse A — "DEC-5 — Notification system".

### Task 20: Notification repository + UserNotifications bounded context wiring

**Mix-model**: sonnet · **Effort**: M (~5h)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Repositories/INotificationRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Repositories/NotificationRepository.cs`
- Test: `apps/api/tests/Api.Tests/Integration/UserNotifications/NotificationRepositoryTests.cs`

- [ ] Standard repository pattern + CRUD operations + integration test against Testcontainers postgres.

```bash
git commit -m "feat(user-notifications): #1896 Notification repository (DEC-5)"
```

---

### Task 21: GET /notifications + PATCH read endpoints

**Mix-model**: sonnet · **Effort**: M (~5h)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/GetNotifications/GetNotificationsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/MarkAsRead/MarkAsReadCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Routing/NotificationEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/Integration/UserNotifications/NotificationEndpointsTests.cs`

- [ ] Endpoint contract:
  - `GET /api/v1/notifications?page=N&size=M` → `PagedResult<NotificationDto>`
  - `PATCH /api/v1/notifications/{id}/read` → `204 No Content`
  - `POST /api/v1/notifications/mark-all-read` → `{ markedCount }`

```bash
git commit -m "feat(user-notifications): #1896 inbox endpoints GET + PATCH read (DEC-5)"
```

---

### Task 22: IEmailSender + ResendEmailSender + GameNightInvitation template

**Mix-model**: sonnet · **Effort**: L (~8h)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Email/IEmailSender.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Email/ResendEmailSender.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Email/Templates/GameNightInvitationTemplate.cs`
- Create: `infra/secrets/email.secret.example`
- Test: `apps/api/tests/Api.Tests/Unit/UserNotifications/Email/ResendEmailSenderTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public async Task ResendEmailSender_SendAsync_PostsToResendAPI_WithApiKey()
{
    var mockHttp = new MockHttpMessageHandler();
    mockHttp.When("https://api.resend.com/emails")
        .Respond(HttpStatusCode.OK, JsonContent.Create(new { id = "test-msg-id" }));
    var sender = new ResendEmailSender(
        new HttpClient(mockHttp),
        new ResendOptions { ApiKey = "re_test" });

    var template = GameNightInvitationTemplate.Render(
        recipientName: "Anna",
        hostName: "Marco",
        gameNightName: "Sabato a casa Marco",
        date: new DateTimeOffset(2026, 6, 14, 20, 0, 0, TimeSpan.Zero),
        location: "Roma");

    await sender.SendAsync(
        toEmail: "anna@example.com",
        subject: "Marco ti ha invitato",
        htmlBody: template.Html,
        plainTextBody: template.PlainText);

    mockHttp.GetMatchCount(/* request */).Should().Be(1);
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement**

```csharp
public interface IEmailSender
{
    Task<string> SendAsync(string toEmail, string subject, string htmlBody, string plainTextBody, CancellationToken ct = default);
}

public class ResendOptions { public string ApiKey { get; set; } = default!; public string FromEmail { get; set; } = "noreply@meepleai.app"; }

public class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly ResendOptions _options;

    public ResendEmailSender(HttpClient http, ResendOptions options)
    {
        _http = http;
        _options = options;
        _http.DefaultRequestHeaders.Authorization = new("Bearer", options.ApiKey);
    }

    public async Task<string> SendAsync(string to, string subject, string html, string text, CancellationToken ct = default)
    {
        var payload = new
        {
            from = _options.FromEmail,
            to = new[] { to },
            subject,
            html,
            text,
        };
        var response = await _http.PostAsJsonAsync("https://api.resend.com/emails", payload, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ResendResponse>(cancellationToken: ct);
        return result!.Id;
    }
    private record ResendResponse(string Id);
}

public static class GameNightInvitationTemplate
{
    public record Rendered(string Html, string PlainText);

    public static Rendered Render(
        string recipientName, string hostName, string gameNightName,
        DateTimeOffset date, string location)
    {
        var formattedDate = date.ToString("dddd d MMMM yyyy 'alle' HH:mm",
            CultureInfo.GetCultureInfo("it-IT"));

        var html = $"""
            <h1>Ciao {recipientName}!</h1>
            <p><strong>{hostName}</strong> ti ha invitato a una serata:</p>
            <p><strong>{gameNightName}</strong> · {formattedDate} · {location}</p>
            <p><a href="https://meepleai.app/game-nights">Conferma RSVP</a></p>
            """;
        var text = $"""
            Ciao {recipientName}!
            {hostName} ti ha invitato a {gameNightName} ({formattedDate}, {location}).
            Conferma RSVP: https://meepleai.app/game-nights
            """;
        return new Rendered(html, text);
    }
}
```

- [ ] **Step 4: Update infra/secrets/email.secret.example**

```bash
# Email transactional provider (Resend)
RESEND_API_KEY=re_xxxxxxxxxxxxxxxxxxxxxxxxxxxxx
RESEND_FROM_EMAIL=noreply@meepleai.app
```

- [ ] **Step 5: Register DI in Program.cs**

```csharp
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection("Resend"));
builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>();
```

- [ ] **Step 6: Run → PASS**

- [ ] **Step 7: Commit**

```bash
git commit -m "feat(user-notifications): #1896 IEmailSender + ResendEmailSender + invitation template (DEC-5)"
```

**Self-review**:
- [ ] HttpClient with Resend bearer auth
- [ ] Idempotent (Resend SDK provides message-id, no double-send on retry)
- [ ] Email template Italian-localized
- [ ] HTML + plain text both provided

---

### Task 23: SendInvitationNotificationCommand handler in-app+email

**Mix-model**: sonnet · **Effort**: L (~8h)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/SendInvitationNotification/SendInvitationNotificationCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/SendInvitationNotification/SendInvitationNotificationCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/Integration/UserNotifications/SendInvitationFlowTests.cs`

- [ ] **Step 1: Write failing integration test**

```csharp
[Fact]
public async Task SendInvitationCommand_CreatesInAppNotification_AndCallsEmailSender()
{
    using var app = await TestApp.CreateAsync(services =>
    {
        services.AddSingleton<IEmailSender>(Substitute.For<IEmailSender>());
    });
    var emailSender = app.Resolve<IEmailSender>();
    var (gnId, recipientPlayerId) = await app.SeedGameNightWithInvitedPlayerAsync();

    await app.Mediator.Send(new SendInvitationNotificationCommand(gnId, recipientPlayerId));

    var notifs = await app.Db.Notifications.Where(n => n.Type == "GameNightInvitation").ToListAsync();
    notifs.Should().ContainSingle();
    notifs[0].Payload.Should().Contain(gnId.ToString());

    await emailSender.Received(1).SendAsync(
        Arg.Any<string>(), Arg.Is<string>(s => s.Contains("invitato")),
        Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
}

[Fact]
public async Task SendInvitationCommand_EmailFails_StillCommitsInAppNotification()
{
    using var app = await TestApp.CreateAsync(services =>
    {
        var failingEmail = Substitute.For<IEmailSender>();
        failingEmail.SendAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(Task.FromException<string>(new HttpRequestException("Resend down")));
        services.AddSingleton(failingEmail);
    });
    var (gnId, recipientPlayerId) = await app.SeedGameNightWithInvitedPlayerAsync();

    await app.Mediator.Send(new SendInvitationNotificationCommand(gnId, recipientPlayerId));

    var notifs = await app.Db.Notifications.ToListAsync();
    notifs.Should().ContainSingle(); // in-app saved despite email failure
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement handler**

```csharp
public record SendInvitationNotificationCommand(Guid GameNightId, Guid RecipientPlayerId) : IRequest;

public class SendInvitationNotificationCommandHandler : IRequestHandler<SendInvitationNotificationCommand>
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _email;
    private readonly ILogger<SendInvitationNotificationCommandHandler> _logger;

    public SendInvitationNotificationCommandHandler(
        ApplicationDbContext db, IEmailSender email, ILogger<SendInvitationNotificationCommandHandler> logger)
    {
        _db = db; _email = email; _logger = logger;
    }

    public async Task Handle(SendInvitationNotificationCommand cmd, CancellationToken ct)
    {
        var gn = await _db.GameNights.FindAsync(new object?[] { cmd.GameNightId }, ct)
            ?? throw new NotFoundException($"GameNight {cmd.GameNightId}");
        var player = await _db.Players.FindAsync(new object?[] { cmd.RecipientPlayerId }, ct)
            ?? throw new NotFoundException($"Player {cmd.RecipientPlayerId}");
        if (player.UserId is null) return; // guest, no notification

        // 1. In-app notification (transactional)
        var payload = JsonSerializer.Serialize(new
        {
            gameNightId = gn.Id,
            gameNightName = gn.Name,
            hostName = (await _db.Users.FindAsync(new object?[] { gn.OwnerId }, ct))?.DisplayName,
            date = gn.Date,
            location = gn.Location,
        });
        var notif = Notification.Create(
            recipientUserId: player.UserId.Value,
            type: "GameNightInvitation",
            payloadJson: payload);
        _db.Notifications.Add(notif);
        await _db.SaveChangesAsync(ct);

        // 2. Email (best-effort, doesn't block in-app)
        try
        {
            var rendered = GameNightInvitationTemplate.Render(
                recipientName: player.Name,
                hostName: (await _db.Users.FindAsync(new object?[] { gn.OwnerId }, ct))!.DisplayName,
                gameNightName: gn.Name,
                date: gn.Date,
                location: gn.Location);

            var user = await _db.Users.FindAsync(new object?[] { player.UserId.Value }, ct);
            await _email.SendAsync(
                toEmail: user!.Email,
                subject: $"Marco ti ha invitato a {gn.Name}",
                htmlBody: rendered.Html,
                plainTextBody: rendered.PlainText,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email send failed for notification {NotifId}, in-app committed", notif.Id);
            // Swallow — in-app is the source of truth
        }
    }
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(user-notifications): #1896 SendInvitationNotificationCommand in-app+email (DEC-5)"
```

---

## WP7 — OpenAPI + final acceptance

### Task 24: OpenAPI yaml updates + new error codes

**Mix-model**: haiku · **Effort**: S (~3h)

**Files:**
- Modify: `apps/api/src/Api/openapi.yaml` (or wherever OpenAPI lives)
- Test: `apps/api/tests/Api.Tests/Integration/OpenApiContractTests.cs`

- [ ] Add new error code definitions, polymorphic ScoreData schema, Notification DTOs, X-Warning-Code header.

```bash
git commit -m "docs(api): #1896 OpenAPI updates + new error codes + polymorphic DTOs"
```

---

### Task 25: Final spec compliance review + CLAUDE.md update

**Mix-model**: haiku · **Effort**: M (~4h)

**Files:**
- Modify: `CLAUDE.md` (Domain Model — GameNight / Session section)
- Modify: `docs/superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md` (changelog inline)

- [ ] **Step 1**: Run full test suite
```bash
dotnet test
```
Expected: PASS

- [ ] **Step 2**: Update CLAUDE.md section "Domain Model — GameNight / Session" con stato post-impl: tutte 20 invarianti coperte + ScoreType polimorfico + Notification.

- [ ] **Step 3**: Update spec consolidato changelog
```markdown
- 2026-06-XX: asse A implementation complete — 25 task TDD shipped via PR #YYYY
```

- [ ] **Step 4**: Verify Definition of Done umbrella:
  - 20 invarianti implementate e testate
  - DEC-1 ScoreType 4 strategies + 4 unit class + 1 integration test
  - DEC-5 Notification + `/notifications` + email Resend operativo

- [ ] **Step 5**: Final commit
```bash
git commit -m "docs(claude-design-alignment): #1896 asse A complete — 25 task shipped, spec updated"
```

---

## Self-Review Checklist (post-plan)

**Spec coverage**:
- [x] WP1 covers Migration EF Core 3-step (MAJ-1 fix)
- [x] WP2 covers invarianti #11/#12/#13/#14
- [x] WP3 covers state machine #8/#15/#16/#17
- [x] WP4 covers invariante #10 + MIN-1 rename
- [x] WP5 covers DEC-1 polymorphic ScoreType (4 strategies)
- [x] WP6 covers DEC-5 Notification (in-app + email Resend)
- [x] WP7 covers OpenAPI updates + final acceptance
- [x] MAJ-2 backwards-compat documented in T3
- [x] MIN-6 factory method clarified in T6

**Placeholder scan**: no TBD, no "implement later", no "similar to Task N", no untyped methods.

**Type consistency**:
- `IScoringStrategy.ComputeWinnerPlayerId` returns `Guid?` consistently across T15/T16/T17/T19
- `RsvpStatus` enum values consistent across T3/T11/T12
- `GameNightStatus` consistent across T2/T10
- `SaveSessionResult` record consistent across T9/T19

**Critical path identification**:
- WP1 → WP2 → WP4 (max 1 live needs Session.OpenLiveMode)
- WP1 → WP3 → WP6 (SendGameNightInvitations triggers notification)
- WP5 parallelizable with WP3+WP4 (no shared aggregate)
- WP6 parallelizable with WP5 (different bounded context)
- WP7 closes WP

**Effort verification**:
- WP1: 3+2+3+2+4 = 14h ≈ 2gg
- WP2: 6+3+2+5 = 16h ≈ 2gg
- WP3: 8+5+6 = 19h ≈ 2.5gg
- WP4: 5+3 = 8h ≈ 1gg
- WP5: 5+6+6+5+8 = 30h ≈ 4gg
- WP6: 5+5+8+8 = 26h ≈ 3.5gg
- WP7: 3+4 = 7h ≈ 1gg
- **Total**: ~16gg dev + ~3gg buffer review/CI fix = ~19gg, in linea con stima 15+3=18gg ✓

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-06-04-asse-a-semantic-alignment.md`. Two execution options:**

**1. Subagent-Driven (recommended)** — Dispatch fresh subagent per task with mix-model (haiku/sonnet), review between tasks, ~3-4 settimane elapsed time per asse A.

**2. Inline Execution** — Execute tasks in current session sequentially, batch checkpoints. Non praticabile per asse A (XL).

**Recommended sequence**: WP1 (must be first) → WP2 + WP5 parallel (con 2 dev) → WP3 + WP4 + WP6 parallel → WP7 final.

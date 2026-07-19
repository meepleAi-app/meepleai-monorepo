# Design — #3157 C1: Restore the dropped `max 1 live per GameNight` partial unique index

**Issue**: [#3157](https://github.com/meepleAi-app/meepleai-monorepo/issues/3157) (scope C1) · **Branch**: `feature/issue-3157-session-live-canonicalization` · **Parent**: `main-dev` · **Date**: 2026-07-19

## 1. Problem (verified via deep discovery)

The invariant **"max 1 InProgress session-link per GameNight"** is referenced across `PauseSessionCommandHandler`, `ResumeSessionCommandHandler`, and `CreateSessionCommandHandler` as being enforced by *"the partial unique index from T1"* on `game_night_sessions`. **That index does not exist.**

- It was created in the T1 migration `AddSessionTurnOrderAndGameNightInProgress` (#365, commit `7fe20e96d`) as a raw `migrationBuilder.Sql()` partial unique index:
  ```sql
  CREATE UNIQUE INDEX IF NOT EXISTS "ix_game_night_sessions_unique_active"
  ON "game_night_sessions" ("game_night_event_id") WHERE "status" = 'InProgress';
  ```
- It was **silently dropped** when 14 migrations were consolidated into `20260712200111_Initial` (#2875 / PR #2880, 2026-07-12): `ef migrations add Initial` only reflects the EF *model*, so raw-SQL DDL is lost and `has-pending-model-changes` reports none. This is the documented pitfall `feedback_migration_flatten_drops_raw_sql`. Verified: 0 occurrences of `ix_game_night_sessions_unique_active` in `Migrations/`, 0 raw `migrationBuilder.Sql(` in the Initial baseline.

Today "max 1 live per night" is held only by application logic (a read-check in `ResolveGameNightAsync`, a domain guard `MaxLiveSessionsExceededException` in Path B, and `ResumeSessionCommandHandler`'s demote-then-promote transaction ordering) — none race-safe.

**Companion bug (must fix together):** `FinalizeSessionCommandHandler` (Path A) does **not** transition the link status — a finalized session leaves its `game_night_sessions` row stuck at `InProgress` (orphaned live slot). Path B (`CompleteGameNightSessionCommandHandler`) already closes it. Restoring the unique index **without** fixing this would make a legitimate sequential flow fail: session1 finalized (link1 orphaned `InProgress`) → session2 created in the same night mints link2 `InProgress` → **UNIQUE violation → 500**.

## 2. Scope (C1, user-approved)

Restore the DB-enforced invariant + stop the orphaning that would make it unsafe. **Out of scope** (deferred / part of C2): setting `Session.StartedAt` at create (would violate domain invariante #5/#14 — StartedAt is set ONLY via `OpenLiveMode`), realigning the guard off `Status==Active`, reconciling the two writer paths, changing the warning #13 query.

## 3. Design

### 3.1 Add the partial unique index to the EF model (root-cause fix)

In `GameNightSessionEntityConfiguration` add:
```csharp
builder.HasIndex(s => s.GameNightEventId)
    .HasDatabaseName("ix_game_night_sessions_unique_active")
    .IsUnique()
    .HasFilter("status = 'InProgress'");
```
Adding it to the **model** (not raw SQL) means it is EF-tracked and recorded in the model snapshot, so it will **survive a future flatten** — fixing the root cause of the original silent drop, not just the symptom.

### 3.2 Migration `RestoreGameNightSessionInProgressUniqueIndex`

Generate via `dotnet ef migrations add RestoreGameNightSessionInProgressUniqueIndex` (from `apps/api/src/Api`). EF emits the filtered `CreateIndex`. **Manually prepend** a data-cleanup block to `Up` (before the `CreateIndex`) so the index can be built on environments that already carry orphaned/duplicate `InProgress` rows:

```csharp
// Reconcile pre-existing violations of the restored invariant before the unique index.
// (a) Orphaned live slots: link InProgress but its session is finalized → close it.
migrationBuilder.Sql(@"
    UPDATE game_night_sessions gns
    SET status = 'Completed', completed_at = COALESCE(gns.completed_at, NOW())
    FROM session_tracking_sessions s
    WHERE gns.status = 'InProgress'
      AND s.id = gns.session_id
      AND s.finalized_at IS NOT NULL;");
// (b) Any remaining duplicates per night: keep the most-recently-started, demote the rest to Pending.
migrationBuilder.Sql(@"
    WITH ranked AS (
        SELECT id, ROW_NUMBER() OVER (
            PARTITION BY game_night_event_id
            ORDER BY started_at DESC NULLS LAST, id) AS rn
        FROM game_night_sessions WHERE status = 'InProgress')
    UPDATE game_night_sessions
    SET status = 'Pending'
    WHERE id IN (SELECT id FROM ranked WHERE rn > 1);");
```
`Down` drops the index (EF-generated). Verify the Session PK column is `id` at implement time.

### 3.3 Fix Path A orphaning — `FinalizeSessionCommandHandler` closes the link

After the existing finalize logic, close this session's `InProgress` link (idempotent, guarded so Path B's prior close is a no-op):
```csharp
var link = await _db.GameNightSessions
    .FirstOrDefaultAsync(l => l.SessionId == session.Id
        && l.Status == nameof(GameNightSessionStatus.InProgress), cancellationToken)
    .ConfigureAwait(false);
if (link is not null)
{
    link.Status = nameof(GameNightSessionStatus.Completed);
    link.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;
}
```
Path B safety: `CompleteGameNightSessionCommandHandler` already sets the link `Completed`; the `Status == InProgress` filter makes this a no-op there (and idempotent). Confirm at implement time that Path B and this handler resolve the SAME `GameNightSessionEntity` instance (identity resolution) to avoid a double-tracked conflict; if they diverge, guard accordingly.

## 4. Testing (integration, Testcontainers Postgres)

1. **Index blocks a 2nd live slot**: seed a GameNight + 1 `InProgress` link; inserting a 2nd `InProgress` link for the same `game_night_event_id` throws `DbUpdateException` (unique violation). A `Pending`/`Completed` 2nd link is allowed.
2. **Finalize frees the slot**: create session1 in a night (link `InProgress`) → `FinalizeSessionCommand` → assert its link is `Completed`; then a 2nd session in the same night persists (no violation).
3. **Cleanup migration** is exercised implicitly (tests run `MigrateAsync`, which applies it on a fresh DB — asserting no dup data path).

## 5. Files touched

| File | Change |
|---|---|
| `…/EntityConfigurations/GameManagement/GameNightSessionEntityConfiguration.cs` | Add the partial unique index |
| `…/Infrastructure/Migrations/<ts>_RestoreGameNightSessionInProgressUniqueIndex.cs` (+ Designer + snapshot) | EF-generated `CreateIndex` + prepended cleanup SQL |
| `…/SessionTracking/Application/Commands/FinalizeSessionCommandHandler.cs` | Close the `InProgress` link on finalize |
| `apps/api/tests/Api.Tests/…/SessionTracking/…` | Integration tests (index + finalize-frees-slot) |

## 6. Out of scope / follow-up (record on the issue)

- Realign the guard off `Status==Active` onto the link/IsLive; reconcile Path A/B writer semantics; warning #13 query — the broader C2 canonicalization.
- Optional hardening: catch `DbUpdateException`(23505) in `CreateSessionCommandHandler` and map to a clean 409 instead of 500 (the index now makes the race a real DB error).

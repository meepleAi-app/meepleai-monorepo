# ADR-066 — PlayRecord Ownership Model

**Status**: Proposed
**Date**: 2026-06-15
**Deciders**: @badsworm (pending ratification at PR review)
**Tracking**: [#2363](https://github.com/meepleAi-app/meepleai-monorepo/issues/2363) Wave 1 — sub-issue [#2358](https://github.com/meepleAi-app/meepleai-monorepo/issues/2358)
**Related**: [umbrella #2342](https://github.com/meepleAi-app/meepleai-monorepo/issues/2342) · [spec `docs/for-developers/specs/2026-06-14-mockup-us-coverage-map.md`](../../../for-developers/specs/2026-06-14-mockup-us-coverage-map.md) §4a US-INT-2

## Context

The `PlayRecord` aggregate (`apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/PlayRecord.cs`) already exists as a completed domain model. It has `CreatedByUserId` as the sole ownership field — the user who created the record. There is no `HostUserId` or participant-role distinction on the aggregate itself.

The current `UpdatePlayRecordCommandHandler` (`...Commands/PlayRecords/UpdatePlayRecordCommandHandler.cs:33-45`) performs a raw `_recordRepository.GetByIdAsync` and calls `record.UpdateDetails(...)` **with no auth check on the caller**. The command (`UpdatePlayRecordCommand`) carries only `RecordId` and nullable patch fields — there is no `RequesterUserId` in the command signature. This is an IDOR vulnerability: any authenticated user who knows the `RecordId` can mutate any record.

The same gap exists in `CompletePlayRecordCommandHandler`, `RecordScoreCommandHandler`, and `AddPlayerToRecordCommandHandler` — none of them check whether the caller is authorised to mutate the aggregate.

The comparable aggregates in the codebase enforce host-only mutation explicitly:
- `GameNightEvent`: `CancelGameNightCommandHandler` (`line 34`) asserts `gameNight.OrganizerId != command.UserId` before mutating. `UpdateGameNightCommandHandler` (`line 33`) does the same.
- `Session.SetScores`: `UpdateSessionScoresCommandHandler` (`lines 43-51`) has an explicit IDOR guard — the session owner **or** a registered participant (User-linked) can mutate scores, matching asse A semantic alignment PR c1efb4fb6.

`PlayRecord` is a personal-or-collaborative artifact: it is created by one user (`CreatedByUserId`) but the stored `Players` list may contain multiple registered users (`RecordPlayer.UserId != null`) and guests (`RecordPlayer.UserId == null`). The spec (US-INT-2, step 4, failure mode 3) anticipates concurrent edits from host and participants, and mentions optimistic concurrency via `xmin` (per ADR-060 pattern).

## Problem

The specific architectural question: **which roles can mutate a `PlayRecord` after creation — the creator only, all registered players, or a hybrid model?**

Without a decision, implementation work for US-INT-2b (create form + autosave) and US-INT-2c (detail + edit) will resolve the question inconsistently. The current handlers have an active IDOR vulnerability that must be closed before the feature reaches production.

Measurable impact if left undecided:
- US-INT-2 sub-issues 2b and 2c cannot be fully specced (edit permissions are product behaviour, not just backend policy).
- The IDOR surface on existing handlers will persist and widen as more mutation commands are added.

## Options Considered

### Option A — Creator-only edit (strict single-owner)

The creator (`CreatedByUserId`) is the sole authority. All mutation commands reject callers whose `UserId != record.CreatedByUserId`.

**Pros**:
- Simplest to implement — one field check, no join to `_players`.
- Zero edit-conflict surface — the record is a single-writer resource.
- Matches the `GameNightEvent` pattern exactly (organizer-only for cancel/update).

**Cons**:
- Friction: if Davide was the one who created the record, but Marco (the host of the game night) wants to correct a score, he cannot — he must ask Davide.
- The spec (US-INT-2, Cockburn step 4) says "Marco inserisce winner, score" where Marco is the game-night host but not necessarily the `PlayRecord` creator.
- No escape hatch: if the creator account is deleted or deactivated, the record becomes immutable by everyone.

**Risks**: Low engineering risk. High product friction for collaborative game-night groups.

**Code/path impact**: Add `RequesterUserId` to `UpdatePlayRecordCommand`, `CompletePlayRecordCommand`, `RecordScoreCommand`, `AddPlayerToRecordCommand`. Add guard in each handler: `if (record.CreatedByUserId != command.RequesterUserId) throw new ForbiddenException(...)`.

---

### Option B — All registered players edit

Any `RecordPlayer` with a non-null `UserId` (i.e. a User-linked player, not a guest) can mutate the record. Mirrors the `UpdateSessionScoresCommandHandler` pattern where owner OR participant can mutate.

**Pros**:
- Collaborative UX: all players at the table can contribute their own score / add notes without friction.
- Consistent with `UpdateSessionScoresCommandHandler` (IDOR guard: owner OR participant).
- Aligns with how board game groups operate in practice — everyone knows the final scores.

**Cons**:
- Edit conflicts: if two players edit simultaneously (failure mode 3 in US-INT-2 spec), optimistic concurrency (`xmin` per ADR-060) handles the DB collision, but there is no semantic merge — last write wins.
- A registered player can overwrite the host's corrections, reopening disputes.
- Guest players (null `UserId`) cannot edit at all, which can be surprising if a guest later creates an account.

**Risks**: Moderate. The `xmin` concurrency guard prevents data corruption but produces confusing UX (one edit silently discards the other).

**Code/path impact**: Same as Option A but guard becomes: `if (record.CreatedByUserId != command.RequesterUserId && !record.Players.Any(p => p.UserId == command.RequesterUserId)) throw new ForbiddenException(...)`. Requires `_players` to be loaded (EF join already in place via `IPlayRecordRepository`).

---

### Option C — Creator-edit + participant score-only edit (scoped hybrid)

The creator retains full edit authority (all fields: date, notes, location, visibility, player roster, scores). Registered participants can edit **only their own score entry** — they cannot edit metadata or other players' scores. No "suggest queue" (deferred to future enhancement if requested).

**Pros**:
- Balances autonomy and authority: Marco creates and controls the record; Davide can correct his own score if Marco entered it wrong.
- Scope-scoped mutations reduce conflict surface: metadata collisions (date/notes) remain creator-only; score collisions are per-player (two players cannot edit each other's row).
- Natural mapping to the `RecordScore` domain method (`PlayRecord.RecordScore(playerId, score, ...)`) — the `playerId` is already the scope key.
- Avoids a new "pending suggestion" entity (extra schema complexity, workflow UX cost) that is not in scope for US-INT-2.

**Cons**:
- Requires two distinct auth checks: creator-level for `UpdatePlayRecordCommand` / `CompletePlayRecordCommand` / `AddPlayerToRecordCommand`; player-scoped for `RecordScoreCommand`.
- A participant wanting to change their display name in the record (not their score) cannot — they must ask the creator. Edge case but real.

**Risks**: Moderate engineering. The scope boundary (`creator` vs `own-score`) must be clearly documented and enforced at every new mutation command added in future US.

**Code/path impact**: 
- `UpdatePlayRecordCommand`, `CompletePlayRecordCommand`, `AddPlayerToRecordCommand`: add `RequesterUserId`; guard: creator-only.
- `RecordScoreCommand`: add `RequesterUserId`; guard: creator OR the player whose `RecordPlayer.UserId == command.RequesterUserId && RecordPlayer.Id == command.PlayerId`.
- `PlayRecord` domain: no new methods needed — `RecordScore(playerId, ...)` already takes `playerId` as scope key.

---

### Option D — Full suggestion-queue (host approves participant corrections)

Participants submit correction "suggestions"; the creator approves/rejects them. Suggestions are a separate aggregate or a status-field on `RecordScore`.

**Pros**: Maximum authority for creator; full audit trail of who suggested what.

**Cons**: Significant scope expansion — new `PlayRecordSuggestion` entity, suggest/approve/reject commands, notification pipeline. Not in scope for US-INT-2 (16gg estimate already pushes limits). US-INT-2 spec has no step for suggestion review UX.

Rejected as out-of-scope for Tier 2 MVP.

---

## Decision

**Option C — Creator-edit + participant score-only edit.**

Rationale: Option A is product-unfriendly for collaborative game groups. Option B produces last-write-wins conflicts with no semantic resolution. Option D is out of scope. Option C provides a natural scope boundary that maps directly to the existing `RecordScore(playerId, ...)` domain method — the player's own score entry is already keyed by `playerId`, making the auth check mechanical. The creator retains authority over all structural mutations (metadata, roster, completion state), which matches the `GameNightEvent` pattern. Closing the active IDOR vulnerability on all existing handlers is also required as part of this work, regardless of which option is chosen.

## Consequences

### Positive

- IDOR vulnerability on `UpdatePlayRecordCommandHandler`, `CompletePlayRecordCommandHandler`, `RecordScoreCommandHandler`, `AddPlayerToRecordCommandHandler` is closed in a single PR.
- Scope-scoped mutations minimise conflict surface: metadata stays creator-only, score stays per-player.
- Consistent with the `GameNightEvent` pattern (creator authority) and partly consistent with `UpdateSessionScoresCommandHandler` (participant participation in their own data).

### Negative

- Participants cannot correct their display name in the roster without creator assistance — minor friction in the edge case where the creator typo'd a player's name.
- Every future mutation command added by US-INT-2b/2c must declare its scope at design time (creator-level or player-scoped).

### Trade-offs Accepted

- Last-write-wins on the creator's own edits is acceptable: the creator is the single writer for metadata, so no conflict exists there.
- Guest players (`RecordPlayer.UserId == null`) cannot edit anything. This is intentional — guest identity is not authenticated.
- The suggestion-queue UX (Option D) is deferred; it can be added later as `AddRecordSuggestionCommand` without schema changes to the current model.

## Implementation Guidance

**Step 1 — Close IDOR on existing handlers (required for any option)**

Add `Guid RequesterUserId` to all four mutation commands. In each handler, after loading the aggregate, apply the appropriate guard before calling the domain method:

```csharp
// Creator-level guard (UpdatePlayRecord, CompletePlayRecord, AddPlayerToRecord):
if (record.CreatedByUserId != command.RequesterUserId)
    throw new ForbiddenException($"User {command.RequesterUserId} is not the creator of PlayRecord {command.RecordId}.");

// Player-scoped guard (RecordScore):
var isCreator = record.CreatedByUserId == command.RequesterUserId;
var isOwnPlayer = record.Players.Any(p => p.UserId == command.RequesterUserId && p.Id == command.PlayerId);
if (!isCreator && !isOwnPlayer)
    throw new ForbiddenException($"User {command.RequesterUserId} cannot record score for player {command.PlayerId} in PlayRecord {command.RecordId}.");
```

**Step 2 — Wire `RequesterUserId` at routing layer**

Each endpoint extracts `UserId` from the JWT claims (`ClaimsPrincipal.GetUserId()` extension). Pass it as `RequesterUserId` when constructing the command. This is the same pattern used by `CancelGameNightCommandHandler` (command carries `command.UserId`) and `UpdateSessionScoresCommandHandler` (command carries `command.RequestedBy`).

**Step 3 — Unit tests**

For each handler, add a test asserting `ForbiddenException` is thrown when `RequesterUserId` is not the creator (or not the scoped player for `RecordScore`). Minimum 2 tests per handler: authorised path succeeds, unauthorised path throws.

**Step 4 — Future mutation commands (US-INT-2b/2c)**

When adding photo-upload or visibility-change commands for US-INT-2, declare the scope in the command summary comment: `// Creator-level: only CreatedByUserId may invoke`.

## Rollback / Reversibility

The auth guard additions are purely additive at the application layer — no schema change required. Rolling back means removing the `RequesterUserId` field from the commands and the guard from the handlers, which returns the handlers to their current (IDOR-vulnerable) state. The domain model (`PlayRecord.cs`) is unchanged.

## References

- Spec: `docs/for-developers/specs/2026-06-14-mockup-us-coverage-map.md` §4a US-INT-2, Required ADRs item 1
- Sub-issue: [#2358](https://github.com/meepleAi-app/meepleai-monorepo/issues/2358)
- Tracker: [#2363](https://github.com/meepleAi-app/meepleai-monorepo/issues/2363)
- IDOR pattern reference: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/UpdateSessionScoresCommandHandler.cs:43-51`
- GameNightEvent host-only pattern: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/CancelGameNightCommandHandler.cs:34-35`
- Existing PlayRecord handlers: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/`
- PlayRecord aggregate: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/PlayRecord.cs`
- US-INT-2 failure mode 3 (concurrent edit): `docs/for-developers/specs/2026-06-14-mockup-us-coverage-map.md` §4a US-INT-2 "Failure modes"

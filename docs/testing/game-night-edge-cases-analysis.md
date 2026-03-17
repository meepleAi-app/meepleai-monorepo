# Game Night Improvvisata — Edge Cases Analysis

**Date**: 2026-03-16 | **Epic**: #379 (CLOSED) | **Method**: Code-level verification

---

## Summary

| Edge Case | Status | Risk |
|-----------|--------|------|
| 1. PDF upload failure + retry | ✅ Fully Handled | Low |
| 2. Agent summary generation failure | ✅ Fully Handled | Low |
| 3. Guest join with expired invite | ⚠️ Partially Handled | Medium |
| 4. Score parse low confidence | ✅ Fully Handled | Low |
| 5. Concurrent session modifications | ✅ Fully Handled | Low |
| 6. PDF too large / corrupted | ✅ Fully Handled | Low |
| 7. Agent quota exceeded | ✅ Fully Handled | Low |
| 8. Session resume after long time | ⚠️ Partially Handled | Medium |

**Overall**: 6/8 fully handled, 2/8 partially handled (design choices, not bugs).

---

## Detailed Analysis

### 1. PDF Upload Failure + Retry ✅

**Files**: `DocumentProcessing/Domain/Entities/PdfDocument.cs`, `PdfFailedEvent.cs`

- **Retry mechanism**: `RetryCount` field with `MaxRetries = 3`
- **State machine**: 7-state pipeline (Pending → Uploading → Extracting → Chunking → Embedding → Indexing → Ready) with validated transitions
- **Retry method**: `Retry()` increments counter, resumes from failed state or Extracting
- **Notification**: `PdfFailedEvent` emitted with error category + `UploadedByUserId` for notification routing
- **Permanent failure**: After 3 retries, document enters `Failed` state permanently; event published for user notification

### 2. Agent Summary Generation Failure ✅

**Files**: `KnowledgeBase/Application/EventHandlers/GenerateAgentSummaryHandler.cs`, `ResumeSessionFromSnapshotCommand.cs:179-185`

- **Fire-and-forget design**: Handler catches ALL exceptions, logs errors but does NOT crash
- **LLM failure check**: Validates `result.Success` and checks for empty response
- **Fallback recap**: If LLM fails → returns `"Sessione ripresa dal turno {currentTurn}."`
- **Session continuation**: Pause/resume completes successfully regardless of summary generation outcome

### 3. Guest Join with Expired Invite ⚠️

**Files**: `SessionTracking/Application/Handlers/InviteHandlers.cs:35-78`, `JoinSessionByCodeCommandHandler.cs`

- **Expiry check**: `IsInviteTokenValid()` method checks expiration
- **Error response**: `SessionInviteResponse` with `CanJoin = false` and `ReasonCannotJoin = "Invite link has expired."`
- **Host regenerate**: `GenerateInviteTokenCommand` allows owner to issue fresh tokens (24h default)
- **Gap**: System uses invite tokens + session codes (6-char alphanumeric), not PIN. Semantics are correct but naming differs from spec ("PIN" vs "session code")

### 4. Score Parse Low Confidence ✅

**Files**: `KnowledgeBase/Application/Handlers/ParseAndRecordScoreCommandHandler.cs`

| Confidence | Behavior | UX |
|------------|----------|-----|
| ≥ 80% + clear match | Auto-record (if `AutoRecord = true`) | Instant update |
| 60-80% | `status: "parsed"` + `RequiresConfirmation = true` | User confirms |
| < 60% / ambiguous player | `status: "ambiguous"` + candidate list | User picks player |
| No match | `status: "parsed"` + message | User retypes |

### 5. Concurrent Session Modifications ✅

**Files**: `GameManagement/Domain/Entities/LiveGameSession.cs:64`

- **Optimistic concurrency**: `RowVersion byte[]` field with EF Core `DbUpdateConcurrencyException`
- **Atomic save**: Standard EF Core transaction isolation
- **SignalR ordering**: `ISessionSyncService.PublishEventAsync()` for real-time events
- **Validation order**: Status check → participant cap → add → DB save

### 6. PDF Too Large / Corrupted ✅

**Files**: `DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs:214-247`, `PdfProcessingOptions.cs`

- **Max file size**: 100 MB default (`MaxFileSizeBytes = 104857600`) + tier-specific limits
- **Validation chain**:
  1. File not null/empty
  2. File size vs tier limit
  3. Content-Type = `"application/pdf"` only
  4. `ValidatePdfStructureAsync()` checks PDF headers/trailers
- **Corrupted PDF**: Structure validation rejects with "Invalid PDF file structure"
- **Large PDF strategy**: Temp files for PDFs > 50 MB threshold
- **User feedback**: `"File is too large (X MB). Maximum is Y MB. Try compressing or splitting."`

### 7. Agent Quota Exceeded ✅

**Files**: `GameManagement/Application/EventHandlers/AutoCreateAgentOnPdfReadyHandler.cs:116-128`

- **Tier quota check**: `_tierEnforcementService.CanPerformAsync(userId, TierAction.CreateAgent)`
- **Quota full behavior**: Returns silently without throwing
  - Logs warning: `"Tier quota exceeded for UserId={UserId}. Agent auto-creation skipped."`
  - User can manually create agent later when quota available
- **Usage recording**: After successful creation, records usage with `RecordUsageAsync()`
- **UX**: PDF processes successfully; agent simply not auto-created

### 8. Session Resume After Long Time ⚠️

**Files**: `GameManagement/Application/Commands/GameNight/ResumeSessionFromSnapshotCommand.cs`

- **Max pause duration**: No enforced limit — sessions can remain paused indefinitely
- **Invite expiry**: Fresh invite issued on resume with 24-hour default expiry
- **Auto-save cleanup**: Old auto-save snapshots deleted on resume
- **Gap**: No warning if pause > N days; no automatic session archival
- **Risk**: Low — this is a design choice. Users won't lose data, they just need to be aware old invites expire.

---

## Recommendations

### No Action Needed (Design Choices)

| Item | Rationale |
|------|-----------|
| No max pause duration (#8) | Users should be able to resume whenever. Fresh invites solve guest access. |
| "Session code" vs "PIN" naming (#3) | Implementation is correct; spec naming is aspirational |

### Optional Improvements (Future)

| Item | Effort | Value |
|------|--------|-------|
| Warning notification if paused > 7 days | 0.5 days | Low — nice-to-have |
| Auto-archive after 30 days paused | 0.5 days | Low — cleanup |
| Explicit "invite expired" toast in frontend | 0.5 days | Medium — better UX for guests |
| Agent quota notification (instead of silent skip) | 0.5 days | Medium — user awareness |

---

**Last Updated**: 2026-03-16

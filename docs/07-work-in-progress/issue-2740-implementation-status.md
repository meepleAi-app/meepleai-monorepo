# Issue #2740 - Admin Alerts Implementation Status

**Issue**: [#2740 - Notifications - Admin Alerts](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2740)
**Date**: 2026-01-22
**Status**: ⚠️ **Blocked - Missing Dependencies**

---

## Summary

Implementation of issue #2740 requires foundational components from previous issues (#2719-#2739) that exist on `main` branch but are **not yet merged to `main-dev`**. Attempting to merge resulted in migration conflicts requiring manual resolution.

---

## Completed Work

### ✅ NotificationType Extension
- **File**: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationType.cs`
- **Changes**: Added 3 new admin notification types:
  - `AdminNewShareRequest` - Alert for new pending share requests
  - `AdminStaleShareRequests` - Warning for requests older than threshold
  - `AdminReviewLockExpiring` - Notification for expiring review locks

### 🎯 Branch Created
- **Branch**: `feature/issue-2740-admin-alerts`
- **Base**: `main-dev`
- **Status**: Ready for additional implementation once dependencies are available

---

## Missing Dependencies (Blocking)

The following components are required but **not available on `main-dev`**:

### 1. ShareRequest Domain (#2719)
- **Location**: `SharedGameCatalog/Domain/Entities/ShareRequest.cs`
- **Status**: Exists on `main` (commit `1972403be`)
- **Required for**: Event handlers, repository queries

### 2. ShareRequest Events (#2721)
- **Location**: `SharedGameCatalog/Domain/Events/ShareRequestEvents.cs`
- **Status**: Exists on `main` (commit `1972403be`)
- **Required for**: `ShareRequestCreatedEvent` event handler
- **Events needed**:
  - `ShareRequestCreatedEvent`
  - `ShareRequestReviewStartedEvent`
  - `ShareRequestLockExpiredEvent`

### 3. ShareRequest Repository (#2727)
- **Interface**: `IShareRequestRepository`
- **Status**: Not found in current codebase
- **Required methods**:
  ```csharp
  Task<PendingShareRequestStats> GetPendingStats(CancellationToken ct);
  Task<List<ShareRequest>> GetPendingOlderThan(DateTime threshold, CancellationToken ct);
  Task<int> CountPending(CancellationToken ct);
  Task<int> CountInReview(CancellationToken ct);
  ```

### 4. Admin Repository
- **Interface**: `IAdminRepository`
- **Status**: Not found in current codebase
- **Required methods**:
  ```csharp
  Task<List<Admin>> GetWithPermission(Permission permission, CancellationToken ct);
  ```
- **Required types**:
  - `Admin` entity with `Preferences` property
  - `Permission` enum with `ReviewShareRequests` value
  - `AdminPreferences` with notification flags

### 5. Notification Service
- **Interface**: `INotificationService`
- **Status**: Not found in current codebase
- **Required methods**:
  ```csharp
  Task CreateAsync(CreateNotificationDto dto, CancellationToken ct);
  ```

### 6. Cache Service
- **Interface**: `ICacheService`
- **Status**: Not found in current codebase
- **Required methods**:
  ```csharp
  Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory);
  ```

---

## Implementation Plan (Post-Merge)

Once dependencies are available, implement in this order:

### Phase 1: Event Handler
1. **NewShareRequestAdminAlertHandler**
   - Listen to `ShareRequestCreatedEvent`
   - Query admins with `ReviewShareRequests` permission
   - Respect admin notification preferences
   - Create notifications for each eligible admin

### Phase 2: Scheduled Jobs (Quartz.NET)
2. **AdminShareRequestDigestJob**
   - Schedule: Daily at 9:00 AM UTC
   - Query pending stats
   - Send digest email to admins with preference enabled
   - Skip if no pending requests

3. **StaleShareRequestWarningJob**
   - Schedule: Every 6 hours
   - Query requests older than 7 days
   - Send high-priority notifications to admins
   - Configurable threshold

### Phase 3: Dashboard Service
4. **AdminDashboardService**
   - Implement badge count caching (5-min TTL)
   - Aggregate pending + in-review counts
   - Expose via admin dashboard endpoint

### Phase 4: Email Template
5. **AdminShareRequestDigest.html**
   - Responsive HTML template
   - Display stats (total pending, oldest age, new today)
   - Breakdown by contribution type
   - Call-to-action link to review queue

### Phase 5: Testing
6. **Integration Tests**
   - Event handler test with admin permission check
   - Job execution tests with time mocking
   - Dashboard service cache behavior
   - Email template rendering

---

## Recommended Next Steps

### Option A: Merge `main` → `main-dev` (Recommended)
1. Resolve migration conflicts manually
2. Update `main-dev` with ShareRequest foundation
3. Complete issue #2740 implementation
4. Benefits: Clean merge history, full feature availability

### Option B: Work on `main` Branch
1. Checkout `main` branch
2. Create `feature/issue-2740-admin-alerts` from `main`
3. Complete implementation
4. Merge to `main` when ready
5. Later sync `main-dev` with resolved conflicts

### Option C: Wait for Sync
1. Wait for `main` → `main-dev` sync by team
2. Resume implementation on updated `main-dev`
3. Benefits: No merge conflict responsibility

---

## Files Modified (Current)

```
apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationType.cs
```

---

## Acceptance Criteria Status

- [ ] Admin alert on new share request (blocked - missing dependencies)
- [ ] Daily digest email with pending stats (blocked - missing dependencies)
- [ ] Stale request warnings (> 7 days) (blocked - missing dependencies)
- [ ] Respect admin notification preferences (blocked - missing Admin entity)
- [ ] Dashboard badge count for pending requests (blocked - missing repository)
- [ ] Configurable thresholds (stale days, digest time) (not started)
- [ ] Aggregation to avoid notification spam (not started)
- [ ] Integration tests for jobs (blocked - dependencies)
- [ ] Test coverage ≥ 90% (not started)

**Overall Progress**: 1/9 criteria (11%) - NotificationType extension only

---

## Contact

For questions or to proceed with Option A/B, coordinate with:
- **Issue Tracker**: #2740
- **Epic Tracker**: #2718
- **Related PRs**: #2902 (ShareRequest foundation on `main`)

---

**Last Updated**: 2026-01-22 17:45 UTC

# ADR-019: Two-Step Delete Workflow for SharedGameCatalog

**Status**: Accepted
**Date**: 2026-01-14
**Deciders**: Development Team
**Issue**: #2425 (Parent: #2374 Phase 5)

---

## Context

SharedGameCatalog games are **community assets** that may have:
- **User dependencies**: Personal game collections linked via `SharedGameId` FK
- **Document dependencies**: PDF rules, FAQs, errata attached to games
- **Audit requirements**: Governance and accountability for catalog changes

Accidental or malicious deletion could:
- Break user collections (orphaned `SharedGameId` references)
- Lose valuable community-contributed content (FAQs, errata)
- Violate governance policies (who approved this deletion?)

We needed a delete workflow that balances **editor productivity** with **data safety**.

---

## Decision

**We implement a two-step delete workflow**:

### Step 1: Editor Requests Deletion
```csharp
// Command: RequestDeleteSharedGameCommand
// Authorization: AdminOrEditorPolicy
// Result: Creates SharedGameDeleteRequest entity with status=Pending
POST /api/v1/admin/shared-games/{id}/request-delete
{
  "reason": "Duplicate entry - game exists as ID xyz"
}
```

### Step 2: Admin Reviews and Approves
```csharp
// Query: GetPendingDeleteRequestsQuery
// Authorization: AdminOnlyPolicy
GET /api/v1/admin/shared-games/delete-requests?status=Pending

// Command: ApproveDeleteRequestCommand
// Authorization: AdminOnlyPolicy
// Result: Soft deletes game (sets is_deleted=true)
POST /api/v1/admin/shared-games/delete-requests/{requestId}/approve

// OR

// Command: RejectDeleteRequestCommand
// Authorization: AdminOnlyPolicy
// Result: Updates request status=Rejected, game unchanged
POST /api/v1/admin/shared-games/delete-requests/{requestId}/reject
{
  "reason": "Game is actively used, cannot delete"
}
```

---

## Rationale

### 1. Prevent Accidental Data Loss
**Scenario**: Editor mistakenly clicks "Delete" on wrong game.

**Without workflow**:
- ❌ Game immediately deleted (soft or hard)
- ❌ User collections broken (SharedGameId references orphaned)
- ❌ No recovery mechanism (unless database backup restored)

**With workflow**:
- ✅ Delete request queued, game remains active
- ✅ Admin reviews before action taken
- ✅ Mistake caught before data loss

### 2. Audit Trail for Governance
**Requirement**: Catalog changes must be traceable.

**With two-step workflow**:
- ✅ Delete request logged with `RequestedBy` user and `Reason`
- ✅ Approval/rejection logged with `ReviewedBy` admin and decision
- ✅ Full timeline visible: Request → Review → Outcome
- ✅ Audit logs via `SharedGameDeleteRequestedEvent` and `SharedGameDeletedEvent`

**Governance compliance**: Can answer "Who deleted this? Why? Who approved it?"

### 3. Dependency Review Opportunity
**Before deletion, admin can check**:
- How many users have this game in their collections?
- Are there valuable FAQs/errata attached?
- Is this truly a duplicate or just similar?

**Query to check dependencies**:
```sql
SELECT COUNT(*) FROM games WHERE shared_game_id = '{gameId}';
```

If count > 0, admin may reject deletion and suggest archive instead.

### 4. Community Catalog Governance Model
**Principle**: Shared resources require shared responsibility.

- **Editors**: Can propose changes (including deletion)
- **Admins**: Act as stewards, approve/reject proposals
- **Separation of Powers**: No single role can unilaterally delete

This aligns with community governance best practices (Wikipedia model).

---

## Alternatives Considered

### Alternative 1: Direct Delete (Editor can delete immediately)
**Rejected**: Too risky for shared catalog.

**Problems**:
- Accidental deletions are irreversible (unless DB backup)
- No review opportunity for dependencies
- Violates governance principle (shared responsibility)

**When acceptable**: Personal user data (e.g., user's own game collection)

### Alternative 2: Hard Delete (Permanent removal from database)
**Rejected**: Violates audit requirements.

**Problems**:
- Data permanently lost (cannot analyze deletion patterns)
- Breaks foreign key integrity if dependencies exist
- No recovery mechanism

**Decision**: Use soft delete (`is_deleted = true`) for recoverability.

### Alternative 3: Archive Instead of Delete
**Rejected as sole mechanism**: Archives games from active catalog but doesn't handle duplicates/errors.

**Why not archive only?**
- Duplicates should be deleted, not archived
- Data quality errors (wrong game, wrong BGG ID) should be removed
- Archive is for "out of print" or "no longer supported" games

**Decision**: Archive AND delete both available, with delete requiring approval.

---

## Workflow Implementation

### Entities
```csharp
// Domain/Entities/SharedGameDeleteRequest.cs
public class SharedGameDeleteRequest
{
    public Guid Id { get; private set; }
    public Guid SharedGameId { get; private set; }  // FK to SharedGame
    public string Reason { get; private set; }      // Editor's justification
    public Guid RequestedBy { get; private set; }   // Editor user ID
    public DeleteRequestStatus Status { get; private set; }  // Pending/Approved/Rejected
    public Guid? ReviewedBy { get; private set; }   // Admin user ID (nullable until reviewed)
    public string? ReviewNotes { get; private set; } // Admin's decision notes
    public DateTime RequestedAt { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
}

public enum DeleteRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
```

### Commands
- `RequestDeleteSharedGameCommand` - Editor creates request
- `ApproveDeleteRequestCommand` - Admin approves → soft delete game
- `RejectDeleteRequestCommand` - Admin rejects → close request, keep game

### Queries
- `GetPendingDeleteRequestsQuery` - Admin reviews pending requests

### Domain Events
- `SharedGameDeleteRequestedEvent` - Audit log creation
- `SharedGameDeletedEvent` - Audit log for approval + deletion

---

## Consequences

### Positive
- ✅ Prevents accidental data loss
- ✅ Provides audit trail for governance
- ✅ Allows dependency review before deletion
- ✅ Aligns with community catalog model
- ✅ Recoverable via soft delete
- ✅ Transparent (editors know their requests may be rejected)

### Negative
- ❌ Additional complexity (delete request entity, workflow commands)
- ❌ UX friction (editors cannot delete immediately)
- ❌ Admin workload (must review delete requests)

### Mitigation Strategies
- **Complexity**: Standard CQRS pattern, well-documented
- **UX Friction**: Educate editors that review protects catalog quality
- **Admin Workload**: Batch review in admin UI, email notifications for pending requests

---

## User Experience

### Editor Flow
1. Navigate to game in admin UI
2. Click "Request Deletion" button
3. Fill reason: "Duplicate of game xyz"
4. Submit → Toast "Delete request submitted for admin review"
5. Game remains visible (status: Published)
6. Email notification when admin reviews

### Admin Flow
1. Navigate to "Pending Delete Requests" page
2. See list: Game title, Requested by, Reason, Requested date
3. Click "Review" → See game details + dependency count
4. Decision:
   - **Approve**: Confirm → Game soft deleted
   - **Reject**: Enter reason → Request closed, game unchanged
5. Editor notified of decision

---

## Compliance

This decision aligns with:
- **Data Protection**: Prevent unauthorized or accidental data loss
- **Governance**: Audit trail and separation of powers
- **Domain-Driven Design**: Workflow encodes business rules in domain model

---

## Future Considerations

### Potential Enhancements
- **Auto-approve after N days**: If no dependencies and editor is trusted
- **Bulk delete**: Admin can approve multiple requests at once
- **Email notifications**: Alert editors when their requests are reviewed
- **Dependency visualization**: Show impact graph before approval

### When to Reconsider
- If admin workload becomes unsustainable (> 50 requests/week)
- If editor trust model changes (verified editors get direct delete)
- If data recovery requirements become stricter (require hard delete with archiving)

---

## References

- Issue #2370: Phase 1 (Delete workflow entities created)
- Issue #2372: Phase 3 (Admin UI for delete request review)
- Commands implemented:
  - `RequestDeleteSharedGameCommand`
  - `ApproveDeleteRequestCommand`
  - `RejectDeleteRequestCommand`
- Query: `GetPendingDeleteRequestsQuery`
- Events: `SharedGameDeleteRequestedEvent`, `SharedGameDeletedEvent`

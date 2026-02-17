# Share Request System - Implementation Guide

**Developer reference for Share Request workflow implementation**

---

## Architecture

**Bounded Context**: SharedGameCatalog
**Location**: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/`

**Structure**:
```
Domain/
  Entities/     ShareRequestEntity (aggregate), ShareRequestDocumentEntity, ContributorEntity
  ValueObjects/ ShareRequestStatus (6 states), ContributionType, BadgeType
  Events/       Created, Approved, ReviewLockAcquired domain events
Application/
  Commands/     Create, Approve, Reject, RequestChanges, Withdraw, StartReview, ReleaseReview
  Queries/      GetPending, GetUserRequests, GetMyActiveReviews, GetUserContributionStats
  Handlers/     Command/Query handlers (CQRS + FluentValidation)
Infrastructure/
  Repositories/ ShareRequestRepository, ContributorRepository
  Jobs/         ReleaseExpiredReviewLocksJob (Hangfire hourly)
```

---

## Domain Model

### ShareRequest Aggregate

**Behaviors**:
```csharp
// Creation
Create(userId, sourceGameId, notes, contributionType)

// Admin State Transitions
StartReview(reviewerId) → InReview
Approve(reviewerId, sharedGameId, adminNotes) → Approved
Reject(reviewerId, reason, adminNotes) → Rejected
RequestChanges(reviewerId, feedback, adminNotes) → ChangesRequested
ReleaseReview(reviewerId) → Pending

// User State Transitions
Withdraw(userId) → Withdrawn
Resubmit(userId) → Pending

// Document Management
AttachDocument(documentId, fileName)
RemoveDocument(documentId)
UpdateDocuments(List<Guid>)

// Query Helpers
CanBeReviewed(), IsLockedForReview(), IsResolvedState(), CanBeModifiedByUser()
```

**Invariant Guards** (throw exceptions on violation):
- Approve: Status=InReview, ReviewedBy=reviewerId, lock not expired
- StartReview: Status=Pending, no existing lock
- User actions: UserId matches owner, state allows modification

### Contributor Aggregate

**Purpose**: Track contribution stats (eventual consistency via domain events)

**Fields**: `UserId`, `ApprovedCount`, `RejectedCount`, `PendingCount`, `ApprovalRate`, `Badges`
**Methods**: `IncrementApproved()`, `IncrementRejected()`, `RecalculateApprovalRate()`

---

## Application Layer (CQRS)

**Commands**: Create, Approve, Reject, RequestChanges, Withdraw, StartReview, ReleaseReview
**Queries**: GetPending, GetUserRequests, GetDetails, GetMyActiveReviews, GetUserContributionStats

### Handler Pattern

```csharp
// 1. Load aggregate
var shareRequest = await _repository.GetByIdAsync(request.ShareRequestId);

// 2. Execute domain logic (raises events)
shareRequest.Approve(request.AdminId, sharedGameId, request.AdminNotes);

// 3. Persist (EF Core tracks events)
await _repository.UpdateAsync(shareRequest);

// 4. Events dispatched after SaveChanges by MeepleAiDbContext
```

**Rule**: Use repository pattern, not DbContext directly.

### Validation Example

```csharp
RuleFor(x => x.SourceGameId)
    .NotEmpty()
    .MustAsync(async (cmd, gameId, ct) =>
        await libraryRepo.GameExistsInUserLibraryAsync(cmd.UserId, gameId, ct))
    .WithMessage("Source game not found in user library");

RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes != null);

RuleFor(x => x)
    .MustAsync(async (cmd, ct) =>
        (await rateLimitRepo.GetUserRateLimitAsync(cmd.UserId, ct)).CanCreateRequest())
    .WithMessage("Rate limit exceeded");
```

---

## Infrastructure Layer

### EF Core Configuration

**ShareRequestEntityConfiguration**:

```csharp
public class ShareRequestEntityConfiguration : IEntityTypeConfiguration<ShareRequestEntity>
{
    public void Configure(EntityTypeBuilder<ShareRequestEntity> builder)
    {
        builder.ToTable("share_requests", "shared_game_catalog");

        builder.HasKey(x => x.Id);

        // Enum as int (performance)
        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ContributionType)
            .HasConversion<int>()
            .IsRequired();

        // Review lock (nullable when not locked)
        builder.Property(x => x.ReviewedBy).IsRequired(false);
        builder.Property(x => x.ReviewLockExpiresAt).IsRequired(false);

        // Soft delete
        builder.HasQueryFilter(x => !x.IsDeleted);

        // Indexes for performance
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.ReviewedBy, x.Status });
        builder.HasIndex(x => x.CreatedAt);

        // Relationships
        builder.HasMany(x => x.Documents)
            .WithOne()
            .HasForeignKey(d => d.ShareRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### Repository Pattern

**Interface** (in Domain):
```csharp
public interface IShareRequestRepository
{
    Task<ShareRequestEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ShareRequestEntity>> GetPendingAsync(int skip, int take, CancellationToken ct);
    Task AddAsync(ShareRequestEntity entity, CancellationToken ct = default);
    Task UpdateAsync(ShareRequestEntity entity, CancellationToken ct = default);
    Task<bool> HasActiveReviewLockAsync(Guid requestId, CancellationToken ct);
    Task<List<ShareRequestEntity>> GetExpiredLocksAsync(CancellationToken ct);
}
```

**Implementation** (in Infrastructure):
```csharp
public class ShareRequestRepository : IShareRequestRepository
{
    private readonly MeepleAiDbContext _context;

    public async Task<ShareRequestEntity?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.ShareRequests
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    // ... other methods
}
```

---

## API Layer

**Endpoint Pattern** (Location: `Routing/SharedGameCatalogEndpoints.cs`):

```csharp
group.MapPost("/share-requests", async (
    [FromBody] CreateShareRequestDto dto,
    [FromServices] IMediator mediator,
    ClaimsPrincipal user,
    CancellationToken ct) =>
{
    var command = new CreateShareRequestCommand(
        user.GetUserId(), dto.SourceGameId, dto.Notes, dto.AttachedDocumentIds);
    var response = await mediator.Send(command, ct);
    return Results.Created($"/api/v1/share-requests/{response.ShareRequestId}", response);
})
.RequireAuthorization()
.RequireRateLimiting("ShareRequestCreation");
```

**🔴 CRITICAL**: Endpoints use ONLY `IMediator.Send()` - NEVER inject services directly.

### Rate Limiting

**Tier Limits**: Free=3, Premium=10, Pro=Unlimited (30-day sliding window)

```csharp
// Program.cs
options.AddPolicy("ShareRequestCreation", context =>
    RateLimitPartition.GetSlidingWindowLimiter(
        partitionKey: context.User.GetUserId().ToString(),
        factory: _ => new SlidingWindowRateLimiterOptions {
            Window = TimeSpan.FromDays(30),
            PermitLimit = GetUserTierLimit(context.User),
            SegmentsPerWindow = 30
        }));
```

---

## Event Handling

**MeepleAiDbContext Pattern**:
1. Collect domain events from entities during `SaveChanges`
2. Save changes first (transaction)
3. Dispatch events after successful save via `_mediator.Publish()`

**Badge Assignment** (Trigger: `ShareRequestApprovedDomainEvent`):
```csharp
// 1. Get/create contributor
// 2. IncrementApproved()
// 3. EvaluateEligibleBadgesAsync()
// 4. AssignBadge() for each new badge (raises BadgeEarnedDomainEvent)
// 5. UpdateAsync()
```

---

## Testing

| Type | Focus | Pattern | Example |
|------|-------|---------|---------|
| **Unit** | Domain logic | No infra, mocks | `ShareRequestTests`: `Approve_WhenInReview_TransitionsToApproved()` |
| **Integration** | Repository | InMemory DB isolation | `ShareRequestRepositoryTests`: `GetExpiredLocksAsync_ReturnsOnlyExpiredLocks()` |
| **E2E** | Full flow | Playwright | `share-game-flow.spec.ts`: Wizard → Approve → Rate limit (Issue #2954) |

---

## Infrastructure

### EF Core Configuration

**Pattern**:
```csharp
builder.ToTable("share_requests", "shared_game_catalog");
builder.Property(x => x.Status).HasConversion<int>();  // Enum → int
builder.HasQueryFilter(x => !x.IsDeleted);  // Soft delete
builder.HasIndex(x => x.UserId);
builder.HasIndex(x => new { x.ReviewedBy, x.Status });
builder.HasMany(x => x.Documents).WithOne().OnDelete(DeleteBehavior.Cascade);
```

**Critical Indexes**:
```sql
CREATE INDEX idx_share_requests_status_created ON share_requests(status, created_at DESC);
CREATE INDEX idx_share_requests_user_id ON share_requests(user_id, created_at DESC);
CREATE INDEX idx_share_requests_lock_expires ON share_requests(review_lock_expires_at) WHERE status = 1;
```

### Background Jobs

**ReleaseExpiredReviewLocksJob** (Hangfire hourly):
```csharp
[AutomaticRetry(Attempts = 3)]
public async Task Execute() {
    var expiredLocks = await GetExpiredLocksAsync();
    foreach (var (requestId, reviewerId) in expiredLocks) {
        await _mediator.Send(new ReleaseReviewCommand(requestId, reviewerId, isAutomaticRelease: true));
    }
}
```

**Registration**: `RecurringJob.AddOrUpdate<ReleaseExpiredReviewLocksJob>("release-expired-review-locks", job => job.Execute(), Cron.Hourly);`

---

## Frontend Integration

**Share Game Wizard** (`ShareGameWizard.tsx`):
```
Game Selection → Game Info (BGG auto-fill) → Details (players/playtime) → Documents (PDF) → Review → Submit
→ POST /api/v1/share-requests → Redirect /my-requests
```

**Admin Dashboard** (`admin/share-requests/page.tsx`):
- Server-side pagination (React Query)
- SSE real-time lock updates
- Optimistic UI for lock acquisition
- 409 Conflict → "Already in review by another admin"

---

## Performance & Security

### Query Optimization

**Pattern**: `.AsNoTracking()` + Early DTO projection + Index usage

```csharp
.AsNoTracking().Where(x => x.Status == ShareRequestStatus.Pending)
.OrderBy(x => x.CreatedAt).Select(x => new AdminShareRequestDto { /* minimal fields */ })
```

**Caching**: Leaderboard → `[OutputCache(Duration = 3600)]` (1 hour)

**Avoid**: ❌ Full entities, ❌ N+1 queries, ❌ In-memory filtering

### Authorization

| Endpoint | Policy | Check |
|----------|--------|-------|
| User endpoints | `.RequireAuthorization()` | Verify `request.UserId == currentUserId` |
| Admin endpoints | `.RequireAuthorization("AdminOnlyPolicy")` | Verify lock ownership before modify |

### Common Issues

| Issue | Symptom | Fix |
|-------|---------|-----|
| ObjectDisposedException | Disposed DbContext | Use scoped instances, not fields (Issue #2707) |
| 57P01 parallel error | Testcontainers connection terminated | Retry logic with `WaitAndRetryAsync(3)` (Issue #2920) |
| FK constraint violation | `FK_share_requests_users` error | Seed parent before child (Issue #2620) |

### Performance Targets (p95)

| Operation | Target | Measured |
|-----------|--------|----------|
| Create | <200ms | ~150ms |
| List pending | <100ms | ~80ms |
| Approve | <500ms | ~400ms |

---

**Last Updated**: 2026-01-23 • **Epic**: Issue #2718 • **Maintainer**: Backend Team

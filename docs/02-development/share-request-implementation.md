# Share Request System - Implementation Guide

**Developer reference for Share Request workflow implementation**

## Architecture Overview

### Bounded Context

**SharedGameCatalog** - Owner del Share Request aggregate

**Location**: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/`

**Structure**:
```
SharedGameCatalog/
├── Domain/
│   ├── Entities/
│   │   ├── ShareRequestEntity.cs           # Aggregate root
│   │   ├── ShareRequestDocumentEntity.cs   # Value entity
│   │   └── ContributorEntity.cs            # Separate aggregate
│   ├── ValueObjects/
│   │   ├── ShareRequestStatus.cs           # Enum (6 states)
│   │   ├── ContributionType.cs             # Enum (NewGame, AdditionalContent)
│   │   └── BadgeType.cs                    # Badge definitions
│   └── Events/
│       ├── ShareRequestCreatedDomainEvent.cs
│       ├── ShareRequestApprovedDomainEvent.cs
│       └── ReviewLockAcquiredDomainEvent.cs
├── Application/
│   ├── Commands/
│   │   ├── CreateShareRequestCommand.cs    # User creates request
│   │   ├── ApproveShareRequestCommand.cs   # Admin approves
│   │   ├── RejectShareRequestCommand.cs    # Admin rejects
│   │   └── StartReview/StartReviewCommand.cs # Admin acquires lock
│   ├── Queries/
│   │   ├── GetPendingShareRequestsQuery.cs # Admin dashboard
│   │   ├── GetUserShareRequestsQuery.cs    # User's requests
│   │   └── GetMyActiveReviewsQuery.cs      # Admin's locked reviews
│   └── EventHandlers/
│       └── BadgeEvaluationOnApprovalHandler.cs
└── Infrastructure/
    ├── Repositories/
    │   ├── ShareRequestRepository.cs
    │   └── ContributorRepository.cs
    └── BackgroundJobs/
        └── ReleaseExpiredReviewLocksJob.cs  # Hangfire, hourly
```

---

## Domain Model

### ShareRequest Aggregate

**Key Behaviors**:

```csharp
public class ShareRequestEntity
{
    // Creation
    public static ShareRequestEntity Create(
        Guid userId,
        Guid sourceGameId,
        string? notes,
        ContributionType contributionType);

    // State Transitions (Admin)
    public void StartReview(Guid reviewerId);
    public void Approve(Guid reviewerId, Guid sharedGameId, string? adminNotes);
    public void Reject(Guid reviewerId, string reason, string? adminNotes);
    public void RequestChanges(Guid reviewerId, string feedback, string? adminNotes);
    public void ReleaseReview(Guid reviewerId);

    // State Transitions (User)
    public void Withdraw(Guid userId);
    public void Resubmit(Guid userId);

    // Document Management
    public void AttachDocument(Guid documentId, string fileName);
    public void RemoveDocument(Guid documentId);
    public void UpdateDocuments(List<Guid> documentIds);

    // Query Helpers
    public bool CanBeReviewed();
    public bool IsLockedForReview();
    public bool IsResolvedState();
    public bool CanBeModifiedByUser();
}
```

**Invariant Enforcement**:
```csharp
public void Approve(Guid reviewerId, ...)
{
    if (Status != ShareRequestStatus.InReview)
        throw new InvalidOperationException("Can only approve requests in review");

    if (ReviewedBy != reviewerId)
        throw new UnauthorizedAccessException("Only reviewing admin can approve");

    if (IsLockExpired())
        throw new ReviewLockExpiredException("Review lock has expired");

    // Proceed with approval...
}
```

### Contributor Aggregate

**Purpose**: Track user contribution statistics independently

**Separation Rationale**:
- ShareRequest è transactional (single request lifecycle)
- Contributor è analytical (aggregate stats across all requests)
- Eventual consistency via domain events è accettabile

**Fields**:
```csharp
public class ContributorEntity
{
    public Guid UserId { get; private set; }  // FK to User
    public int ApprovedCount { get; private set; }
    public int RejectedCount { get; private set; }
    public int PendingCount { get; private set; }
    public double ApprovalRate { get; private set; }
    public List<BadgeEntity> Badges { get; private set; }

    public void IncrementApproved();
    public void IncrementRejected();
    public void IncrementPending();
    public void RecalculateApprovalRate();
}
```

---

## Application Layer

### CQRS Pattern

**Commands** (write operations):
- `CreateShareRequestCommand` → `CreateShareRequestCommandHandler`
- `ApproveShareRequestCommand` → `ApproveShareRequestCommandHandler`
- `RejectShareRequestCommand` → `RejectShareRequestCommandHandler`
- `RequestShareRequestChangesCommand` → Handler
- `WithdrawShareRequestCommand` → Handler
- `StartReviewCommand` → Handler (nested folder for organization)
- `ReleaseReviewCommand` → Handler (nested folder)

**Queries** (read operations):
- `GetPendingShareRequestsQuery` → Paginated admin dashboard
- `GetUserShareRequestsQuery` → User's request list
- `GetShareRequestDetailsQuery` → Full details for review
- `GetMyActiveReviewsQuery` → Admin's locked reviews
- `GetUserContributionStatsQuery` → User profile stats

### Command Handler Pattern

**Example**: `ApproveShareRequestCommandHandler`

```csharp
public class ApproveShareRequestCommandHandler
    : IRequestHandler<ApproveShareRequestCommand, ApproveShareRequestResponse>
{
    private readonly IShareRequestRepository _repository;
    private readonly ISharedGameRepository _gameRepository;
    private readonly ILogger<ApproveShareRequestCommandHandler> _logger;

    public async Task<ApproveShareRequestResponse> Handle(
        ApproveShareRequestCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate
        var shareRequest = await _repository.GetByIdAsync(request.ShareRequestId);

        // 2. Execute domain logic (raises events)
        var sharedGameId = CreateSharedGameFromRequest(shareRequest);
        shareRequest.Approve(request.AdminId, sharedGameId, request.AdminNotes);

        // 3. Persist changes (EF Core tracks events)
        await _repository.UpdateAsync(shareRequest);

        // 4. Events dispatched after SaveChanges by MeepleAiDbContext

        return new ApproveShareRequestResponse(
            shareRequest.Id,
            shareRequest.Status,
            sharedGameId,
            shareRequest.ResolvedAt
        );
    }
}
```

**Important**: Handler usa repository pattern, non DbContext diretto.

### FluentValidation

**Example**: `CreateShareRequestCommandValidator`

```csharp
public class CreateShareRequestCommandValidator
    : AbstractValidator<CreateShareRequestCommand>
{
    public CreateShareRequestCommandValidator(
        IUserLibraryRepository libraryRepo,
        IRateLimitRepository rateLimitRepo)
    {
        RuleFor(x => x.SourceGameId)
            .NotEmpty()
            .MustAsync(async (cmd, gameId, ct) =>
                await libraryRepo.GameExistsInUserLibraryAsync(cmd.UserId, gameId, ct))
            .WithMessage("Source game not found in user library");

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .When(x => x.Notes != null);

        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
            {
                var limit = await rateLimitRepo.GetUserRateLimitAsync(cmd.UserId, ct);
                return limit.CanCreateRequest();
            })
            .WithMessage("Rate limit exceeded for your tier");
    }
}
```

**Testing**: Unit test validators separately con mocked repositories.

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

### Endpoint Registration

**Location**: `apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs`

**Pattern**:
```csharp
private static void MapUserShareRequestEndpoints(RouteGroupBuilder group)
{
    group.MapPost("/share-requests", async (
        [FromBody] CreateShareRequestDto dto,
        [FromServices] IMediator mediator,
        ClaimsPrincipal user,
        CancellationToken ct) =>
    {
        var userId = user.GetUserId();
        var command = new CreateShareRequestCommand(
            userId,
            dto.SourceGameId,
            dto.Notes,
            dto.AttachedDocumentIds
        );

        var response = await mediator.Send(command, ct);
        return Results.Created($"/api/v1/share-requests/{response.ShareRequestId}", response);
    })
    .RequireAuthorization()
    .RequireRateLimiting("ShareRequestCreation")
    .WithName("CreateShareRequest")
    .Produces<CreateShareRequestResponse>(StatusCodes.Status201Created);
}
```

**CRITICAL**: Endpoints **MUST use ONLY `IMediator.Send()`** - NEVER inject services directly.

### Rate Limiting Configuration

**Startup Registration** (`Program.cs`):

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("ShareRequestCreation", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.User.GetUserId().ToString(),
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                Window = TimeSpan.FromDays(30),
                PermitLimit = GetUserTierLimit(context.User),
                SegmentsPerWindow = 30
            }));
});
```

**Tier Limit Lookup**:
```csharp
private static int GetUserTierLimit(ClaimsPrincipal user)
{
    var tier = user.FindFirst("tier")?.Value ?? "free";
    return tier.ToLowerInvariant() switch
    {
        "pro" => int.MaxValue,
        "premium" => 10,
        _ => 3
    };
}
```

---

## Event Handling

### Domain Event Dispatch

**MeepleAiDbContext** raccoglie e dispatcha eventi dopo `SaveChanges`:

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    // Collect domain events from entities
    var domainEvents = ChangeTracker.Entries<IEntity>()
        .SelectMany(entry => entry.Entity.DomainEvents)
        .ToList();

    // Save changes first (transaction)
    var result = await base.SaveChangesAsync(ct);

    // Dispatch events after successful save
    foreach (var domainEvent in domainEvents)
    {
        await _mediator.Publish(domainEvent, ct);
    }

    return result;
}
```

### Badge Assignment Handler

**Trigger**: `ShareRequestApprovedDomainEvent`

```csharp
public class BadgeEvaluationOnApprovalHandler
    : INotificationHandler<ShareRequestApprovedDomainEvent>
{
    private readonly IContributorRepository _contributorRepo;
    private readonly IBadgeEvaluator _evaluator;

    public async Task Handle(ShareRequestApprovedDomainEvent evt, CancellationToken ct)
    {
        // 1. Get or create contributor
        var contributor = await _contributorRepo.GetByUserIdAsync(evt.UserId, ct)
            ?? Contributor.Create(evt.UserId);

        // 2. Increment approved count
        contributor.IncrementApproved();

        // 3. Evaluate badge eligibility
        var newBadges = await _evaluator.EvaluateEligibleBadgesAsync(contributor, ct);

        // 4. Assign new badges
        foreach (var badge in newBadges)
        {
            contributor.AssignBadge(badge);
            // Raises BadgeEarnedDomainEvent
        }

        // 5. Persist
        await _contributorRepo.UpdateAsync(contributor, ct);
    }
}
```

---

## Testing Strategy

### Unit Tests

**Domain Logic** (no infrastructure):
```csharp
// tests/Api.Tests/SharedGameCatalog/Domain/Entities/ShareRequestTests.cs
public class ShareRequestTests
{
    [Fact]
    public void Approve_WhenInReview_TransitionsToApproved()
    {
        // Arrange
        var request = ShareRequestEntity.Create(userId, gameId, "notes", ContributionType.NewGame);
        request.StartReview(adminId);

        // Act
        request.Approve(adminId, sharedGameId, "admin notes");

        // Assert
        request.Status.Should().Be(ShareRequestStatus.Approved);
        request.SharedGameId.Should().Be(sharedGameId);
        request.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public void Approve_WhenNotInReview_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = ShareRequestEntity.Create(userId, gameId, "notes", ContributionType.NewGame);
        // NOT in review

        // Act
        Action act = () => request.Approve(adminId, sharedGameId, "notes");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*in review*");
    }
}
```

### Integration Tests (Repository)

**Pattern**: InMemory database per isolation

```csharp
// tests/Api.Tests/SharedGameCatalog/Infrastructure/ShareRequestRepositoryTests.cs
public class ShareRequestRepositoryTests : IAsyncLifetime
{
    private readonly DbContextOptions<MeepleAiDbContext> _options;
    private MeepleAiDbContext _context = null!;
    private ShareRequestRepository _sut = null!;

    public async Task InitializeAsync()
    {
        _context = new MeepleAiDbContext(_options);
        await _context.Database.EnsureCreatedAsync();
        _sut = new ShareRequestRepository(_context);
    }

    [Fact]
    public async Task GetExpiredLocksAsync_ReturnsOnlyExpiredLocks()
    {
        // Arrange: Seed data with expired and active locks
        // Act: Query expired
        // Assert: Only expired returned
    }
}
```

### E2E Tests (Playwright)

**Implemented**: `apps/web/e2e/share-game-flow.spec.ts` (Issue #2954)

**Coverage**:
1. Complete share game wizard flow
2. Admin approval with lock acquisition
3. Rate limit warning display

**Pattern**:
```typescript
test('should complete share game wizard successfully', async ({ page, authenticateViaAPI }) => {
  // Arrange: Login + navigate
  await authenticateViaAPI('user@test.com', 'password');
  await page.goto('/library');

  // Act: Complete wizard
  await page.getByTestId('share-game-button').click();
  await page.getByTestId('game-title-input').fill('Catan');
  // ... complete wizard steps

  // Assert: Success message
  await expect(page.getByText('Richiesta inviata con successo')).toBeVisible();
});
```

---

## Background Jobs

### LockCleanupJob (Hangfire)

**Schedule**: Ogni ora (cron: `0 * * * *`)

**Implementation**:
```csharp
public class ReleaseExpiredReviewLocksJob
{
    private readonly IMediator _mediator;
    private readonly ILogger<ReleaseExpiredReviewLocksJob> _logger;

    [AutomaticRetry(Attempts = 3)]
    public async Task Execute()
    {
        _logger.LogInformation("Starting expired review lock cleanup");

        var expiredLocks = await GetExpiredLocksAsync();

        foreach (var (requestId, reviewerId) in expiredLocks)
        {
            try
            {
                await _mediator.Send(new ReleaseReviewCommand(
                    requestId,
                    reviewerId,
                    isAutomaticRelease: true
                ));

                _logger.LogInformation(
                    "Released expired lock for request {RequestId}, reviewer {ReviewerId}",
                    requestId, reviewerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to release lock for request {RequestId}",
                    requestId);
                // Continue with other locks
            }
        }
    }
}
```

**Registration** (in `Program.cs`):
```csharp
RecurringJob.AddOrUpdate<ReleaseExpiredReviewLocksJob>(
    "release-expired-review-locks",
    job => job.Execute(),
    Cron.Hourly,
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
```

---

## Frontend Integration

### Share Game Wizard

**Component**: `apps/web/src/app/(authenticated)/library/components/ShareGameWizard.tsx`

**Flow**:
```
Step 1: Game Selection (from library)
   ↓
Step 2: Game Info (title, description, auto-filled from BGG if available)
   ↓
Step 3: Details (players, playtime, complexity)
   ↓
Step 4: Documents (optional PDF attachments)
   ↓
Step 5: Review & Submit
   ↓
API Call: POST /api/v1/share-requests
   ↓
Success: Redirect to /my-requests
```

**State Management** (Zustand):
```typescript
interface ShareRequestStore {
  currentStep: number;
  formData: ShareRequestFormData;
  isSubmitting: boolean;

  setStep: (step: number) => void;
  updateFormData: (data: Partial<ShareRequestFormData>) => void;
  submitRequest: () => Promise<void>;
  reset: () => void;
}
```

### Admin Dashboard

**Component**: `apps/web/src/app/(authenticated)/admin/share-requests/page.tsx`

**Features**:
- Server-side pagination (React Query + `useShareRequestsQuery`)
- Filters (status, type, search)
- Real-time updates via SSE (for lock status)
- Optimistic UI for lock acquisition

**Lock Management**:
```typescript
const { mutate: startReview } = useStartReviewMutation({
  onSuccess: (data) => {
    // Navigate to review page with lock confirmation
    router.push(`/admin/share-requests/${requestId}/review`);
  },
  onError: (error) => {
    if (error.status === 409) {
      toast.error('Request already in review by another admin');
    }
  }
});
```

---

## Performance Considerations

### Database Indexes

**Critical Indexes**:
```sql
-- Admin dashboard queries (status + date)
CREATE INDEX idx_share_requests_status_created
ON share_requests(status, created_at DESC);

-- User's requests lookup
CREATE INDEX idx_share_requests_user_id
ON share_requests(user_id, created_at DESC);

-- Lock cleanup job (find expired)
CREATE INDEX idx_share_requests_lock_expires
ON share_requests(review_lock_expires_at)
WHERE status = 1; -- InReview only

-- Contributor stats aggregation
CREATE INDEX idx_share_requests_user_status
ON share_requests(user_id, status);
```

### Query Optimization

**GetPendingShareRequests** (admin dashboard):
```csharp
public async Task<PagedResult<AdminShareRequestDto>> Handle(...)
{
    var query = _context.ShareRequests
        .AsNoTracking()  // Read-only optimization
        .Where(x => x.Status == ShareRequestStatus.Pending)
        .OrderBy(x => x.CreatedAt)  // Uses index
        .Select(x => new AdminShareRequestDto  // Project to DTO early
        {
            Id = x.Id,
            GameTitle = x.SourceGame.Title,
            // ... only needed fields
        });

    var total = await query.CountAsync(ct);
    var items = await query
        .Skip((request.Page - 1) * request.PageSize)
        .Take(request.PageSize)
        .ToListAsync(ct);

    return new PagedResult<AdminShareRequestDto>(items, total, request.Page, request.PageSize);
}
```

**Avoid**:
- ❌ Loading full entities when only few fields needed
- ❌ N+1 queries (use `.Include()` appropriately)
- ❌ Filtering in memory after ToList()

### Caching Strategy

**Contributor Leaderboard** (expensive query):

```csharp
[OutputCache(Duration = 3600)] // 1 hour
public async Task<List<LeaderboardEntryDto>> GetLeaderboard(...)
{
    return await _context.Contributors
        .OrderByDescending(c => c.ApprovedCount)
        .Take(limit)
        .Select(c => new LeaderboardEntryDto { ... })
        .ToListAsync();
}
```

**Invalidation**: Manual invalidation quando contributore stats cambiano significativamente.

---

## Security Considerations

### Authorization

**Policies** (in `Program.cs`):
```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnlyPolicy", policy =>
        policy.RequireRole("admin"));

    options.AddPolicy("UserOrHigherPolicy", policy =>
        policy.RequireRole("user", "editor", "admin"));
});
```

**Endpoint Usage**:
```csharp
.RequireAuthorization("AdminOnlyPolicy")  // Admin endpoints
.RequireAuthorization()                   // Any authenticated user
.AllowAnonymous()                         // Public
```

### Data Access Control

**User Requests**:
```csharp
// User can only access their own requests
var request = await _repository.GetByIdAsync(requestId);
if (request.UserId != currentUserId)
{
    throw new ForbiddenException("Cannot access other users' requests");
}
```

**Admin Requests**:
```csharp
// Admin can access all, but only modify if holding lock
var request = await _repository.GetByIdAsync(requestId);
if (request.ReviewedBy != null && request.ReviewedBy != adminId)
{
    throw new ConflictException("Request is locked by another admin");
}
```

---

## Migration Strategy

### Database Migration

**Created**: `20260114_AddShareRequestTables.cs`

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.EnsureSchema("shared_game_catalog");

    migrationBuilder.CreateTable(
        name: "share_requests",
        schema: "shared_game_catalog",
        columns: table => new
        {
            id = table.Column<Guid>(nullable: false),
            user_id = table.Column<Guid>(nullable: false),
            source_game_id = table.Column<Guid>(nullable: false),
            status = table.Column<int>(nullable: false),
            contribution_type = table.Column<int>(nullable: false),
            reviewed_by = table.Column<Guid>(nullable: true),
            review_lock_expires_at = table.Column<DateTime>(nullable: true),
            // ... other columns
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_share_requests", x => x.id);
            table.ForeignKey(
                name: "FK_share_requests_users",
                column: x => x.user_id,
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        });
}
```

**Rollback**: Down migration drops tables and schema.

---

## Troubleshooting

### Common Issues

**Issue #1: ObjectDisposedException in tests**

**Symptom**: `Cannot access a disposed object. Object name: 'MeepleAiDbContext'`

**Cause**: Tests store DbContext as field invece di usare scoped instances

**Fix** (Issue #2707):
```csharp
// ❌ Wrong
private readonly MeepleAiDbContext _context;

// ✅ Correct
private ShareRequestEntity GetShareRequest(Guid id)
{
    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    return dbContext.ShareRequests.Find(id);
}
```

**Issue #2: 57P01 Parallel Execution Error (Testcontainers)**

**Symptom**: `ERROR: 57P01: terminating connection due to administrator command`

**Cause**: Parallel test execution con Testcontainers shared container

**Fix** (Issue #2920):
```csharp
// SharedTestcontainersFixture with retry logic
await Policy
    .Handle<NpgsqlException>()
    .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(attempt))
    .ExecuteAsync(async () => await connection.OpenAsync());
```

**Issue #3: FK Constraint Violation**

**Symptom**: `violates foreign key constraint "FK_share_requests_users"`

**Cause**: ShareRequest created before User entity (Issue #2620)

**Fix**: Seed entities in dependency order:
```csharp
// ✅ Correct order
dbContext.Users.Add(user);  // Parent first
dbContext.ShareRequests.Add(request);  // Child second
await dbContext.SaveChangesAsync();
```

---

## Performance Benchmarks

### Expected Latency (p95)

| Operation | Target | Measured |
|-----------|--------|----------|
| Create request | < 200ms | ~150ms |
| List pending (page 1) | < 100ms | ~80ms |
| Get details | < 150ms | ~120ms |
| Approve (with badge calc) | < 500ms | ~400ms |
| Lock acquisition | < 100ms | ~70ms |

### Load Characteristics

**Expected Volume**:
- 100-500 requests/month (early phase)
- 1000-2000 requests/month (growth phase)
- 5-10 admin users
- 10-20 concurrent reviews

**Bottlenecks**:
- Badge calculation (N+1 queries) - mitigated by eventual consistency
- Leaderboard generation - mitigated by caching
- Document uploads - offloaded to blob storage

---

## Future Enhancements

### Phase 8 Considerations

**Auto-Approval for Trusted Contributors**:
```csharp
public class TrustedContributorPolicy
{
    public bool IsEligible(Contributor contributor)
    {
        return contributor.ApprovedCount >= 20
            && contributor.ApprovalRate >= 0.95
            && contributor.HasBadge(BadgeType.QualityPremium);
    }
}
```

**ML-Assisted Quality Scoring**:
- Embedding-based duplicate detection
- Auto-scoring di descrizioni via LLM
- Pre-screening suggerimenti miglioramento

**Collaborative Review**:
- Multiple admin possono co-own lock
- Internal chat per discussione casi complessi
- Voting system per decisioni controverse

---

## Related Documentation

- **API Reference**: `/docs/api/share-requests-api.yaml` (OpenAPI spec)
- **User Guide**: `/docs/user-guides/share-game-guide.md`
- **Admin Guide**: `/docs/admin-guides/share-requests-admin-guide.md`
- **ADR**: `/docs/01-architecture/adr/ADR-015-share-request-workflow.md`
- **Epic**: Issue #2718 (GitHub)

---

**Last Updated**: 2026-01-23
**Version**: 1.0
**Maintainer**: Team MeepleAI Backend

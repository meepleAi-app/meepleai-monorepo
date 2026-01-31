# Issue #TBD-001: Dashboard Aggregated API Endpoint

**Epic**: EPIC-DH-001 (Dashboard Hub Core)
**Type**: Backend - Feature
**Priority**: P0 - Critical
**Story Points**: 3
**Assignee**: Backend Team
**Sprint**: N+1

---

## 📋 Description

Creare endpoint REST aggregato che combina dati da multiple Bounded Contexts per popolare la dashboard hub con singola chiamata API ottimizzata.

**Context**:
Attualmente la dashboard usa multiple chiamate API separate (`/users/me`, `/games`, `/chats`) che causano waterfalls e performance degradation. Serve endpoint specializzato che aggrega dati server-side con caching Redis.

---

## 🎯 Acceptance Criteria

### Functional Requirements
- [ ] Endpoint `GET /api/v1/dashboard` implementato
- [ ] Richiede autenticazione (JWT required)
- [ ] Restituisce `DashboardData` con 6 sezioni:
  - `user`: UserProfile (id, username, email, lastLogin)
  - `stats`: Aggregate stats (libraryCount, playedLast30Days, chatCount, wishlistCount, currentStreak)
  - `activeSessions`: Array GameSession (max 5, ordinati per lastUpdate DESC)
  - `librarySnapshot`: { quota, topGames } (top 3 per playCount)
  - `recentActivity`: Array Activity (ultimi 10 eventi cronologici)
  - `chatHistory`: Array ChatThread (ultimi 5 thread)

### Performance Requirements
- [ ] Response time < 500ms (p99) in produzione
- [ ] Redis caching con TTL 5 minuti (user-specific cache key)
- [ ] Database queries ottimizzate (max 6 queries, use eager loading)
- [ ] Graceful degradation: Se sezione fallisce, restituisce empty array (non fail entire request)

### Documentation Requirements
- [ ] OpenAPI spec aggiornato con schema completo
- [ ] Swagger UI example response
- [ ] README con cache invalidation strategy
- [ ] Performance benchmarks documentati

---

## 🔧 Technical Implementation

### API Contract

**Request**:
```http
GET /api/v1/dashboard HTTP/1.1
Authorization: Bearer {jwt_token}
```

**Response** (200 OK):
```json
{
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "username": "marco_rossi",
    "email": "marco@example.com",
    "lastLogin": "2026-01-21T14:30:00Z"
  },
  "stats": {
    "libraryCount": 127,
    "playedLast30Days": 23,
    "chatCount": 12,
    "wishlistCount": 15,
    "currentStreak": 7
  },
  "activeSessions": [
    {
      "id": "uuid",
      "gameName": "Catan",
      "playerCount": 3,
      "maxPlayers": 4,
      "currentTurn": 12,
      "durationMinutes": 45,
      "lastUpdate": "2026-01-20T18:00:00Z"
    }
  ],
  "librarySnapshot": {
    "quota": { "used": 127, "total": 200 },
    "topGames": [
      {
        "id": "uuid",
        "title": "Catan",
        "coverUrl": "https://...",
        "rating": 5,
        "playCount": 45
      }
    ]
  },
  "recentActivity": [
    {
      "id": "uuid",
      "type": "game_added",
      "timestamp": "2026-01-21T15:00:00Z",
      "entityId": "uuid",
      "entityName": "Wingspan",
      "metadata": {}
    }
  ],
  "chatHistory": [
    {
      "id": "uuid",
      "title": "Regole Wingspan - Setup iniziale",
      "lastMessageAt": "2026-01-21T14:30:00Z",
      "messageCount": 8
    }
  ]
}
```

**Error Responses**:
- `401 Unauthorized`: Missing/invalid JWT
- `500 Internal Server Error`: Server failure (with partial data if possible)

---

### CQRS Implementation

**Query**:
```csharp
// BoundedContexts/Administration/Application/Queries/GetDashboardData/GetDashboardDataQuery.cs
public record GetDashboardDataQuery : IRequest<DashboardDataDto>;

public class GetDashboardDataQueryHandler : IRequestHandler<GetDashboardDataQuery, DashboardDataDto>
{
    private readonly IUserRepository _userRepo;
    private readonly IGameLibraryRepository _libraryRepo;
    private readonly IGameSessionRepository _sessionRepo;
    private readonly IChatRepository _chatRepo;
    private readonly IActivityService _activityService;
    private readonly IDistributedCache _cache;

    public async Task<DashboardDataDto> Handle(GetDashboardDataQuery request, CancellationToken ct)
    {
        var userId = _currentUserService.GetUserId();
        var cacheKey = $"dashboard:{userId}";

        // Try cache first
        var cached = await _cache.GetAsync<DashboardDataDto>(cacheKey, ct);
        if (cached != null) return cached;

        // Parallel queries for performance
        var userTask = _userRepo.GetByIdAsync(userId, ct);
        var statsTask = GetUserStatsAsync(userId, ct);
        var sessionsTask = _sessionRepo.GetActiveSessionsAsync(userId, limit: 5, ct);
        var libraryTask = GetLibrarySnapshotAsync(userId, ct);
        var activityTask = _activityService.GetRecentActivityAsync(userId, limit: 10, ct);
        var chatTask = _chatRepo.GetRecentThreadsAsync(userId, limit: 5, ct);

        await Task.WhenAll(userTask, statsTask, sessionsTask, libraryTask, activityTask, chatTask);

        var result = new DashboardDataDto
        {
            User = userTask.Result.ToDto(),
            Stats = statsTask.Result,
            ActiveSessions = sessionsTask.Result.Select(s => s.ToDto()).ToList(),
            LibrarySnapshot = libraryTask.Result,
            RecentActivity = activityTask.Result.Select(a => a.ToDto()).ToList(),
            ChatHistory = chatTask.Result.Select(c => c.ToDto()).ToList()
        };

        // Cache for 5 minutes
        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), ct);

        return result;
    }

    private async Task<UserStatsDto> GetUserStatsAsync(Guid userId, CancellationToken ct)
    {
        // Aggregate stats from multiple sources
        var libraryCount = await _libraryRepo.CountByUserAsync(userId, ct);
        var playedLast30 = await _sessionRepo.CountCompletedAsync(userId, since: DateTime.UtcNow.AddDays(-30), ct);
        var chatCount = await _chatRepo.CountByUserAsync(userId, since: DateTime.UtcNow.AddDays(-7), ct);
        var wishlistCount = 0; // TODO: Implement in Epic 2
        var currentStreak = await CalculateStreakAsync(userId, ct);

        return new UserStatsDto(libraryCount, playedLast30, chatCount, wishlistCount, currentStreak);
    }

    private async Task<LibrarySnapshotDto> GetLibrarySnapshotAsync(Guid userId, CancellationToken ct)
    {
        var quota = await _libraryRepo.GetQuotaAsync(userId, ct);
        var topGames = await _libraryRepo.GetTopGamesByPlayCountAsync(userId, limit: 3, ct);

        return new LibrarySnapshotDto
        {
            Quota = new QuotaDto { Used = quota.Used, Total = quota.Total },
            TopGames = topGames.Select(g => g.ToDto()).ToList()
        };
    }
}
```

**Endpoint Registration**:
```csharp
// Routing/DashboardRoutes.cs
public static class DashboardRoutes
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization();

        group.MapGet("/", async (IMediator mediator) =>
        {
            var query = new GetDashboardDataQuery();
            var result = await mediator.Send(query);
            return Results.Ok(result);
        })
        .WithName("GetDashboardData")
        .WithOpenApi(op => {
            op.Summary = "Get aggregated dashboard data";
            op.Description = "Returns user stats, active sessions, library snapshot, recent activity, and chat history";
            return op;
        });
    }
}
```

---

## 🧪 Testing Requirements

### Unit Tests
```csharp
// GetDashboardDataQueryHandlerTests.cs
public class GetDashboardDataQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsCompleteDashboardData()
    {
        // Arrange
        var handler = CreateHandler();
        var query = new GetDashboardDataQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.User.Should().NotBeNull();
        result.Stats.LibraryCount.Should().BeGreaterThan(0);
        result.ActiveSessions.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_UsesCacheWhenAvailable()
    {
        // Test cache hit scenario
    }

    [Fact]
    public async Task Handle_HandlesPartialFailuresGracefully()
    {
        // Test quando una sezione fallisce, altre funzionano
    }
}
```

### Integration Tests
```csharp
[Fact]
public async Task GetDashboard_ReturnsExpectedData()
{
    // Arrange: Seed database con dati test
    var client = _factory.CreateClient();
    var token = await GetAuthTokenAsync();
    client.DefaultRequestHeaders.Authorization = new("Bearer", token);

    // Act
    var response = await client.GetAsync("/api/v1/dashboard");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var data = await response.Content.ReadFromJsonAsync<DashboardDataDto>();
    data.Should().NotBeNull();
    data.Stats.LibraryCount.Should().Be(10); // Seeded data
}
```

### Performance Tests
```csharp
[Fact]
public async Task GetDashboard_PerformanceUnder500ms()
{
    var stopwatch = Stopwatch.StartNew();
    await _mediator.Send(new GetDashboardDataQuery());
    stopwatch.Stop();

    stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);
}
```

---

## 📦 Deliverables

- [ ] `GetDashboardDataQuery.cs` + Handler + Validator
- [ ] `DashboardDataDto.cs` + related DTOs (UserStatsDto, LibrarySnapshotDto, etc.)
- [ ] `DashboardRoutes.cs` con endpoint mapping
- [ ] Redis caching integration
- [ ] OpenAPI spec update
- [ ] Unit tests (> 90% coverage)
- [ ] Integration tests (API flow)
- [ ] Performance benchmarks

---

## 🔗 Dependencies

**Requires**:
- ✅ Redis configured (already in `infra/docker-compose.yml`)
- ✅ UserLibrary BC operational
- ✅ GameManagement BC operational
- ✅ KnowledgeBase BC operational

**Blocks**:
- Issue #3 (Frontend layout refactoring - needs API contract)
- Issue #4 (Activity feed - needs activity data)
- Issue #5 (Library snapshot - needs library data)

---

## 🚨 Edge Cases & Error Handling

- **User with no games**: Return empty library snapshot with quota 0/200
- **User with no sessions**: Return empty activeSessions array
- **User with no chats**: Return empty chatHistory array
- **Redis cache miss**: Query DB and rebuild cache (expected on first call)
- **Partial BC failure**: Return partial data with null for failed section (don't fail entire request)
- **Database timeout**: Return cached data (stale ok) + log warning

---

## 📝 Open Questions

- [ ] Should we include user preferences (theme, language) in UserProfile?
- [ ] Cache invalidation strategy: On user action (game added) or TTL-only?
- [ ] Should activeSessions include paused sessions or only in-progress?
- [ ] Pagination for chatHistory if > 5 threads?

---

## ✅ Definition of Done

- [ ] Code merged to `main-dev` after PR approval
- [ ] Unit tests passing (> 90% coverage)
- [ ] Integration tests passing
- [ ] Performance benchmarks met (< 500ms p99)
- [ ] OpenAPI spec updated
- [ ] Peer review completed (2 approvals)
- [ ] Deployed to staging environment
- [ ] Backend team demo completed

---

**Created**: 2026-01-21
**Last Updated**: 2026-01-21
**Related Issues**: #2, #3, #4, #5

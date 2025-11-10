# Test Optimization Quick Reference

**For**: Developers implementing test optimization strategy
**Status**: Active Implementation Guide
**Related**: test-optimization-strategy.md, test-optimization-implementation-checklist.md

---

## 🎯 Quick Decision Trees

### Should I Use a Shared User?

```
Does the test modify user properties?
├─ Yes → Create unique user
│  └─ Examples: UpdateUserRole, ChangePassword, DeleteUser
├─ No → Use shared user
   └─ Examples: ListUsers, GetUserById, Authorization checks
```

**Implementation**:
```csharp
// ✅ Good: Read-only test
[Fact]
public async Task GetUser_ReturnsUserDetails()
{
    var user = GetSharedUser(UserRole.Admin);
    var response = await _client.GetAsync($"/api/v1/users/{user.Id}");
    // ...assertions...
}

// ✅ Good: State-modifying test
[Fact]
public async Task UpdateUserRole_ChangesRole()
{
    var user = await CreateTestUserAsync("unique-user", UserRole.User);
    var response = await _client.PutAsync($"/api/v1/users/{user.Id}/role", ...);
    // ...assertions...
}
```

---

### Should I Use a Shared Game?

```
Does the test modify game properties or create associated data?
├─ Yes → Create unique game
│  └─ Examples: UpdateGame, DeleteGame, CreateRuleSpec
├─ No → Use shared game
   └─ Examples: ListGames, GetGameById, SearchGames
```

**Implementation**:
```csharp
// ✅ Good: Read-only test
[Fact]
public async Task GetGame_ReturnsGameDetails()
{
    var game = _postgresFixture.ChessGame;
    var response = await _client.GetAsync($"/api/v1/games/{game.Id}");
    // ...assertions...
}

// ✅ Good: State-modifying test
[Fact]
public async Task CreateRuleSpec_AddsRuleSpec()
{
    var game = await CreateTestGameAsync("my-game");
    var response = await _client.PostAsync($"/api/v1/games/{game.Id}/rulespecs", ...);
    // ...assertions...
}
```

---

### Should I Use GetTestCacheKey()?

```
Does the test interact with Redis cache?
├─ Yes → Use GetTestCacheKey()
│  └─ Examples: Cache admin endpoints, cache invalidation
├─ No → Normal test logic
   └─ Examples: Database-only tests, auth tests
```

**Implementation**:
```csharp
// ✅ Good: Cache isolation
[Fact]
public async Task CacheStats_ReturnsMetrics()
{
    var cacheKey = GetTestCacheKey($"game:{gameId}");
    await CacheService.SetAsync(cacheKey, data);

    var response = await _client.GetAsync("/api/v1/admin/cache/stats");
    // ...assertions...
}
```

---

### Which Collection Should My Test Be In?

```
What domain does the test cover?
├─ User Management → [Collection("Admin User Management")]
├─ Session Management → [Collection("Admin Session Management")]
├─ API Key Management → [Collection("Admin API Key Management")]
├─ Cache Management → [Collection("Admin Cache Management")]
├─ Prompt Management → [Collection("Admin Prompt Management")]
└─ Other → Keep in original collection or create new one
```

**Example**:
```csharp
// BEFORE: All in AdminTestCollection
[Collection("Admin Endpoints")]
public class UserManagementEndpointsTests : IntegrationTestBase

// AFTER: Domain-specific collection
[Collection("Admin User Management")]
public class UserManagementEndpointsTests : IntegrationTestBase
```

---

## 🔧 Common Patterns

### Pattern 1: Read-Only Test with Shared Resources

```csharp
[Collection("Admin User Management")]
public class UserListEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task ListUsers_ReturnsAllUsers()
    {
        // Arrange: Use shared user (no CreateTestUserAsync needed)
        var adminUser = GetSharedUser(UserRole.Admin);

        // Act
        var response = await _client.GetAsync("/api/v1/admin/users");

        // Assert
        response.EnsureSuccessStatusCode();
        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.NotEmpty(users);
    }
}
```

**Benefits**:
- No PBKDF2 hashing overhead (~150ms saved)
- No user cleanup needed
- Faster test execution

---

### Pattern 2: State-Modifying Test with Unique Resources

```csharp
[Collection("Admin User Management")]
public class UserUpdateEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task UpdateUserRole_ChangesRole()
    {
        // Arrange: Create unique user (will be cleaned up)
        var user = await CreateTestUserAsync("test-user", UserRole.User);
        var adminUser = GetSharedUser(UserRole.Admin);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/admin/users/{user.Id}/role",
            new { role = "Editor" });

        // Assert
        response.EnsureSuccessStatusCode();

        // Verify: Check user role changed in database
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var updatedUser = await db.Users.FindAsync(user.Id);
        Assert.Equal(UserRole.Editor, updatedUser!.Role);
    }
}
```

**When to use**:
- Test modifies user properties
- Test deletes users
- Test requires specific user state

---

### Pattern 3: Cache-Aware Test

```csharp
[Collection("Admin Cache Management")]
public class CacheAdminEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task CacheStats_ReturnsCorrectMetrics()
    {
        // Arrange: Use test-isolated cache keys
        var adminUser = GetSharedUser(UserRole.Admin);
        var cacheKey = GetTestCacheKey($"game:{_postgresFixture.ChessGame.Id}");

        // Prime cache
        await CacheService.SetAsync(cacheKey, new { data = "test" });

        // Act
        var response = await _client.GetAsync("/api/v1/admin/cache/stats");

        // Assert
        response.EnsureSuccessStatusCode();
        var stats = await response.Content.ReadFromJsonAsync<CacheStatsDto>();
        Assert.NotNull(stats);
    }
}
```

**Key points**:
- Always use `GetTestCacheKey()` for cache operations
- Ensures parallel tests don't interfere
- Cache is automatically cleaned up (test-scoped namespace)

---

### Pattern 4: Database Transaction Test

```csharp
[Collection("Admin User Management")]
public class UserDeleteEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task DeleteUser_RemovesAllAssociatedData()
    {
        // Arrange
        var user = await CreateTestUserAsync("delete-me", UserRole.User);
        var adminUser = GetSharedUser(UserRole.Admin);

        // Create associated data
        await CreateTestSessionAsync(user.Id);
        await CreateTestApiKeyAsync(user.Id);

        // Act
        var response = await _client.DeleteAsync($"/api/v1/admin/users/{user.Id}");

        // Assert
        response.EnsureSuccessStatusCode();

        // Verify cascading delete
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var deletedUser = await db.Users.FindAsync(user.Id);
        Assert.Null(deletedUser);

        var orphanedSessions = await db.UserSessions
            .Where(s => s.UserId == user.Id)
            .ToListAsync();
        Assert.Empty(orphanedSessions);

        var orphanedApiKeys = await db.ApiKeys
            .Where(k => k.UserId == user.Id)
            .ToListAsync();
        Assert.Empty(orphanedApiKeys);
    }
}
```

---

### Pattern 5: DbContext Pooling (Advanced)

```csharp
[Collection("Admin User Management")]
public class PerformanceCriticalTests : IntegrationTestBase
{
    [Fact]
    public async Task BulkOperation_HandlesLargeDataset()
    {
        // Use pooled DbContext for performance
        var result = await WithDbContextAsync(async db =>
        {
            var users = await db.Users
                .AsNoTracking()
                .Where(u => u.Role == UserRole.User)
                .ToListAsync();

            return users.Count;
        });

        Assert.True(result > 0);
    }
}
```

**Benefits**:
- Reuses DbContext instances
- Automatic cleanup via pool
- Faster than creating new DbContext per operation

---

## ⚠️ Common Pitfalls

### Pitfall 1: Modifying Shared Users

```csharp
// ❌ BAD: Modifies shared user
[Fact]
public async Task UpdateUser_ChangesDisplayName()
{
    var user = GetSharedUser(UserRole.Admin);  // Shared user!

    // This will affect other tests using the same shared user
    var response = await _client.PutAsJsonAsync(
        $"/api/v1/users/{user.Id}",
        new { displayName = "Modified Name" });
}

// ✅ GOOD: Create unique user
[Fact]
public async Task UpdateUser_ChangesDisplayName()
{
    var user = await CreateTestUserAsync("unique-user", UserRole.Admin);

    var response = await _client.PutAsJsonAsync(
        $"/api/v1/users/{user.Id}",
        new { displayName = "Modified Name" });
}
```

**How to detect**: Use `EnsureNotSharedUser()` helper
```csharp
protected void EnsureNotSharedUser(UserEntity user)
{
    if (user.Email.EndsWith("@test.fixture"))
    {
        throw new InvalidOperationException(
            "Cannot modify shared fixture user. Create unique user for state modification tests.");
    }
}
```

---

### Pitfall 2: Forgetting Cache Isolation

```csharp
// ❌ BAD: No cache isolation
[Fact]
public async Task CacheTest_ChecksCacheHit()
{
    await CacheService.SetAsync("game:chess", data);  // Global key!
    // Other parallel tests might use "game:chess" too
}

// ✅ GOOD: Test-isolated cache key
[Fact]
public async Task CacheTest_ChecksCacheHit()
{
    var cacheKey = GetTestCacheKey("game:chess");  // test:{TestRunId}:game:chess
    await CacheService.SetAsync(cacheKey, data);
}
```

---

### Pitfall 3: Assuming Sequential Execution

```csharp
// ❌ BAD: Assumes test order
[Fact]
public async Task CreateUser_Step1()
{
    var user = await CreateTestUserAsync("shared-user", UserRole.User);
    // Store user.Id somewhere for Step2?
}

[Fact]
public async Task UpdateUser_Step2()
{
    // This might run in parallel or before Step1!
    var userId = /* how to get userId from Step1? */;
}

// ✅ GOOD: Self-contained test
[Fact]
public async Task CreateAndUpdateUser_EndToEnd()
{
    // Arrange
    var user = await CreateTestUserAsync("unique-user", UserRole.User);

    // Act 1: Create
    var createResponse = await _client.PostAsync(...);
    createResponse.EnsureSuccessStatusCode();

    // Act 2: Update
    var updateResponse = await _client.PutAsync(...);
    updateResponse.EnsureSuccessStatusCode();

    // Assert both operations
}
```

---

### Pitfall 4: Not Cleaning Up Unique Resources

```csharp
// ❌ BAD: Manual cleanup
[Fact]
public async Task CreateGame_ReturnsSuccess()
{
    var game = await CreateTestGameAsync("my-game");
    // ...test logic...

    // Manual cleanup (unnecessary!)
    using var scope = Factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    db.Games.Remove(game);
    await db.SaveChangesAsync();
}

// ✅ GOOD: Automatic cleanup
[Fact]
public async Task CreateGame_ReturnsSuccess()
{
    var game = await CreateTestGameAsync("my-game");
    // ...test logic...

    // No manual cleanup needed!
    // CreateTestGameAsync adds to _testGameIds
    // DisposeAsync will clean up automatically
}
```

**How it works**:
- `CreateTestUserAsync()` → adds to `_testUserIds`
- `CreateTestGameAsync()` → adds to `_testGameIds`
- `DisposeAsync()` → batch deletes all tracked resources

---

## 📊 Performance Guidelines

### Expected Test Timing

| Category | Duration | Guidance |
|----------|----------|----------|
| **⚡ Fast** | <1s | Most tests should be here (read-only, shared resources) |
| **✅ Normal** | 1-3s | Acceptable (unique resource creation, simple workflows) |
| **⚠️ Slow** | 3-5s | Review for optimization (complex workflows, multiple API calls) |
| **🐌 Very Slow** | >5s | Must optimize (consider splitting, mocking, or parallel operations) |

### When a Test is Too Slow

1. **Use Shared Resources**: Replace `CreateTestUserAsync` with `GetSharedUser` if read-only
2. **Reduce API Calls**: Combine multiple operations into one test
3. **Use DbContext Pooling**: For database-heavy tests
4. **Check for N+1 Queries**: Use EF Core logging to detect inefficiencies
5. **Consider Unit Tests**: If testing pure logic, unit test may be faster

### Measuring Test Performance

```csharp
// Option 1: Manual timing
[Fact]
public async Task MyTest()
{
    await using var _ = new TestTimingScope(nameof(MyTest));
    // ...test logic...
}

// Option 2: Run with detailed logging
// dotnet test --logger "console;verbosity=detailed"
```

---

## 🔍 Debugging Parallel Test Failures

### Step 1: Identify the Flaky Test

```bash
# Run test 10 times to reproduce
for i in {1..10}; do
    dotnet test --filter "FullyQualifiedName~MyFlakyTest" || echo "FAIL $i"
done
```

### Step 2: Check for Shared State

Look for:
- **Modifying shared users/games**: Use unique resources instead
- **Cache key conflicts**: Add `GetTestCacheKey()`
- **Global database state**: Ensure TestRunId isolation
- **Race conditions**: Add proper async/await

### Step 3: Add Logging

```csharp
[Fact]
public async Task MyFlakyTest()
{
    Console.WriteLine($"🧵 [Thread {Thread.CurrentThread.ManagedThreadId}] Starting {nameof(MyFlakyTest)}");
    Console.WriteLine($"📋 [TestRunId: {TestRunId}]");

    var user = await CreateTestUserAsync("test-user", UserRole.User);
    Console.WriteLine($"👤 [Created user: {user.Id}]");

    // ...test logic...
}
```

### Step 4: Isolate the Test

```csharp
// Temporarily disable parallelization for debugging
[Collection("Debug Collection")]  // New collection with DisableParallelization = true
public class MyFlakyTest : IntegrationTestBase
```

### Step 5: Check Database Locks

```sql
-- Check for deadlocks in Postgres logs
SELECT * FROM pg_stat_activity WHERE wait_event_type IS NOT NULL;
```

---

## 📚 Reference

### Available Shared Resources

| Resource | Property | Collection | Notes |
|----------|----------|------------|-------|
| Admin User | `GetSharedUser(UserRole.Admin)` | All collections | Read-only, email: admin@test.fixture |
| Editor User | `GetSharedUser(UserRole.Editor)` | All collections | Read-only, email: editor@test.fixture |
| Regular User | `GetSharedUser(UserRole.User)` | All collections | Read-only, email: user@test.fixture |
| Chess Game | `_postgresFixture.ChessGame` | All collections | Read-only, ID: chess-fixture |
| Tic-Tac-Toe Game | `_postgresFixture.TicTacToeGame` | All collections | Read-only, ID: tic-tac-toe-fixture |

### Helper Methods

| Method | Purpose | When to Use |
|--------|---------|-------------|
| `GetSharedUser(role)` | Get pre-created shared user | Read-only tests |
| `CreateTestUserAsync(...)` | Create unique test user | State-modifying tests |
| `CreateTestGameAsync(...)` | Create unique test game | Tests requiring unique games |
| `GetTestCacheKey(key)` | Get test-isolated cache key | All cache operations |
| `WithDbContextAsync(action)` | Use pooled DbContext | Performance-critical DB operations |
| `EnsureNotSharedUser(user)` | Validate not modifying shared user | Safety check before modifications |

### Collection Definitions

| Collection | Test Files | Parallelization |
|------------|-----------|----------------|
| `Admin User Management` | UserManagementEndpointsTests.cs | ✅ Enabled |
| `Admin Session Management` | SessionManagementEndpointsTests.cs, SessionStatusEndpointsTests.cs | ✅ Enabled |
| `Admin API Key Management` | ApiKeyManagementEndpointsTests.cs, ApiKeyAuthenticationIntegrationTests.cs | ✅ Enabled |
| `Admin Cache Management` | CacheAdminEndpointsTests.cs, CacheInvalidationIntegrationTests.cs | ✅ Enabled |
| `Admin Prompt Management` | PromptManagementEndpointsTests.cs | ✅ Enabled |

---

## 🚨 Emergency Rollback

If tests become unstable after optimization:

### Quick Rollback (Feature Flag)

```bash
# Set environment variable to disable parallelization
export DISABLE_TEST_PARALLELIZATION=true
dotnet test
```

### Partial Rollback (Specific Collection)

```csharp
// Re-enable serialization for problematic collection
[CollectionDefinition("Admin Cache Management", DisableParallelization = true)]
public class AdminCacheMgmtCollection : ICollectionFixture<PostgresCollectionFixture>
```

### Full Rollback

```bash
# Revert parallelization commit
git revert <commit-hash>

# Run tests to verify
dotnet test
```

---

## 📞 Getting Help

- **Flaky Tests**: Check #test-optimization Slack channel
- **Performance Issues**: See test-optimization-strategy.md Section 5
- **Questions**: Ask in team stand-up or create GitHub Discussion

---

**Last Updated**: 2025-11-10
**Version**: 1.0
**Maintained By**: Backend Team

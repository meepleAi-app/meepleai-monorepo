# Testing Specialized Guides

**Comprehensive guide for specialized testing scenarios**

---

## Table of Contents

1. [Manual Testing](#manual-testing)
2. [Accessibility Testing](#accessibility-testing)
3. [Background Service Testing](#background-service-testing)
4. [Concurrency Testing](#concurrency-testing)
5. [API Testing with Postman](#api-testing-with-postman)

---

## Manual Testing

For comprehensive manual testing procedures, see the Italian guide:
- **File**: `manual-testing-guide.md` (kept separate for language/workflow reasons)

**Quick Reference**:
- Full stack setup and verification
- Authentication flows (login, register, OAuth, 2FA)
- PDF upload and processing
- Chat and RAG testing
- Admin functions
- Performance and security checks

---

## Accessibility Testing

### Overview
Ensure MeepleAI is usable by everyone, including users with disabilities.

### Tools
- **axe DevTools**: Browser extension for automated accessibility auditing
- **Playwright accessibility**: Built-in `page.accessibility` API
- **Screen readers**: NVDA (Windows), VoiceOver (macOS)

### Key Areas

#### 1. Keyboard Navigation
```typescript
test('all interactive elements accessible via keyboard', async ({ page }) => {
  await page.goto('/chat');

  // Tab through all interactive elements
  await page.keyboard.press('Tab');
  const firstElement = await page.locator(':focus');
  expect(firstElement).toHaveAttribute('role', 'button');

  // Verify tab order is logical
  await page.keyboard.press('Tab');
  const secondElement = await page.locator(':focus');
  // Assert secondElement is next in logical order
});
```

#### 2. ARIA Labels and Roles
```typescript
test('buttons have accessible labels', async ({ page }) => {
  await page.goto('/games');

  // Verify all buttons have accessible names
  const buttons = await page.locator('button').all();
  for (const button of buttons) {
    const accessibleName = await button.getAttribute('aria-label')
      || await button.textContent();
    expect(accessibleName).toBeTruthy();
  }
});
```

#### 3. Color Contrast
- Minimum contrast ratio: 4.5:1 for normal text
- Minimum contrast ratio: 3:1 for large text (18pt+ or 14pt+ bold)
- Use axe DevTools to audit automatically

#### 4. Focus Indicators
```typescript
test('focus indicators visible', async ({ page }) => {
  await page.goto('/upload');

  const uploadButton = page.getByRole('button', { name: /upload/i });
  await uploadButton.focus();

  // Verify visible focus indicator
  const outline = await uploadButton.evaluate((el) => {
    return window.getComputedStyle(el).outline;
  });
  expect(outline).not.toBe('none');
});
```

### WCAG 2.1 Level AA Checklist

- [ ] Keyboard accessible: All functionality available via keyboard
- [ ] No keyboard traps: User can navigate away from any element
- [ ] Skip links: "Skip to main content" link present
- [ ] Page titles: Descriptive `<title>` tags
- [ ] Focus order: Logical tab order
- [ ] Link purpose: Link text describes destination
- [ ] Multiple ways: Multiple ways to navigate (menu, search, sitemap)
- [ ] Headings: Proper heading hierarchy (h1 → h2 → h3)
- [ ] Labels: All form inputs have labels
- [ ] Error identification: Form errors clearly identified
- [ ] Color not sole indicator: Info not conveyed by color alone

---

## Background Service Testing

### Overview
Test long-running background services (hosted services, workers, scheduled tasks).

### Challenges
- Asynchronous execution
- Time-dependent logic
- External dependencies (file system, network)
- Cancellation token handling

### Patterns

#### 1. Fake Time Provider
```csharp
public class BackgroundServiceTests
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly BackgroundService _service;

    public BackgroundServiceTests()
    {
        _timeProvider = new FakeTimeProvider();
        _service = new MyBackgroundService(_timeProvider);
    }

    [Fact]
    public async Task ExecutesTaskEvery5Minutes()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var executionCount = 0;

        // Act
        var serviceTask = _service.StartAsync(cts.Token);

        // Advance time by 5 minutes
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        await Task.Delay(100); // Allow execution

        // Assert
        Assert.Equal(1, executionCount);

        // Cleanup
        cts.Cancel();
        await serviceTask;
    }
}
```

#### 2. Cancellation Token Testing
```csharp
[Fact]
public async Task StopsGracefullyOnCancellation()
{
    // Arrange
    var cts = new CancellationTokenSource();
    var service = new MyBackgroundService();

    // Act
    var serviceTask = service.StartAsync(cts.Token);
    await Task.Delay(100); // Let it start

    cts.Cancel();

    // Assert: Should complete without throwing
    await serviceTask; // Should not throw OperationCanceledException
}
```

#### 3. Testing with Testcontainers
```csharp
public class PdfProcessingBackgroundServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;

    public PdfProcessingBackgroundServiceTests()
    {
        _postgres = new PostgreSqlBuilder().Build();
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    [Fact]
    public async Task ProcessesPendingPdfsFromDatabase()
    {
        // Arrange: Insert pending PDF job
        // Act: Start background service
        // Assert: PDF processed and marked complete
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
```

---

## Concurrency Testing

### Overview
Test race conditions, deadlocks, and thread-safety issues.

### Techniques

#### 1. Parallel Task Execution
```csharp
[Fact]
public async Task HandlesConcurrentRequests()
{
    // Arrange
    var service = new ApiKeyService();
    var tasks = new List<Task<string>>();

    // Act: 100 concurrent requests
    for (int i = 0; i < 100; i++)
    {
        tasks.Add(service.GenerateApiKeyAsync($"user-{i}"));
    }

    var results = await Task.WhenAll(tasks);

    // Assert: All keys unique (no race conditions)
    Assert.Equal(100, results.Distinct().Count());
}
```

#### 2. Stress Testing with SemaphoreSlim
```csharp
[Fact]
public async Task RateLimitHandlesConcurrency()
{
    // Arrange
    var rateLimiter = new RateLimitService(maxRequests: 10, perMinute: 1);
    var successCount = 0;
    var rejectedCount = 0;

    // Act: 100 concurrent requests
    var tasks = Enumerable.Range(0, 100).Select(async i =>
    {
        var allowed = await rateLimiter.TryAcquireAsync();
        if (allowed) Interlocked.Increment(ref successCount);
        else Interlocked.Increment(ref rejectedCount);
    });

    await Task.WhenAll(tasks);

    // Assert
    Assert.Equal(10, successCount); // Only 10 allowed
    Assert.Equal(90, rejectedCount);
}
```

#### 3. Database Concurrency Testing
```csharp
[Fact]
public async Task OptimisticConcurrencyHandlesConflicts()
{
    // Arrange: Two users editing same entity
    var user1Context = CreateDbContext();
    var user2Context = CreateDbContext();

    var entity1 = await user1Context.Games.FindAsync(gameId);
    var entity2 = await user2Context.Games.FindAsync(gameId);

    // Act: Both modify and save
    entity1.Name = "User 1 Update";
    await user1Context.SaveChangesAsync(); // Succeeds

    entity2.Name = "User 2 Update";

    // Assert: Second save throws concurrency exception
    await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
        () => user2Context.SaveChangesAsync()
    );
}
```

---

## API Testing with Postman

### Setup
1. Import collections from: `tests/postman/collections/` (DDD bounded contexts)
2. Import environment: `tests/postman/environments/local.postman_environment.json`
3. Set environment variables:
   - `API_BASE_URL`: `http://localhost:5080`
   - `ADMIN_EMAIL`: `admin@meepleai.dev`
   - `ADMIN_PASSWORD`: `Demo123!`

### Test Scenarios

#### 1. Authentication Flow
```
1. POST /api/v1/auth/register → Save userId
2. POST /api/v1/auth/login → Save sessionCookie
3. GET /api/v1/auth/me → Verify user data
4. POST /api/v1/auth/logout → Clear session
5. GET /api/v1/auth/me → Expect 401
```

#### 2. API Key Authentication
```
1. POST /api/v1/auth/login
2. POST /api/v1/api-keys → Save apiKey
3. GET /api/v1/games (with X-API-Key header) → Success
4. DELETE /api/v1/api-keys/:id
5. GET /api/v1/games (with deleted key) → Expect 401
```

#### 3. PDF Upload and Processing
```
1. POST /api/v1/games → Create game → Save gameId
2. POST /api/v1/pdf/upload (multipart/form-data) → Save documentId
3. GET /api/v1/pdf/:documentId/status → Poll until "Completed"
4. GET /api/v1/pdf/:documentId/quality → Verify quality score ≥0.70
5. POST /api/v1/chat (with gameId) → Test RAG retrieval
```

#### 4. Admin Operations
```
1. POST /api/v1/auth/login (admin credentials)
2. GET /api/v1/admin/users → List all users
3. PUT /api/v1/admin/users/:id/role → Change user role
4. GET /api/v1/admin/stats → Verify metrics
5. GET /api/v1/admin/alerts → Check active alerts
```

### Pre-request Scripts

**Auto-login**:
```javascript
// Set in Collection pre-request script
if (!pm.collectionVariables.get('sessionCookie')) {
    pm.sendRequest({
        url: pm.environment.get('API_BASE_URL') + '/api/v1/auth/login',
        method: 'POST',
        header: { 'Content-Type': 'application/json' },
        body: {
            mode: 'raw',
            raw: JSON.stringify({
                email: pm.environment.get('ADMIN_EMAIL'),
                password: pm.environment.get('ADMIN_PASSWORD')
            })
        }
    }, (err, res) => {
        const cookie = res.headers.get('Set-Cookie');
        pm.collectionVariables.set('sessionCookie', cookie);
    });
}
```

### Test Scripts

**Verify Response Schema**:
```javascript
pm.test("Response has expected schema", function () {
    const schema = {
        type: "object",
        properties: {
            id: { type: "string" },
            name: { type: "string" },
            createdAt: { type: "string" }
        },
        required: ["id", "name"]
    };
    pm.response.to.have.jsonSchema(schema);
});
```

**Save Response Variable**:
```javascript
pm.test("Status is 201 Created", function () {
    pm.response.to.have.status(201);
    const jsonData = pm.response.json();
    pm.collectionVariables.set('gameId', jsonData.id);
});
```

---

## See Also

- **Testing Guide**: `testing-guide.md` - Comprehensive test writing tutorial
- **Testing Strategy**: `testing-strategy.md` - Overall testing approach
- **E2E Advanced**: `testing-e2e-advanced.md` - Page Object Model and advanced E2E patterns
- **Manual Testing (Italian)**: `manual-testing-guide.md` - Full manual QA procedures

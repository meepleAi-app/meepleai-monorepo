## 🎯 Objective

Expand concurrency test coverage to additional critical services using the proven **WebApplicationFactory + Testcontainers + PostgreSQL** pattern.

## 📚 Background

Issue #601 (Phase 1) successfully delivered:
- ✅ Comprehensive concurrency testing guide (500+ lines)
- ✅ ConfigurationConcurrencyTests (6 passing tests)
- ✅ 4 test patterns documented

**Phase 2 Critical Finding**: SQLite in-memory databases CANNOT support true concurrent operations due to:
- Nested transaction limitations
- Single-writer serialization
- Shared connection concurrency errors

**Solution Proven**: ConfigurationConcurrencyTests demonstrates the CORRECT pattern:
- WebApplicationFactory + proper DI scoping
- Testcontainers with PostgreSQL
- HTTP clients = multiple concurrent service instances
- Real database with concurrent transaction support

## 🎯 Scope

Implement concurrency tests for 4-5 critical services using the **Testcontainers pattern**.

### High Priority Services (Must Have)

#### 1. RuleSpecService (Critical - Version Conflicts)
**Concurrency Risks**:
- `GenerateNextVersionAsync`: Concurrent version generation may create duplicates
- `UpdateRuleSpecAsync`: Race condition in version existence checks
- TOCTOU vulnerability in check-then-create logic

**Test Scenarios** (Pattern: ConfigurationConcurrencyTests):
- [ ] Concurrent version generation (5 simultaneous auto-gen calls)
- [ ] Version conflict detection (2 concurrent calls with same version "1.0")
- [ ] TOCTOU prevention (10 concurrent calls after initial version)
- [ ] Cache invalidation propagation

**Estimated Effort**: 6-8 hours

#### 2. SessionManagementService (Critical - Auth Security)
**Concurrency Risks**:
- `RevokeSessionAsync`: Idempotent revocation of same session
- `RevokeAllUserSessionsAsync`: Count consistency with concurrent bulk operations
- `RevokeInactiveSessionsAsync`: Duplicate cleanup in concurrent cleanup runs

**Test Scenarios**:
- [ ] Concurrent same-session revocation (5 simultaneous revoke calls)
- [ ] Concurrent bulk revocations (2 RevokeAllUserSessionsAsync on same user)
- [ ] Mixed single + bulk revocations (TOCTOU prevention)
- [ ] Multi-user concurrent operations (data consistency)
- [ ] Concurrent inactive cleanup (no duplicate revocations)

**Estimated Effort**: 6-8 hours

### Medium Priority Services (Nice to Have)

#### 3. PromptTemplateService
**Concurrency Risks**:
- `ActivateVersionAsync`: Concurrent version activation
- Cache invalidation races
- Optimistic concurrency in updates

**Test Scenarios**:
- [ ] Concurrent version activation
- [ ] Cache coherence validation
- [ ] Optimistic concurrency control

**Estimated Effort**: 6-8 hours

#### 4. ChatService
**Concurrency Risks**:
- Message creation/deletion races
- Concurrent message ordering
- Chat session consistency

**Test Scenarios**:
- [ ] Concurrent message creation
- [ ] Message ordering preservation
- [ ] Concurrent delete operations

**Estimated Effort**: 6-8 hours

## 🎨 Implementation Pattern (MANDATORY)

### ✅ MUST Follow This Pattern

Reference: `apps/api/tests/Api.Tests/Integration/ConfigurationConcurrencyTests.cs`

```csharp
[Collection("Admin Endpoints")]  // Important for Testcontainers
public class ServiceConcurrencyTests : ConfigIntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public ServiceConcurrencyTests(
        WebApplicationFactoryFixture factory,
        ITestOutputHelper output
    ) : base(factory)
    {
        _output = output;
    }

    [Fact]
    public async Task ConcurrentOperation_NoRaceCondition_Test()
    {
        // Arrange: Setup via HTTP API (uses Testcontainers PostgreSQL)
        var client1 = Factory.CreateHttpsClient();
        var client2 = Factory.CreateHttpsClient();

        // Act: Concurrent operations via HTTP clients
        var task1 = PostAsJsonAuthenticatedAsync(client1, ...);
        var task2 = PostAsJsonAuthenticatedAsync(client2, ...);

        var results = await Task.WhenAll(task1, task2);

        // Assert: Verify correct concurrent behavior
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Validate results...
    }
}
```

### Key Requirements

1. **Inherit from `ConfigIntegrationTestBase`** (provides Testcontainers)
2. **Use `[Collection("Admin Endpoints")]`** attribute
3. **Create separate HTTP clients** for each concurrent actor
4. **Use HTTP API calls** (not direct service calls)
5. **Verify via DbContext** from Factory.Services
6. **Follow existing test naming** conventions

### ❌ DO NOT Use This Pattern

```csharp
// WRONG: SQLite in-memory
_connection = new SqliteConnection("Filename=:memory:");

// WRONG: Shared DbContext across threads
await _service.Operation1();  // Same DbContext
await _service.Operation2();  // Same DbContext

// WRONG: Direct service instantiation
var service = new MyService(_dbContext, ...);
```

## 📋 Acceptance Criteria

### Per-Service Checklist
For each service (RuleSpec, SessionManagement, PromptTemplate, Chat):

- [ ] Test file created in `apps/api/tests/Api.Tests/Integration/`
- [ ] Inherits from `ConfigIntegrationTestBase`
- [ ] Uses `[Collection("Admin Endpoints")]`
- [ ] Implements 3-5 concurrency tests
- [ ] Covers all 4 patterns:
  - Pattern 1: Lost Update Detection
  - Pattern 2: Optimistic Concurrency
  - Pattern 3: TOCTOU Prevention
  - Pattern 4: Cache Coherence
- [ ] All tests pass consistently (10x runs)
- [ ] XML comments document scenarios
- [ ] Real race conditions documented (if found)

### Overall Goals
- [ ] Minimum 4 services tested
- [ ] Minimum 16-20 total tests added
- [ ] 100% pass rate on all tests
- [ ] Zero SQLite-based concurrency tests
- [ ] Documentation updated with new examples
- [ ] CI pipeline includes concurrency tests

## 🔧 Implementation Guidelines

### Step-by-Step Process

#### 1. Setup Test File (15 min)
- Create file: `[Service]ConcurrencyTests.cs`
- Inherit from `ConfigIntegrationTestBase`
- Add `[Collection("Admin Endpoints")]`
- Constructor: `(WebApplicationFactoryFixture factory, ITestOutputHelper output)`

#### 2. API Discovery (30 min)
- Use Serena MCP: `find_symbol("I[Service]")` for interface
- Study existing tests: `[Service]Tests.cs` for patterns
- Identify HTTP endpoints: Check `Program.cs` API routes
- Map entities: Read entity definitions

#### 3. Write Tests (4-6 hours)
- Start with Pattern 1 (simplest)
- Create 2 HTTP clients
- Implement concurrent operation
- Verify results via DbContext
- Repeat for all 4 patterns

#### 4. Validation (30 min)
- Run test 10x: `dotnet test --filter "[TestName]" | grep "Passed\|Failed"`
- Check for flakiness
- Validate assertions are correct
- Update documentation

## 📊 Estimated Effort

**Per Service**: 6-8 hours
- Setup & API discovery: 1h
- Test implementation: 4-5h
- Validation & refinement: 1-2h

**Total (4 services)**: 24-32 hours

**Breakdown**:
- RuleSpecService: 6-8h
- SessionManagementService: 6-8h
- PromptTemplateService: 6-8h
- ChatService: 6-8h

## ✅ Success Metrics

- [ ] 4+ services with Testcontainers-based concurrency tests
- [ ] 16-20+ additional tests
- [ ] 100% pass rate (across 10 runs per test)
- [ ] All 4 patterns validated per service
- [ ] Zero SQLite-based concurrency tests added
- [ ] Documentation includes new service examples
- [ ] CI pipeline runs concurrency tests successfully

## 🔗 Related Issues

- Completed: #601 (TEST-03 Phase 1 - Framework + SQLite limitation discovery)
- Related: #391 (TEST-02 Backend coverage)
- Related: #444 (TEST-05 Frontend coverage)

## 📚 References

### Required Reading
- `docs/testing/concurrency-testing-guide.md` - Complete patterns guide
- `docs/testing/concurrency-testing-implementation-summary.md` - Phase 1+2 learnings
- `apps/api/tests/Api.Tests/Integration/ConfigurationConcurrencyTests.cs` - **MANDATORY REFERENCE**

### Key Learnings from #601 Phase 2
- ✅ Testcontainers is the ONLY correct approach
- ✅ ConfigurationConcurrencyTests pattern proven
- ❌ SQLite fundamentally unsuitable for concurrency
- ✅ API discovery (Serena MCP) prevents compilation errors
- ✅ Incremental approach (1 service at a time) works best

## 🏷️ Labels

`testing`, `concurrency`, `race-conditions`, `testcontainers`, `high-priority`

## 📌 Priority

**High** - Prevents race condition bugs in production, critical for system reliability

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)

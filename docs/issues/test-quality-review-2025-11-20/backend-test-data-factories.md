# Issue: Create Centralized Test Data Factories

**ID**: TEST-003
**Category**: Backend Testing - Code Quality
**Priority**: 🟢 **MEDIUM** (Priority 1)
**Status**: 🔴 Open
**Created**: 2025-11-20

---

## 📋 Summary

Create centralized test data factories to complement existing builders. While builders are excellent for customization, factories provide quick access to common test scenarios, reducing setup boilerplate across 187 test files.

---

## 🎯 Problem Statement

### Current State: Excellent Builders, Missing Factories

**Existing Builders** (✅ Good):
```csharp
// GameBuilder.cs - Great for custom scenarios
var game = new GameBuilder()
    .WithTitle("Catan")
    .WithPlayerCount(2, 4)
    .WithPublisher("Catan Studio")
    .WithYearPublished(1995)
    .Build();
```

**Repetitive Setup** (⚠️ Problem):
```csharp
// Repeated across many test files
public class SomeTests
{
    [Fact]
    public async Task Test1()
    {
        // Common scenario #1: Valid game
        var game = new GameBuilder()
            .WithTitle("Test Game")
            .WithPlayerCount(2, 4)
            .Build();
        // ...
    }

    [Fact]
    public async Task Test2()
    {
        // Common scenario #2: User with role
        var user = new UserBuilder()
            .WithEmail("test@example.com")
            .WithRole(Role.User)
            .Build();
        // ...
    }
}
```

### Issues
- ⚠️ **Repetitive code**: Same builder calls in multiple tests
- ⚠️ **Maintenance burden**: Changing common scenario requires updating many files
- ⚠️ **Inconsistent defaults**: Different tests use different "typical" values
- ⚠️ **Slow test writing**: Setup boilerplate slows down TDD
- ⚠️ **Not discoverable**: Developers don't know what common scenarios exist

---

## 🔧 Solution: Create TestDataFactory

### Recommended Implementation

**File**: `apps/api/tests/Api.Tests/TestHelpers/TestDataFactory.cs`

```csharp
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.BoundedContexts.GameManagement.TestHelpers;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.BoundedContexts.KnowledgeBase.TestHelpers;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Centralized factory for creating common test data scenarios.
/// Complements builders for quick access to typical test cases.
/// </summary>
public static class TestDataFactory
{
    #region Games

    /// <summary>
    /// Creates a valid game with sensible defaults (Catan-like).
    /// </summary>
    public static Game CreateValidGame(string? title = null) =>
        new GameBuilder()
            .WithTitle(title ?? "Test Game")
            .WithPublisher("Test Publisher")
            .WithYearPublished(2000)
            .WithPlayerCount(2, 4)
            .WithPlayTime(45, 90)
            .Build();

    /// <summary>
    /// Creates a game with full BGG metadata.
    /// </summary>
    public static Game CreateGameWithBggData(int bggId = 13, string? metadata = null) =>
        new GameBuilder()
            .WithTitle("Catan")
            .WithPublisher("Catan Studio")
            .WithYearPublished(1995)
            .WithPlayerCount(3, 4)
            .WithPlayTime(60, 120)
            .WithBggLink(bggId, metadata ?? "{\"rating\": 7.2, \"weight\": 2.3}")
            .Build();

    /// <summary>
    /// Creates a game with minimal data (title only).
    /// </summary>
    public static Game CreateMinimalGame(string title = "Minimal Game") =>
        new GameBuilder()
            .WithTitle(title)
            .Build();

    /// <summary>
    /// Creates multiple games for list tests.
    /// </summary>
    public static List<Game> CreateGames(int count)
    {
        var games = new List<Game>();
        for (int i = 1; i <= count; i++)
        {
            games.Add(CreateValidGame($"Game {i}"));
        }
        return games;
    }

    #endregion

    #region Users

    /// <summary>
    /// Creates a standard user with User role.
    /// </summary>
    public static User CreateUser(string? email = null, string? displayName = null) =>
        new UserBuilder()
            .WithEmail(email ?? "user@test.com")
            .WithDisplayName(displayName ?? "Test User")
            .WithRole(Role.User)
            .Build();

    /// <summary>
    /// Creates an admin user.
    /// </summary>
    public static User CreateAdmin(string? email = null) =>
        new UserBuilder()
            .WithEmail(email ?? "admin@test.com")
            .WithDisplayName("Test Admin")
            .WithRole(Role.Admin)
            .Build();

    /// <summary>
    /// Creates an editor user.
    /// </summary>
    public static User CreateEditor(string? email = null) =>
        new UserBuilder()
            .WithEmail(email ?? "editor@test.com")
            .WithDisplayName("Test Editor")
            .WithRole(Role.Editor)
            .Build();

    /// <summary>
    /// Creates a user with 2FA enabled.
    /// </summary>
    public static User CreateUserWith2FA(string? email = null)
    {
        var user = CreateUser(email);
        user.Enable2FA("JBSWY3DPEHPK3PXP");
        return user;
    }

    #endregion

    #region Sessions

    /// <summary>
    /// Creates an active session for a user.
    /// </summary>
    public static Session CreateActiveSession(Guid userId, int expiresInMinutes = 60) =>
        new SessionBuilder()
            .WithUserId(userId)
            .WithExpiresAt(DateTime.UtcNow.AddMinutes(expiresInMinutes))
            .Build();

    /// <summary>
    /// Creates an expired session.
    /// </summary>
    public static Session CreateExpiredSession(Guid userId) =>
        new SessionBuilder()
            .WithUserId(userId)
            .WithExpiresAt(DateTime.UtcNow.AddMinutes(-10))
            .Build();

    /// <summary>
    /// Creates a temporary 2FA session.
    /// </summary>
    public static Session CreateTemp2FASession(Guid userId) =>
        new SessionBuilder()
            .WithUserId(userId)
            .WithExpiresAt(DateTime.UtcNow.AddMinutes(5))
            .AsTemporary2FASession()
            .Build();

    #endregion

    #region PDF Documents

    /// <summary>
    /// Creates a valid PDF document for a game.
    /// </summary>
    public static PdfDocumentEntity CreatePdfDocument(
        Guid gameId,
        string? fileName = null,
        int pageCount = 10) =>
        new PdfDocumentBuilder()
            .WithGameId(gameId)
            .WithFileName(fileName ?? "test-rules.pdf")
            .WithPageCount(pageCount)
            .WithQualityScore(0.85)
            .Build();

    /// <summary>
    /// Creates multiple PDFs for a game.
    /// </summary>
    public static List<PdfDocumentEntity> CreatePdfDocuments(Guid gameId, int count)
    {
        var pdfs = new List<PdfDocumentEntity>();
        for (int i = 1; i <= count; i++)
        {
            pdfs.Add(CreatePdfDocument(gameId, $"document-{i}.pdf", pageCount: 5 + i));
        }
        return pdfs;
    }

    #endregion

    #region Snippets (RAG)

    /// <summary>
    /// Creates valid snippets for citation testing.
    /// </summary>
    public static List<Snippet> CreateValidSnippets(
        Guid pdfId,
        int count = 3)
    {
        var snippets = new List<Snippet>();
        for (int i = 1; i <= count; i++)
        {
            snippets.Add(new Snippet(
                text: $"Sample text {i}",
                source: $"PDF:{pdfId}",
                page: i,
                line: 0,
                score: 0.9f - (i * 0.05f)
            ));
        }
        return snippets;
    }

    /// <summary>
    /// Creates snippets with invalid page numbers.
    /// </summary>
    public static List<Snippet> CreateSnippetsWithInvalidPages(Guid pdfId) =>
        new()
        {
            new Snippet("text", $"PDF:{pdfId}", page: 0, line: 0, score: 0.9f),
            new Snippet("text", $"PDF:{pdfId}", page: -1, line: 0, score: 0.8f),
            new Snippet("text", $"PDF:{pdfId}", page: 999, line: 0, score: 0.7f),
        };

    #endregion

    #region Agents

    /// <summary>
    /// Creates a RAG agent with hybrid search strategy.
    /// </summary>
    public static Agent CreateRagAgent(string? name = null) =>
        new AgentBuilder()
            .WithName(name ?? "Test RAG Agent")
            .WithType(AgentType.RagAgent)
            .WithStrategy(AgentStrategy.HybridSearch())
            .Build();

    /// <summary>
    /// Creates a citation agent.
    /// </summary>
    public static Agent CreateCitationAgent(string? name = null) =>
        new AgentBuilder()
            .WithName(name ?? "Test Citation Agent")
            .WithType(AgentType.CitationAgent)
            .WithStrategy(AgentStrategy.VectorOnly())
            .Build();

    #endregion

    #region Common Scenarios

    /// <summary>
    /// Creates a complete game setup with user, game, and PDFs.
    /// </summary>
    public static (User user, Game game, List<PdfDocumentEntity> pdfs) CreateGameSetup()
    {
        var user = CreateUser();
        var game = CreateValidGame();
        var pdfs = CreatePdfDocuments(game.Id, count: 2);

        return (user, game, pdfs);
    }

    /// <summary>
    /// Creates a complete RAG test scenario.
    /// </summary>
    public static (Game game, PdfDocumentEntity pdf, List<Snippet> snippets) CreateRagScenario()
    {
        var game = CreateValidGame();
        var pdf = CreatePdfDocument(game.Id);
        var snippets = CreateValidSnippets(pdf.Id);

        return (game, pdf, snippets);
    }

    #endregion

    #region Constants

    public static class Defaults
    {
        public const string TestEmail = "test@example.com";
        public const string TestPassword = "Test123!";
        public const string TestDisplayName = "Test User";
        public const string TestGameTitle = "Test Game";
        public const int DefaultPageCount = 10;
        public const double DefaultQualityScore = 0.85;
        public const float DefaultSnippetScore = 0.9f;
    }

    #endregion
}
```

### Usage Examples

#### Before (Repetitive)
```csharp
[Fact]
public async Task UpdateGame_ValidData_Updates()
{
    // Arrange
    var game = new GameBuilder()
        .WithTitle("Test Game")
        .WithPublisher("Test Publisher")
        .WithYearPublished(2000)
        .WithPlayerCount(2, 4)
        .Build();
    // ...
}
```

#### After (Concise)
```csharp
[Fact]
public async Task UpdateGame_ValidData_Updates()
{
    // Arrange
    var game = TestDataFactory.CreateValidGame();
    // ...
}
```

#### Complex Scenarios
```csharp
[Fact]
public async Task ValidateCitations_CompleteSetup_Success()
{
    // Arrange
    var (game, pdf, snippets) = TestDataFactory.CreateRagScenario();

    // Act
    var result = await _service.ValidateCitationsAsync(snippets, game.Id.ToString(), CancellationToken.None);

    // Assert
    Assert.True(result.IsValid);
}
```

---

## 📝 Implementation Checklist

### Phase 1: Create Factory (1-2 hours)
- [ ] Create `apps/api/tests/Api.Tests/TestHelpers/TestDataFactory.cs`
- [ ] Implement Game factories
- [ ] Implement User/Session factories
- [ ] Implement PDF/Snippet factories
- [ ] Implement Agent factories
- [ ] Add XML documentation for all methods
- [ ] Add Constants/Defaults section

### Phase 2: Update High-Impact Tests (1 hour)
- [ ] Refactor `CitationValidationServiceTests.cs` to use factories
- [ ] Refactor `UpdateGameCommandHandlerTests.cs` to use factories
- [ ] Refactor 3-5 other high-traffic test files
- [ ] Verify tests still pass

### Phase 3: Documentation (<1 hour)
- [ ] Add factory usage to testing guide
- [ ] Update CONTRIBUTING.md with factory examples
- [ ] Document when to use builders vs factories

---

## ✅ Acceptance Criteria

- [ ] TestDataFactory class created with 15+ methods
- [ ] Covers all major bounded contexts (Game, User, PDF, Agent)
- [ ] All methods documented with XML comments
- [ ] High-impact tests refactored to use factories
- [ ] Documentation includes builder vs factory guidelines
- [ ] All tests pass with no regression

---

## 📊 Impact Analysis

### Code Reduction

```csharp
// Before: 8 lines setup
var game = new GameBuilder()
    .WithTitle("Test Game")
    .WithPublisher("Test Publisher")
    .WithYearPublished(2000)
    .WithPlayerCount(2, 4)
    .WithPlayTime(45, 90)
    .Build();

// After: 1 line setup
var game = TestDataFactory.CreateValidGame();

// 87% reduction for common scenarios
```

### Consistency
- **Before**: 10 tests might use 10 different "typical" game setups
- **After**: All tests use same `CreateValidGame()`, ensuring consistency

---

## 🔗 Related Issues

- [TEST-001](./backend-test-naming-standardization.md) - Test Naming
- [TEST-002](./backend-test-isolation-fixes.md) - Test Isolation (factories help with fresh data)

---

## 📈 Effort Estimate

**Total: 2-3 hours**

| Phase | Effort | Description |
|-------|--------|-------------|
| Create factory | 1-2h | 15+ methods with documentation |
| Refactor tests | 1h | Update 5-10 high-impact files |
| Documentation | <1h | Update guides |

---

**Last Updated**: 2025-11-20
**Status**: 🔴 Open
**Assignee**: TBD

# Testing Strategy

**Comprehensive Validation for Multi-Agent System**

> 📖 **Parent**: [Multi-Agent Architecture](./README.md)

## Testing Pyramid

```
        /\
       /E2E\      5% - Critical user journeys
      /------\
     /Integr.\   25% - Agent coordination, DB
    /----------\
   /   Unit     \ 70% - Logic, algorithms, handlers
  /--------------\
```

**Target Coverage**: >90% backend, >85% frontend

## Tutor Agent Testing

### Unit Tests

```csharp
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class IntentClassifierTests
{
    [Theory]
    [InlineData("How do I set up the board?", IntentType.Setup)]
    [InlineData("Can I move backwards?", IntentType.Rules)]
    [InlineData("What is this game about?", IntentType.General)]
    public async Task Classify_WithTypicalQueries_ReturnsCorrectIntent(
        string query,
        IntentType expected)
    {
        // Arrange
        var classifier = new IntentClassifier();

        // Act
        var intent = await classifier.ClassifyAsync(query);

        // Assert
        intent.Type.Should().Be(expected);
        intent.Confidence.Should().BeGreaterThan(0.7f);
    }
}
```

### Integration Tests

```csharp
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
public class TutorAgentIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;

    [Fact]
    public async Task HandleQuery_WithMultipleTurns_MaintainsContext()
    {
        // Arrange
        var tutor = new TutorAgent(_dbContext, _qdrant, _llm);
        var sessionId = Guid.NewGuid();

        // Act: Turn 1
        var response1 = await tutor.HandleQueryAsync(
            sessionId,
            "How do I win in Catan?",
            GameIds.Catan
        );

        // Act: Turn 2 (context-dependent)
        var response2 = await tutor.HandleQueryAsync(
            sessionId,
            "How many do I need?",  // Refers to victory points
            GameIds.Catan
        );

        // Assert
        response1.Content.Should().Contain("victory points");
        response2.Content.Should().Contain("10");
    }
}
```

## Arbitro Agent Testing

### Correctness Tests

```csharp
[Theory]
[InlineData("pawn", "e2", "e4", true)]   // Valid advance
[InlineData("pawn", "e2", "e5", false)]  // Invalid 3-square
[InlineData("knight", "b1", "c3", true)] // Valid L-shape
public async Task ValidateMove_WithChessRules_ReturnsCorrectValidity(
    string piece,
    string from,
    string to,
    bool expectedValid)
{
    // Arrange
    var arbitro = new ArbitroAgent(_ruleCache, _resolver);
    var gameState = GameState.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR");
    var move = new Move(piece, from, to);

    // Act
    var result = await arbitro.ValidateMoveAsync(move, gameState);

    // Assert
    result.IsValid.Should().Be(expectedValid);
}
```

### Performance Tests

```csharp
[Fact]
public async Task ValidateMove_Under100msP95()
{
    // Arrange
    var arbitro = new ArbitroAgent(_ruleCache, _resolver);
    var latencies = new List<double>();

    // Act: 100 iterations
    for (int i = 0; i < 100; i++)
    {
        var sw = Stopwatch.StartNew();
        await arbitro.ValidateMoveAsync(TestMoves.SampleChessMove, TestStates.MidGame);
        sw.Stop();
        latencies.Add(sw.Elapsed.TotalMilliseconds);
    }

    // Assert: P95 < 100ms
    var p95 = latencies.OrderBy(x => x).ElementAt(94);
    p95.Should().BeLessThan(100);
}
```

## Decisore Agent Testing

### Strategy Quality

```csharp
[Fact]
public async Task SuggestMove_InChessOpening_MakesReasonableMove()
{
    // Arrange
    var decisore = new DecisoreAgent(_mcts, _evaluator, _strategyRAG);
    var initialState = GameState.InitialPosition(GameIds.Chess);

    // Act
    var move = await decisore.SuggestMoveAsync(
        initialState,
        difficulty: Difficulty.Expert,
        timeBudget: TimeSpan.FromSeconds(5)
    );

    // Assert: Standard opening moves
    var validOpenings = new[] { "e4", "e5", "Nf3", "Nc3", "d4", "d5" };
    move.To.Should().BeOneOf(validOpenings);
}
```

### Difficulty Calibration

```csharp
[Fact]
public async Task SimulateGames_ExpertVsBeginner_ExpertWins80Percent()
{
    // Arrange
    var expert = new DecisoreAgent(Difficulty.Expert);
    var beginner = new DecisoreAgent(Difficulty.Beginner);
    var wins = new Dictionary<string, int> { ["expert"] = 0, ["beginner"] = 0 };

    // Act: Simulate 100 games
    for (int i = 0; i < 100; i++)
    {
        var winner = await SimulateGameAsync(expert, beginner);
        wins[winner]++;
    }

    // Assert: Expert wins >80%
    wins["expert"].Should().BeGreaterThan(80);
}
```

## E2E Testing

```typescript
// apps/web/__tests__/e2e/agent-tutor.spec.ts
import { test, expect } from '@playwright/test';

test('Tutor agent maintains context across conversation', async ({ page }) => {
  await page.goto('/games/catan');

  // Turn 1
  await page.fill('[data-testid="agent-input"]', 'How do I win in Catan?');
  await page.click('[data-testid="agent-submit"]');

  await expect(page.locator('[data-testid="agent-response"]'))
    .toContainText('victory points');

  // Turn 2 (context-dependent)
  await page.fill('[data-testid="agent-input"]', 'How many do I need?');
  await page.click('[data-testid="agent-submit"]');

  await expect(page.locator('[data-testid="agent-response"]'))
    .toContainText('10');
});
```

## Performance Benchmarks

```bash
# Backend
cd apps/api/src/Api
dotnet test --filter "Category=Performance" /p:CollectCoverage=true

# Frontend
cd apps/web
pnpm test:e2e --grep "@performance"
```

## CI/CD Integration

```yaml
# .github/workflows/agent-tests.yml
name: Agent Tests

on: [push, pull_request]

jobs:
  backend-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Unit Tests
        run: dotnet test --filter "Category=Unit"
      - name: Integration Tests
        run: dotnet test --filter "Category=Integration"

  frontend-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: E2E Tests
        run: pnpm test:e2e
```

---

**Related**: [Tutor Agent](./02-tutor-agent.md) | [Integration](./06-integration.md)

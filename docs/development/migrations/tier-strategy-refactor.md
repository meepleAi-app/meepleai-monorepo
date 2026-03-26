# Tier-Strategy Architecture Migration Guide

**Migration guide for the Tier → Strategy → Model refactor**

Issue: #3434 (Architecture) | Related: #3435-#3442

Last updated: 2026-02-04

---

## Overview

This guide documents the architectural change from tier-based model selection to strategy-based model selection.

### Before (❌ Old Architecture)
```
User Tier → Model Selection
```
User tier directly determined which LLM model was used.

### After (✅ New Architecture)
```
User Tier → Available Strategies → Strategy Selection → Model
```
User tier controls which strategies are available; strategy determines the model.

---

## Breaking Changes

### 1. API Request Format

**Before**:
```json
POST /api/v1/rag/ask-question
{
  "gameId": "uuid",
  "question": "How do I set up the game?",
  "searchMode": "Hybrid"
}
```

**After** (strategy parameter added):
```json
POST /api/v1/rag/ask-question
{
  "gameId": "uuid",
  "question": "How do I set up the game?",
  "searchMode": "Hybrid",
  "strategy": "BALANCED"  // NEW: Required field
}
```

### 2. Error Response Changes

**New 403 Error**:
```json
{
  "error": "PRECISE strategy requires Editor tier or higher",
  "code": "STRATEGY_ACCESS_DENIED",
  "availableStrategies": ["FAST", "BALANCED"]
}
```

### 3. Strategy Enum Values

Valid strategy values:
- `FAST` - Quick responses, free models
- `BALANCED` - Standard quality, budget models
- `PRECISE` - High quality, premium models (Editor+)
- `EXPERT` - Multi-hop reasoning (Admin+)
- `CONSENSUS` - Multi-model voting (Admin+)
- `CUSTOM` - Admin-configured (Admin only)

---

## Migration Steps

### Step 1: Update API Clients

#### TypeScript/Frontend

```typescript
// Before
const response = await api.rag.askQuestion({
  gameId,
  question,
  searchMode: 'Hybrid'
});

// After
const response = await api.rag.askQuestion({
  gameId,
  question,
  searchMode: 'Hybrid',
  strategy: 'BALANCED'  // Add strategy parameter
});
```

#### Available Types

```typescript
import type {
  RagStrategy,
  UserTier,
  TierStrategyAccessDto,
  StrategyModelMappingDto
} from '@/lib/api';

// RagStrategy: 'FAST' | 'BALANCED' | 'PRECISE' | 'EXPERT' | 'CONSENSUS' | 'CUSTOM'
// UserTier: 'Anonymous' | 'User' | 'Editor' | 'Admin' | 'Premium'
```

### Step 2: Handle Strategy Access Errors

```typescript
try {
  const response = await api.rag.askQuestion({ ... });
} catch (error) {
  if (error.code === 'STRATEGY_ACCESS_DENIED') {
    // Show available strategies to user
    const available = error.availableStrategies;
    // Prompt user to select a valid strategy
  }
}
```

### Step 3: Update Strategy Selection UI

```tsx
// Use the new hooks
import {
  useTierStrategyMatrix,
  useStrategyModelMappings
} from '@/hooks/queries';

function StrategySelector({ userTier }: { userTier: string }) {
  const { data: matrix } = useTierStrategyMatrix();

  const availableStrategies = matrix?.accessMatrix
    .filter(a => a.tier === userTier && a.isEnabled)
    .map(a => a.strategy) ?? [];

  return (
    <select>
      {availableStrategies.map(strategy => (
        <option key={strategy} value={strategy}>
          {strategy}
        </option>
      ))}
    </select>
  );
}
```

### Step 4: Update Backend Services

#### CQRS Pattern

All tier-strategy operations use CQRS:

```csharp
// Query: Get available strategies for a tier
var matrix = await _mediator.Send(new GetTierStrategyMatrixQuery());

// Command: Update access
await _mediator.Send(new UpdateTierStrategyAccessCommand
{
    Tier = "User",
    Strategy = "PRECISE",
    IsEnabled = true
});
```

#### Validation

```csharp
// Validate strategy access in query handlers
public async Task<QaResponseDto> Handle(
    AskQuestionQuery request,
    CancellationToken cancellationToken)
{
    var user = await _userContext.GetCurrentUserAsync();
    var hasAccess = await _tierStrategyAccessService
        .ValidateAccessAsync(user.Tier, request.Strategy);

    if (!hasAccess)
    {
        throw new StrategyAccessDeniedException(
            request.Strategy,
            user.Tier);
    }

    // Continue with query...
}
```

---

## Testing the Migration

### Unit Tests

```typescript
// Test strategy access validation
describe('Strategy Access', () => {
  it('should allow User tier FAST and BALANCED', async () => {
    const matrix = await api.tierStrategy.getMatrix();
    const userStrategies = matrix.accessMatrix
      .filter(a => a.tier === 'User' && a.isEnabled);

    expect(userStrategies.map(s => s.strategy))
      .toEqual(['FAST', 'BALANCED']);
  });

  it('should deny User tier PRECISE', async () => {
    await expect(
      api.rag.askQuestion({
        gameId: 'test',
        question: 'test',
        strategy: 'PRECISE'
      })
    ).rejects.toThrow('STRATEGY_ACCESS_DENIED');
  });
});
```

### Integration Tests

```csharp
[Fact]
public async Task User_CanAccess_BalancedStrategy()
{
    // Arrange
    var user = await CreateUserWithTier("User");
    var query = new AskQuestionQuery
    {
        Strategy = "BALANCED"
    };

    // Act
    var response = await _mediator.Send(query);

    // Assert
    response.Should().NotBeNull();
}

[Fact]
public async Task User_CannotAccess_PreciseStrategy()
{
    // Arrange
    var user = await CreateUserWithTier("User");
    var query = new AskQuestionQuery
    {
        Strategy = "PRECISE"
    };

    // Act & Assert
    await Assert.ThrowsAsync<StrategyAccessDeniedException>(
        () => _mediator.Send(query)
    );
}
```

---

## Rollback Plan

If issues are encountered:

1. **Revert API changes**: Remove strategy parameter requirement
2. **Restore old routing**: Re-enable tier-based model selection
3. **Database**: No schema changes required (config stored in JSON)

---

## Configuration Reference

### Default Access Matrix

| Tier | FAST | BALANCED | PRECISE | EXPERT | CONSENSUS | CUSTOM |
|------|:----:|:--------:|:-------:|:------:|:---------:|:------:|
| Anonymous | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| User | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| Editor | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| Admin | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

### Default Model Mappings

| Strategy | Provider | Primary Model | Fallback |
|----------|----------|---------------|----------|
| FAST | OpenRouter | meta-llama/llama-3.3-70b-instruct:free | gpt-4o-mini |
| BALANCED | DeepSeek | deepseek-chat | claude-haiku-4.5 |
| PRECISE | Anthropic | claude-sonnet-4.5 | claude-haiku-4.5 |
| EXPERT | Anthropic | claude-sonnet-4.5 | gpt-4o |
| CONSENSUS | Mixed | multi-model | - |
| CUSTOM | Anthropic | claude-haiku-4.5 | claude-sonnet-4.5 |

---

## Related Documentation

- [Admin Configuration Guide](../admin/rag-tier-strategy-config.md)
- [RAG Architecture Overview](../api/rag/HOW-IT-WORKS.md)
- [Layer 1: Routing](../api/rag/02-layer1-routing.md)
- [RAG Flow Diagram](../api/rag/diagrams/rag-flow-current.md)

# Strategy Pattern Seeding

Issue #3984 | Bounded Context: KnowledgeBase

## Overview

The `StrategyPatternSeeder` seeds pre-analyzed board game strategy patterns into the `strategy_patterns` table. These patterns enable the Arbitro Agent to provide faster strategy recommendations via semantic search.

## Architecture

```
AutoConfigurationService (startup)
  └─ StrategyPatternSeeder.SeedAsync()
       ├─ Lookup game by name in SharedGameCatalog
       ├─ Check idempotency (skip if patterns exist)
       ├─ Create StrategyPatternEntity records
       ├─ Generate embeddings via IEmbeddingService (if available)
       └─ SaveChanges to PostgreSQL
```

**Location**: `apps/api/src/Api/Infrastructure/Seeders/StrategyPatternSeeder.cs`

## Seeded Games and Patterns

| Game | Patterns | Phases |
|------|----------|--------|
| Chess | 8 | opening, midgame, endgame |
| Catan | 4 | opening, midgame |
| Carcassonne | 3 | opening, midgame, any |
| Ticket to Ride | 3 | opening, midgame |
| Pandemic | 3 | opening, midgame, any |
| Splendor | 3 | opening, midgame, any |
| **Total** | **24** | |

## Data Structure

Each pattern contains:

| Field | Type | Description |
|-------|------|-------------|
| `PatternName` | string(200) | Descriptive name (e.g., "Italian Game") |
| `Description` | text | Detailed strategy explanation |
| `ApplicablePhase` | string(100) | Game phase: opening, midgame, endgame, any |
| `BoardConditionsJson` | jsonb | Preconditions for applying the strategy |
| `MoveSequenceJson` | jsonb | Recommended moves or actions |
| `EvaluationScore` | float | Effectiveness score 0.0-1.0 |
| `Source` | string(100) | Attribution (e.g., "chess.com", "manual") |
| `Embedding` | vector(1536) | Semantic search vector (auto-generated) |

## Configuration

In `appsettings.json`:

```json
{
  "Seeding": {
    "EnableStrategyPatterns": true
  }
}
```

Set `EnableStrategyPatterns` to `false` to skip strategy pattern seeding on startup.

## Embedding Generation

When `IEmbeddingService` is available, the seeder generates vector embeddings for each pattern using the text `"{PatternName}: {Description}"`. Embeddings are generated in batches of 5 to avoid overloading the service.

If the embedding service is unavailable or fails, patterns are still seeded without embeddings. A warning is logged and the missing embeddings can be generated later.

## Idempotency

The seeder is safe to run multiple times:
- It checks if patterns already exist for each game (`COUNT > 0`)
- If any patterns exist for a game, that game is skipped entirely
- No duplicate data is created on repeated runs

## Adding New Patterns

To add patterns for a new game:

1. Add an entry to the `GameStrategyPatterns` dictionary in `StrategyPatternSeeder.cs`:

```csharp
["Game Name"] =
[
    new("Pattern Name", "phase", "Description of the strategy.", 0.75,
        """{"condition":"value"}""",
        """{"move":"value"}""",
        "source"),
],
```

2. Ensure the game exists in the SharedGameCatalog (seeded via BGG or manually)
3. The seeder will pick up the new patterns on next startup

## Dependencies

- **Requires**: `strategy_patterns` table (Migration: `InitialCreate`)
- **Requires**: Games in SharedGameCatalog matching pattern game names
- **Optional**: Embedding service for vector generation
- **Called by**: `AutoConfigurationService.SeedSharedGamesAndRelatedDataAsync()`

## Related Issues

- #3493: PostgreSQL Schema Extensions (table creation)
- #3956: Technical Debt - Complete deferred work
- #3984: Seeding implementation with embeddings and configuration

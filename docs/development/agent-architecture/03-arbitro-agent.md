# Arbitro Agent - Rules Arbitration Engine

**Priority 2 | Complexity: High | Timeline: 6 weeks**

> 📖 **Parent**: [Multi-Agent Architecture](./README.md)

## Capabilities

- **Real-Time Move Validation**: <100ms P95 latency
- **Conflict Resolution**: Handle ambiguous/contradictory rules
- **Edge Case Arbitration**: LLM fallback for complex scenarios
- **State Consistency**: Maintain valid game state

## Tech Stack

```yaml
Rule Storage: PostgreSQL (structured) + Qdrant (semantic)
Cache: Redis (hot rules, O(1) lookup)
State: In-memory with event sourcing
Conflict Resolution: Rule precedence graph + LLM
Integration: Event-driven message bus
```

## Architecture: 3-Tier Rule Retrieval

```
Move Validation Request
    ↓
Tier 1: Redis Hot Cache (μs) → 80%+ hit rate
    ↓ (miss)
Tier 2: In-Memory Rule Graph (ms)
    ↓ (miss)
Tier 3: Qdrant Semantic Search (100ms)
```

## Event-Driven Integration

```python
class ArbitroEventHandler:
    async def on_move_attempted(self, event: MoveAttempted):
        # Retrieve applicable rules
        rules = await self.get_move_rules(
            event.piece_type,
            event.from_position,
            event.to_position,
            event.game_state
        )

        # Validate
        is_valid, reason = await self.validate_move(event, rules)

        # Publish result
        if is_valid:
            await self.publish(MoveApproved(event))
        else:
            await self.publish(MoveRejected(event, reason))
```

## Conflict Resolution

```python
class RuleConflictResolver:
    async def resolve(self, conflicts: List[Rule]) -> Rule:
        # Step 1: Check explicit precedence graph
        for rule in conflicts:
            if self.has_precedence(rule, conflicts):
                return rule

        # Step 2: LLM arbitration for edge cases
        arbitration_prompt = self.build_prompt(conflicts)
        decision = await self.llm.generate(arbitration_prompt)

        # Step 3: Log for human review
        await self.log_arbitration(conflicts, decision)

        return decision
```

## Performance Targets

| Metric | Target | Rationale |
|--------|--------|-----------|
| **Validation Latency** | <100ms P95 | Real-time gameplay |
| **Cache Hit Rate** | >80% | Frequent rules cached |
| **Conflict Accuracy** | >95% | Human review sample |

## Implementation Phases

**Phase 1 (Weeks 11-12)**: Rule precedence graph + Redis cache
**Phase 2 (Weeks 13-14)**: Conflict resolution engine + LLM fallback
**Phase 3 (Weeks 15-16)**: Event-driven integration + monitoring

## API Endpoints

### POST /api/v1/agents/arbitro/validate
```typescript
Request:
{
  "gameId": "uuid",
  "move": {
    "piece": "knight",
    "from": "b1",
    "to": "c3"
  },
  "gameState": { ... }
}

Response:
{
  "valid": true,
  "reason": "Knight moves in L-shape, position is legal",
  "appliedRules": ["knight_movement", "no_obstruction"]
}
```

---

**Related**: [Tutor Agent](./02-tutor-agent.md) | [Decisore Agent](./04-decisore-agent.md)

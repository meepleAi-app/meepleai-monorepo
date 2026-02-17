# Decisore Agent - Strategic AI

**Priority 3 | Complexity: Very High | Timeline: 8 weeks**

> 📖 **Parent**: [Multi-Agent Architecture](./README.md)

## Capabilities

- **Strategic Move Generation**: Suggest optimal moves
- **Strategy Explanation**: Articulate reasoning
- **Difficulty Scaling**: Beginner to expert AI
- **Multi-Turn Planning**: Lookahead for complex strategies

## Hybrid Approach: MCTS + LLM

**Rationale**: MCTS guides search, LLM evaluates positions

```
Current Position
    ↓
Retrieve Strategy Patterns (Context Engineering)
    ↓
MCTS Tree Search (UCB1 policy)
├─ Selection → UCB1
├─ Expansion → Legal moves
├─ Evaluation → LLM position scorer
└─ Backpropagation → Update statistics
    ↓
Best Move + Explanation
```

## Tech Stack

```yaml
Search: MCTS with UCB1
Evaluator: LLM (GPT-4/Claude) + cached patterns
Knowledge: Strategy RAG (similar positions)
Budget: Adaptive depth (beginner: 1s, expert: 10s)
```

## Difficulty Profiles

| Level | Time Budget | Depth | Exploration |
|-------|-------------|-------|-------------|
| **Beginner** | 1s | 2 plies | High (2.0) |
| **Intermediate** | 3s | 4 plies | Medium (1.4) |
| **Expert** | 10s | 8 plies | Low (1.0) |

## Position Evaluation

```python
async def evaluate_position(state: GameState, strategies: List[Strategy]) -> float:
    """LLM-based position evaluation"""

    # Check cache first
    cache_key = hash(state.to_string())
    if cache_key in self.evaluation_cache:
        return self.evaluation_cache[cache_key]

    # LLM evaluation (expensive)
    prompt = f"""
    Evaluate this position on scale -1.0 to 1.0:
    Board: {state.to_string()}
    Phase: {state.phase}
    Relevant Strategies: {strategies}

    Consider: Material, position, tempo, threats
    Return: Single float value
    """

    score = float(await self.llm.generate(prompt))
    self.evaluation_cache[cache_key] = score  # Cache

    return score
```

## Performance Targets

| Metric | Target |
|--------|--------|
| **Move Generation** | <10s (expert) |
| **Difficulty Distinguishable** | Win rate >80% (expert vs beginner) |
| **Explanation Quality** | >80% rated helpful |

## Implementation Phases

**Phase 1 (Weeks 17-19)**: MCTS engine + UCB1
**Phase 2 (Weeks 20-22)**: LLM evaluator + strategy RAG
**Phase 3 (Weeks 23-24)**: Difficulty scaling + explanation generation

## Future: Fine-Tuned Evaluator

**Current**: GPT-4 API (~$0.25/evaluation, 2s latency)
**Future**: Fine-tuned Llama-3-8B (~$0.005/evaluation, 200ms)

Train on 10K+ game positions → Self-hosted inference

---

**Related**: [Tutor Agent](./02-tutor-agent.md) | [Arbitro Agent](./03-arbitro-agent.md)

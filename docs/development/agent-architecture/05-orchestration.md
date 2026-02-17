# LangGraph Orchestration

**Multi-Agent Coordination with Event-Driven Architecture**

> 📖 **Parent**: [Multi-Agent Architecture](./README.md)

## Framework Comparison

| Framework | Best For | MeepleAI Fit |
|-----------|----------|--------------|
| **LangGraph** | Stateful, sequential workflows | ⭐⭐⭐⭐⭐ (Optimal) |
| **CrewAI** | Parallel tasks | ⭐⭐⭐ (Good) |
| **AutoGen** | Code generation | ⭐⭐ (Limited) |

**Decision**: LangGraph for turn-based game workflows

## Orchestrator Pattern

```python
from langgraph.graph import StateGraph

@dataclass
class GameAgentState:
    game_id: str
    current_player: str
    board_state: BoardState
    conversation_history: List[Message]
    pending_move: Optional[Move]
    validation_result: Optional[ValidationResult]
    strategy_suggestion: Optional[Move]

# Agent nodes
graph = StateGraph(GameAgentState)
graph.add_node("tutor", tutor_node)
graph.add_node("arbitro", arbitro_node)
graph.add_node("decisore", decisore_node)

# Dynamic routing
def route_by_action(state: GameAgentState) -> str:
    if state.user_query:
        return "tutor"
    elif state.pending_move:
        return "arbitro"
    elif state.request_suggestion:
        return "decisore"
    return END

graph.set_conditional_entry_point(route_by_action)

# Sequential validation: AI moves must be validated
graph.add_edge("decisore", "arbitro")

orchestrator = graph.compile()
```

## Event-Driven Alternative

```python
class MeepleAIMessageBus:
    """Looser coupling via message bus"""

    def __init__(self):
        self.handlers = {
            "user_query": [tutor_agent],
            "move_attempted": [arbitro_agent],
            "suggest_move": [decisore_agent]
        }

    async def publish(self, event: Event):
        handlers = self.handlers.get(event.type, [])
        await asyncio.gather(*[h.handle(event) for h in handlers])
```

## Coordination Patterns

**Tutor → Arbitro**: User asks about move legality
**Decisore → Arbitro**: AI-suggested move requires validation
**All → Context Engineering**: Shared context assembly

---

**Related**: [Context Engineering](./01-context-engineering.md) | [Integration](./06-integration.md)

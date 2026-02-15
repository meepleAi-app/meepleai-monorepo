# Multi-Agent Router Architecture

**Issue**: #4336 - Multi-Agent Router - Intelligent Request Routing
**Epic**: #3490 - Multi-Agent Game AI System
**Sprint**: 5 (Integration Phase)

## Overview

Intelligent routing system that directs user queries to the appropriate specialized agent (Tutor, Arbitro, or Decisore) based on intent classification.

## Architecture

```
User Query
    ↓
Intent Classifier (keyword-based + confidence scoring)
    ↓
Multi-Agent Router (routing decision)
    ↓
┌────────────┬──────────────┬───────────────┐
│ TutorAgent │ ArbitroAgent │ DecisoreAgent │
└────────────┴──────────────┴───────────────┘
    ↓
Unified Response (with routing metadata)
```

## Intent Classification

### Intent Types

| Intent | Description | Target Agent | Keywords |
|--------|-------------|--------------|----------|
| **Tutorial** | Learning, setup, how-to questions | Tutor | "how to", "setup", "learn", "explain", "tutorial" |
| **RulesQuestion** | Rules clarification, game mechanics | Tutor | "rule", "allowed", "can I", "is it legal" |
| **MoveValidation** | Real-time move arbitration | Arbitro | "validate", "is this move valid", "can I do", "allowed to move" |
| **StrategicAnalysis** | Move suggestions, position analysis | Decisore | "suggest", "best move", "strategy", "analyze position", "what should I do" |

### Classification Algorithm

**Simple Keyword Matching** (Phase 1):
```csharp
Confidence = (MatchedKeywords / TotalKeywords) * Weight + ContextBonus
```

**Thresholds**:
- **High confidence** (>0.90): Route directly
- **Medium confidence** (0.70-0.90): Route with suggestion
- **Low confidence** (<0.70): Fallback to multi-agent or ask clarification

## Routing Logic

### Primary Routing Map

```
Tutorial Intent → TutorAgent.AnswerQuestionAsync()
RulesQuestion → TutorAgent.AnswerQuestionAsync() (rules-focused)
MoveValidation → ArbitroAgent.ValidateMoveAsync()
StrategicAnalysis → DecisoreAgent.AnalyzePositionAsync()
```

### Fallback Strategies

**Ambiguous Intent** (multiple intents detected):
1. Return suggestions for all matching agents
2. User selects preferred agent
3. Cache user preference for similar queries

**No Clear Intent** (<0.70 confidence):
1. Default to TutorAgent (most general)
2. Log as routing failure for improvement
3. Collect user feedback on routing quality

**Agent Unavailable**:
1. Check agent health status
2. Route to fallback agent (TutorAgent)
3. Notify user of degraded service

## Service Architecture

### Core Components

**1. IntentClassifier**
```csharp
interface IIntentClassifier
{
    (AgentIntent intent, double confidence) ClassifyQuery(string query, GameContext? context);
}
```

**2. AgentRouter**
```csharp
interface IAgentRouter
{
    Task<AgentRoutingDecision> RouteQueryAsync(string query, GameContext? context);
}
```

**3. MultiAgentCoordinator** (orchestrates routing)
```csharp
interface IMultiAgentCoordinator
{
    Task<UnifiedAgentResponse> ProcessQueryAsync(AgentRoutingRequest request);
}
```

### Data Flow

```
1. User submits query via POST /api/v1/agents/route
2. IntentClassifier analyzes query → (intent, confidence)
3. AgentRouter selects agent based on intent + confidence
4. If confidence >0.90: Route to agent directly
5. If confidence 0.70-0.90: Suggest agent, ask confirmation
6. If confidence <0.70: Return fallback options
7. Execute agent invocation
8. Transform agent response to unified format
9. Log routing decision + metrics
10. Return unified response with routing metadata
```

## Performance Targets

| Metric | Target | Notes |
|--------|--------|-------|
| Intent classification | <10ms | Keyword matching, no ML |
| Routing decision | <50ms P95 | Including confidence calculation |
| End-to-end | <100ms overhead | On top of agent execution time |
| Classification accuracy | >95% | For clear intent queries |

## Monitoring & Metrics

### Routing Metrics
- Agent usage distribution (% queries to each agent)
- Intent classification accuracy (via user feedback)
- Routing confidence scores (avg, P50, P95)
- Fallback rate (% queries <0.70 confidence)
- Routing latency (P50, P95, P99)

### Quality Metrics
- User satisfaction with routing decisions
- Re-routing rate (user manually selects different agent)
- Agent switching rate (query ambiguity)

## Implementation Phases

### Phase 1: Core Routing (This Sprint)
- [x] Intent classifier with keyword matching
- [x] Agent router service
- [x] Unified routing endpoint
- [x] Basic fallback logic
- [x] Routing metrics collection

### Phase 2: Advanced Features (Future)
- [ ] ML-based intent classification (fine-tuned LLM)
- [ ] Context-aware routing (game state, user history)
- [ ] Multi-agent chaining (Tutor → Arbitro → Decisore)
- [ ] Adaptive confidence thresholds
- [ ] User preference learning

### Phase 3: Optimization (Future)
- [ ] Caching routing decisions for similar queries
- [ ] Parallel agent invocation for ambiguous cases
- [ ] Agent load balancing
- [ ] A/B testing for classification algorithms

## API Design

### Request
```http
POST /api/v1/agents/route
{
  "query": "Should I move my knight to e5?",
  "gameSessionId": "guid",
  "playerName": "Alice",
  "gameContext": {
    "currentPhase": "midgame",
    "turnNumber": 15
  }
}
```

### Response
```json
{
  "routedTo": "ArbitroAgent",
  "intent": "MoveValidation",
  "confidence": 0.95,
  "response": { ...agent-specific response... },
  "alternativeAgents": null,
  "routingLatencyMs": 45
}
```

### Fallback Response (Low Confidence)
```json
{
  "routedTo": null,
  "intent": "Ambiguous",
  "confidence": 0.65,
  "suggestedAgents": [
    {"agent": "ArbitroAgent", "confidence": 0.65, "reason": "Move validation keywords"},
    {"agent": "TutorAgent", "confidence": 0.60, "reason": "Rules clarification"}
  ],
  "message": "Query intent unclear. Please select an agent or rephrase."
}
```

---

**Status**: Design Complete
**Next**: Implementation (Tasks #14-17)

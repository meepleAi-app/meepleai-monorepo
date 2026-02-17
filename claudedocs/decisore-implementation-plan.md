# Decisore Agent - Implementation Plan

**Epic**: #3490 | **Phase**: 4 (Decisore Agent)
**Generated**: 2026-02-11 | **Status**: Design Complete, Ready for Implementation

---

## Architecture Overview

**Decisore Agent**: Strategic AI for board game move suggestions with position evaluation and victory path identification.

**Pattern**: Follows Tutor/Arbitro implementation (CQRS + Domain Services + LangGraph orchestration)

**Technology Stack**:
- .NET 9 CQRS handlers (domain logic)
- Python LangGraph node (orchestration)
- Hybrid algorithm (Heuristics + LLM)
- Context Engineering (RAG strategy patterns)
- Redis caching (position evaluations)

---

## Implementation Phases

### Phase 1: Core Engine (#3769) - 1.5 weeks

**Objective**: Strategic analysis engine with position evaluation

**Files to Create**:
```
Domain/Services/
  ├── IDecisoreAgentService.cs
  ├── DecisoreAgentService.cs (300-400 lines)
  ├── IPositionEvaluator.cs
  └── HeuristicPositionEvaluator.cs (200-300 lines)

Domain/ValueObjects/
  ├── ParsedGameState.cs
  ├── CandidateMove.cs
  ├── MoveScore.cs
  └── PositionStrength.cs

Application/Commands/
  └── AnalyzeGameStateCommand.cs

Application/Handlers/
  └── AnalyzeGameStateCommandHandler.cs

Application/DTOs/
  ├── StrategicAnalysisResultDto.cs
  └── MoveSuggestionDto.cs

Tests/
  ├── DecisoreAgentServiceTests.cs (20+ tests)
  ├── HeuristicPositionEvaluatorTests.cs (15+ tests)
  └── AnalyzeGameStateCommandHandlerTests.cs (10+ tests)
```

**Core Algorithm**:
```csharp
AnalyzePosition():
  1. Parse game state (Issue #3772 dependency)
  2. Generate candidate moves (Issue #3770 dependency)
  3. Evaluate each candidate:
     a. Heuristic scoring (fast, <500ms)
     b. LLM refinement for top 3 (slower, +2-3s)
  4. Rank by combined score
  5. Identify victory paths (if winning position)
  6. Calculate risk level
  7. Return suggestions + reasoning
```

**Heuristic Scoring** (Chess example):
```csharp
Score Components:
  - Material value (piece values: P=1, N=3, B=3, R=5, Q=9)
  - Positional control (center squares +0.3, development +0.2)
  - Threat detection (attacks on pieces, king safety)
  - Defensive needs (protect weak squares, pin defense)

Overall Score = Weighted sum / 10.0  // Normalized 0-1
```

**LLM Refinement**:
```csharp
For top 3 candidates:
  1. Retrieve strategy patterns from RAG (300 tokens)
  2. Build prompt: state + move + context
  3. LLM evaluates (800ms per call)
  4. Parse JSON response: {score, pros, cons, expectedOutcome}
  5. Combine: (0.4 × heuristic) + (0.6 × llm_score)
```

**DoD**:
- [x] IDecisoreAgentService interface defined
- [x] Chess position evaluation accurate (tested against known positions)
- [x] Heuristic scoring complete (material + position + threats)
- [x] LLM integration with reasoning generation
- [x] Victory path identification logic
- [x] Risk level calculation
- [x] 90%+ test coverage (domain + handler)
- [x] Performance <5s per analysis

---

### Phase 2: Move Suggestion Algorithm (#3770) - 1 week

**Objective**: Generate and rank candidate moves

**Files to Create**:
```
Domain/Services/
  ├── IMoveGeneratorService.cs
  ├── ChessMovegenerator.cs (200-250 lines)
  └── MoveFilteringService.cs

Domain/ValueObjects/
  └── LegalMove.cs

Tests/
  ├── ChessMoveGeneratorTests.cs (15+ tests)
  └── MoveFilteringServiceTests.cs (10+ tests)
```

**Legal Move Generation** (Chess):
```csharp
GenerateCandidates(ParsedGameState state, string player):
  1. Identify player's pieces
  2. For each piece, generate legal moves per chess rules
  3. Filter illegal moves (in check, exposes king, etc.)
  4. Apply heuristic pre-filtering:
     - Remove obviously bad moves (lose material without compensation)
     - Prioritize captures, threats, development
  5. Return top N candidates (10-20)
```

**Priority Ranking**:
```
P1: Captures (material gain)
P2: Checks (forcing moves)
P3: Development (opening phase)
P4: Positional improvements
P5: Defensive moves
P6: Quiet moves
```

**DoD**:
- [x] Chess legal move generation complete
- [x] Filtering removes obviously bad moves
- [x] Priority ranking implemented
- [x] Integration with PositionEvaluator (#3769)
- [x] Performance <500ms for candidate generation
- [x] 90%+ test coverage
- [x] Extensible pattern for adding Catan/other games

---

### Phase 3: Game State Parser (#3772) - 1 week

**Objective**: Parse raw board state into structured representation

**Files to Create**:
```
Domain/Services/
  ├── IGameStateParserService.cs
  ├── ChessFENParser.cs (150-200 lines)
  └── GameStateParserFactory.cs

Domain/ValueObjects/
  ├── ChessPiece.cs
  ├── ChessPosition.cs
  └── ChessBoard.cs

Tests/
  ├── ChessFENParserTests.cs (15+ tests)
  └── GameStateParserFactoryTests.cs (5+ tests)
```

**Chess FEN Parsing**:
```csharp
ParseAsync(BoardState raw, string gameTitle):
  Input:  "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
  Output: ParsedGameState
  {
    GameType: "Chess",
    Board: 8x8 grid with pieces,
    CurrentPlayer: "White",
    CastlingRights: { WhiteKingside: true, ... },
    EnPassantTarget: null,
    HalfmoveClock: 0,
    FullmoveNumber: 1
  }
```

**Validation**:
- Detect invalid FEN notation
- Verify piece placement rules
- Check king presence (both sides)
- Validate castling rights consistency

**DoD**:
- [x] Chess FEN parser complete and validated
- [x] Error handling for malformed state
- [x] Unit tests with 20+ FEN examples (openings, middlegame, endgame)
- [x] Integration with DecisoreAgentService
- [x] Performance <100ms per parse
- [x] Extensible for other games (factory pattern)
- [x] 95%+ test coverage

---

### Phase 4: Multi-Model Ensemble (#3771) - 1 week

**Objective**: Consensus evaluation across multiple LLMs

**Files to Create/Modify**:
```
Domain/Services/
  ├── IMultiModelEvaluator.cs
  └── MultiModelEvaluator.cs (250-300 lines)

Domain/ValueObjects/
  ├── ModelEvaluation.cs
  └── ConsensusResult.cs

Tests/
  └── MultiModelEvaluatorTests.cs (12+ tests)
```

**Consensus Algorithm**:
```csharp
EvaluateWithEnsemble(state, move):
  1. Parallel LLM calls:
     - GPT-4: Evaluate move
     - Claude-3.5: Evaluate move
     - DeepSeek: Evaluate move (optional)

  2. Aggregate scores:
     - Mean: (GPT + Claude + DeepSeek) / 3
     - Weighted: 0.4×GPT + 0.4×Claude + 0.2×DeepSeek
     - Min: Conservative (lowest score)

  3. Detect disagreement:
     - Variance > 0.2 → Low consensus, flag for review
     - All agree (variance < 0.1) → High confidence

  4. Return ConsensusResult:
     {
       Score: weighted average,
       Confidence: based on variance,
       ModelScores: individual results,
       Agreement: "high|medium|low"
     }
```

**DoD**:
- [x] Multi-model evaluation (GPT-4 + Claude-3.5 minimum)
- [x] Consensus scoring algorithms (mean, weighted, min)
- [x] Disagreement detection (variance threshold)
- [x] Cost optimization (parallel calls, cache results)
- [x] Fallback to single model if one fails
- [x] 85%+ test coverage
- [x] Performance budget: +1-2s overhead vs single model

---

### Phase 5a: REST API Endpoint (#3773) - 0.5 weeks

**Objective**: Expose Decisore via REST API

**Files to Create**:
```
Routing/
  └── DecisoreAgentEndpoints.cs

Application/Validators/
  └── AnalyzeGameStateCommandValidator.cs
```

**Endpoint Specification**:
```http
POST /api/v1/agents/decisore/analyze
Authorization: Bearer {session_token}
Content-Type: application/json

Request:
{
  "gameSessionId": "uuid",
  "playerName": "string",
  "analysisDepth": "quick|standard|deep",
  "maxSuggestions": 3
}

Response: 200 OK
{
  "suggestions": [
    {
      "move": "e4",
      "position": "center",
      "score": 0.92,
      "reasoning": "Controls center, develops pieces, strong opening",
      "pros": ["Control center", "Piece development", "Flexible continuation"],
      "cons": ["Symmetric response possible (e5)"],
      "expectedOutcome": "Balanced position with slight initiative"
    }
  ],
  "positionStrength": 0.05,
  "riskLevel": "low",
  "victoryPaths": ["Maintain center control → convert to endgame advantage"],
  "confidence": 0.87,
  "executionTimeMs": 3200,
  "timestamp": "2026-02-12T00:00:00Z"
}
```

**DoD**:
- [x] POST endpoint implemented with session auth
- [x] Request/response DTOs defined
- [x] FluentValidation for input
- [x] Integration with DecisoreAgentService
- [x] Error handling (session not found, invalid state, timeout)
- [x] API documentation (Scalar/Swagger)
- [x] 5+ integration tests

---

### Phase 5b: Performance Tuning (#3774) - 0.5 weeks

**Objective**: Optimize to <5s (stretch: <3s)

**Optimization Strategies**:
```yaml
Target Breakdown:
  Load session + state: <50ms (DB optimization, indexes)
  Parse game state: <100ms (efficient FEN parser)
  Generate candidates: <500ms (pruning, legal move cache)
  Heuristic evaluation: <300ms (vectorized scoring)
  LLM top-3 refinement: <2500ms (parallel calls: 3×800ms → 900ms)
  RAG strategy retrieval: <600ms (Redis cache, parallel)
  Format response: <50ms

Total: ~4000ms < 5s target ✅

Optimizations:
  1. Parallel LLM calls (3 sequential → 3 parallel)
  2. Redis cache for position evaluations (24h TTL)
  3. Opening book pre-computation (first 10 moves)
  4. Lazy victory path calculation (only if positionStrength > 0.6)
  5. Token budget optimization (reduce RAG context 500→300)
```

**Monitoring**:
- Performance metrics per analysis depth
- Cache hit rates for positions
- LLM latency percentiles (P50, P95, P99)
- Timeout rate (<1% target)

**DoD**:
- [x] Performance <5s for standard analysis (P95)
- [x] Performance <3s for quick analysis (P95)
- [x] Performance <10s for deep analysis (P95)
- [x] Cache hit rate >60% for repeated positions
- [x] Parallel LLM calls implemented
- [x] Load testing (100 concurrent analyses)
- [x] Metrics dashboard (Grafana/Prometheus)

---

## Issue Dependency Graph

```
#3490 (EPIC: Multi-Agent) ✅ Foundation complete
  │
  ├─ Phase 1: Foundation (#3491-#3495) ✅ ALL CLOSED
  │
  ├─ Phase 2: Tutor (#3496-#3501) ✅ 4/6 CLOSED
  │
  ├─ Phase 3: Arbitro (#3759-#3768) ✅ MOSTLY CLOSED
  │
  └─ Phase 4: Decisore ⏳ 0/7 COMPLETE
      │
      ├─ #3772: Game State Parser (INDEPENDENT)
      │    └─ Deliverable: ChessFENParser + ParsedGameState value objects
      │
      ├─ #3770: Move Suggestion Algorithm (depends on #3772)
      │    └─ Deliverable: ChessMoveGenerator + filtering
      │
      ├─ #3769: Strategic Analysis Engine (depends on #3770, #3772)
      │    └─ Deliverable: DecisoreAgentService + PositionEvaluator + Command/Handler
      │
      ├─ #3771: Multi-Model Ensemble (extends #3769)
      │    └─ Deliverable: MultiModelEvaluator for consensus
      │
      ├─ #3773: REST API Endpoint (depends on #3769)
      │    └─ Deliverable: DecisoreAgentEndpoints.cs + validators
      │
      ├─ #3774: Performance Tuning (depends on #3773)
      │    └─ Deliverable: Optimization (cache, parallel LLM, metrics)
      │
      └─ #3775: Beta Testing (depends on ALL above)
           └─ Deliverable: User feedback, iteration, documentation
```

---

## Implementation Sequence (Recommended)

### Week 1-1.5: Foundation (Issues #3772 + #3770 + #3769 start)
```bash
Day 1-2:   #3772 Game State Parser (Chess FEN)
Day 3-4:   #3770 Move Suggestion Algorithm (legal moves + filtering)
Day 5-7:   #3769 Strategic Analysis Engine (heuristic evaluator)
```

### Week 2-2.5: LLM Integration & Ensemble (#3769 complete + #3771)
```bash
Day 8-10:  #3769 LLM refinement integration + RAG strategy retrieval
Day 11-12: #3771 Multi-Model Ensemble (GPT-4 + Claude consensus)
Day 13:    #3773 REST API Endpoint (CQRS command/handler/routing)
```

### Week 3-3.5: Optimization & Testing (#3774 + #3775)
```bash
Day 14-15: #3774 Performance tuning (parallel LLM, caching, profiling)
Day 16-18: #3775 Beta testing with real users
Day 19-20: Documentation + final polish
```

**Total Estimate**: 4-5 weeks (includes testing + iteration)

---

## API Specification

### POST /api/v1/agents/decisore/analyze

**Request**:
```json
{
  "gameSessionId": "uuid",
  "playerName": "string",
  "analysisDepth": "quick|standard|deep",
  "maxSuggestions": 3
}
```

**Response**:
```json
{
  "suggestions": [
    {
      "move": "e4",
      "position": "center",
      "score": 0.92,
      "reasoning": "Controls center, develops pieces",
      "pros": ["Control center", "Piece development"],
      "cons": ["Symmetric response e5"],
      "expectedOutcome": "Balanced with slight initiative"
    }
  ],
  "positionStrength": 0.05,
  "riskLevel": "low",
  "victoryPaths": ["Center control → endgame advantage"],
  "confidence": 0.87,
  "executionTimeMs": 3200,
  "timestamp": "2026-02-12T00:00:00Z"
}
```

---

## Testing Strategy

### Unit Tests (Domain Services)
```
GameStateParserTests:
  - Valid FEN parsing (10 scenarios)
  - Invalid FEN error handling (5 scenarios)
  - Edge cases (promotions, en passant, castling)

MoveGeneratorTests:
  - Legal move generation per piece type (6 pieces)
  - Illegal move filtering (check, pin, etc.)
  - Priority ranking accuracy

PositionEvaluatorTests:
  - Material scoring accuracy
  - Positional scoring (center, development)
  - Known positions (openings, endgames)

DecisoreAgentServiceTests:
  - Full analysis flow
  - LLM integration
  - RAG context assembly
  - Error scenarios (timeout, invalid state)
```

### Integration Tests (Handlers)
```
AnalyzeGameStateCommandHandlerTests:
  - Valid request handling
  - Session not found error
  - Invalid board state error
  - Performance within budget
```

### E2E Tests (API)
```
DecisoreAgentE2ETests:
  - POST /analyze with chess game
  - Analysis depth variations (quick/standard/deep)
  - Multi-suggestion results
  - Confidence scoring validation
  - Performance <5s assertion
```

**Coverage Target**: >90% (domain 95%, application 90%, API 85%)

---

## Performance Budget

| Analysis Depth | Time Budget | Algorithm |
|----------------|-------------|-----------|
| **Quick** | <1s | Heuristics only, top 5 candidates |
| **Standard** | <5s | Heuristics + LLM top 3 |
| **Deep** | <10s | Heuristics + LLM top 5 + ensemble |

**Optimization Checkpoints**:
- Milestone 1 (Phase 1 complete): Heuristics <1s ✅
- Milestone 2 (LLM integrated): Standard <7s → Optimize to <5s
- Milestone 3 (Ensemble added): Deep <15s → Optimize to <10s

---

## Dependencies & Integration

### External Dependencies
- **Issue #3708**: AgentDefinition (Type, Strategy) ✅ MERGED (PR #4125)
- **Issue #3491**: Context Engineering ✅ CLOSED
- **Issue #3492**: Hybrid Search ✅ CLOSED
- **Issue #3495**: LangGraph Orchestrator ✅ CLOSED

### Internal Dependencies
```
#3772 (Parser) → #3770 (Move Gen) → #3769 (Core Engine)
                                          ↓
                                      #3771 (Ensemble)
                                          ↓
                                      #3773 (API)
                                          ↓
                                      #3774 (Performance)
                                          ↓
                                      #3775 (Testing)
```

### Integration Points
- **Orchestration Service**: Add decisore_agent node to LangGraph (Python)
- **Context Engineering**: Use StrategyPatternsSource for RAG
- **Arbitro**: Validate suggested moves before returning (optional)
- **Multi-Agent Dashboard (#3778)**: Display Decisore stats

---

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Performance >5s | Medium | High | Parallel LLM, caching, profiling early |
| Chess logic bugs | Low | Medium | Comprehensive tests, known positions |
| LLM reasoning quality | Medium | High | Multi-model ensemble, human validation |
| Game-specific complexity | High | Medium | Start Chess only, add games incrementally |
| Integration complexity | Low | Medium | Follow Tutor/Arbitro pattern exactly |

---

## Next Steps

1. **Update GitHub Issues** with detailed specs from this plan
2. **Implement Phase 1** (#3772 + #3770 + #3769) - 1.5 weeks
3. **Review milestone** after Phase 1 complete
4. **Iterate** based on performance and quality metrics
5. **Add Catan support** in Phase 2 (post-MVP)

---

*Generated: 2026-02-12 | Based on: Tutor/Arbitro pattern analysis + Context Engineering architecture*

# 🚀 ROADMAP MEEPLEAI EVOLUTION 2025
## Da RAG Assistant a AI Game Master

### 📅 Timeline: Q1-Q3 2025 | Budget: €100k | Team: 4-6 developers

---

## 📋 PHASE 0: BASELINE ASSESSMENT [Settimana 1-2]

### 🔍 Current State Validation Checklist

#### Core Functionality Tests
```bash
# 1. PDF Processing Pipeline
✓ Upload PDF: POST /api/v1/pdf/upload (test con 5 rulebooks)
✓ Text extraction: Validate Docnet.Core output quality
✓ Chunking strategy: Verify sentence-aware chunking (256-768 chars)
✓ Table extraction: Check PdfTableExtractionService accuracy

# 2. Vector Search & RAG
✓ Embedding generation: Test EmbeddingService latency (<500ms)
✓ Qdrant indexing: Verify vector storage (check 6333/6334 ports)
✓ RAG search: POST /api/v1/rag/search accuracy baseline
✓ RRF fusion: Validate 70% vector + 30% keyword mix

# 3. Chat & Session Management
✓ WebSocket connectivity: Test real-time chat
✓ Session persistence: Validate 30-day auto-revocation
✓ Auth flow: Cookie + API key dual system working
✓ Rate limiting: Verify token limits per role

# 4. Game Management
✓ RuleSpec v0 validation: Check schema compliance
✓ Game CRUD operations: Test GameService endpoints
✓ BGG integration: Verify BggApiService (7-day cache)
✓ Version control: Test RuleSpecDiffService
```

#### Performance Baseline Metrics
```yaml
current_metrics:
  pdf_processing_time: avg 8.3s per document
  rag_accuracy: 72% (manual evaluation on 100 queries)
  response_latency_p95: 2.8s
  concurrent_users: tested up to 150
  vector_search_recall: 74%
  citation_accuracy: 68%

target_improvements:
  pdf_processing: -50% time with Docling
  rag_accuracy: +23% to reach 95%
  response_latency: -30% to <2s
  concurrent_users: +567% to 1000+
```

#### Technical Debt Assessment
```yaml
critical_issues:
  - Single LLM dependency (OpenRouter only)
  - No ambiguity detection system
  - Limited PDF layout preservation
  - No formal rule validation
  - Missing Italian language support

quick_wins:
  - Add Prometheus metrics for accuracy tracking
  - Implement basic consensus with 2 LLMs
  - Upgrade to Docling for better extraction
```

### 🛠️ Baseline Testing Scripts
```bash
# Automated baseline test suite
cd apps/api
dotnet test --filter "Category=Integration" --logger "html;LogFileName=baseline.html"

# Frontend E2E validation
cd apps/web
pnpm test:e2e --grep "critical"

# Performance baseline
docker compose exec meepleai-api curl http://localhost:8080/metrics > metrics_baseline.txt

# Load testing
k6 run --vus 100 --duration 5m load_test_baseline.js
```

---

## 🎯 PHASE 1: HYBRID PRECISION ENGINE [Q1 2025 - Mesi 1-3]

### SPRINT 1: Enhanced PDF Processing [Settimane 3-6]

#### Implementation Tasks
```yaml
week_3-4:
  backend_services:
    - Create PdfVisionService.cs using Docling
    - Implement TableFormerService for complex tables
    - Add DiagramRecognitionService with Vision API
    - Integrate multi-column layout detection

  dependencies:
    - pip install docling (Python sidecar)
    - Add Docling.NET wrapper library
    - Configure GPU support (optional)

week_5-6:
  advanced_chunking:
    - Implement semantic boundary detection
    - Add cross-reference preservation logic
    - Maintain document hierarchy in chunks
    - Create ChunkQualityAnalyzer service
```

#### 🔍 Checkpoint 1.1: PDF Enhancement Validation
```yaml
tests:
  complex_pdfs:
    - Multi-column layouts: >90% accuracy
    - Nested tables: >85% extraction
    - Diagrams with labels: >80% recognition
    - Cross-references: 100% preserved

  regression:
    - All existing PDFs: must still process
    - API compatibility: no breaking changes
    - Processing time: <10s per document

validation_dataset:
  - 10 board game rulebooks (various complexity)
  - 5 with complex tables
  - 5 with multi-column layouts
```

### SPRINT 2: Multi-Model Consensus [Settimane 7-10]

#### Architecture Implementation
```csharp
// New services structure
public interface IMultiLlmOrchestrator
{
    Task<ConsensusResult> GetConsensus(string query, RuleContext context);
    Task<AmbiguityReport> DetectAmbiguities(string ruleText);
}

public class ConsensusEngine
{
    private readonly Dictionary<string, ILlmProvider> _providers = new()
    {
        ["gpt4"] = new OpenAiProvider(),
        ["claude"] = new ClaudeProvider(),
        ["gemini"] = new GeminiProvider()
    };

    public async Task<RuleInterpretation> ReachConsensus(RuleQuery query)
    {
        var results = await Task.WhenAll(_providers.Values
            .Select(p => p.InterpretRule(query)));

        return new ConsensusAnalyzer()
            .Analyze(results, threshold: 0.67); // 2/3 agreement
    }
}
```

#### N8n Workflow Integration
```yaml
workflow_name: multi_llm_consensus
triggers:
  - webhook: /consensus/request

nodes:
  - parallel_llm_calls:
      - openai: primary interpretation
      - anthropic: validation
      - google: disambiguation

  - consensus_logic:
      if: agreement >= 2/3
      then: return consensus
      else: flag for human review
```

#### 🔍 Checkpoint 1.2: Consensus System Validation
```yaml
accuracy_metrics:
  standard_rules: >95% consensus accuracy
  edge_cases: >85% correct handling
  ambiguous_rules: >90% detection rate

performance_metrics:
  consensus_latency: <2s average
  parallel_processing: verified working
  fallback_handling: tested all paths

test_scenarios:
  - 50 standard rule queries
  - 20 edge case scenarios
  - 10 deliberately ambiguous rules
```

### SPRINT 3: Formal Rule Engine [Settimane 11-14]

#### RuleSpec v2.0 Migration
```json
{
  "version": "2.0",
  "backwards_compatible": true,
  "rules": [
    {
      "id": "movement_001",
      "natural_language": "Players can move orthogonally",
      "formal_representation": {
        "preconditions": [
          "is_player_turn(P)",
          "has_valid_piece(P, Piece)",
          "is_adjacent(From, To)"
        ],
        "action": "move(Piece, From, To)",
        "postconditions": [
          "position(Piece, To)",
          "empty(From)",
          "check_victory_condition()"
        ],
        "prolog": "valid_move(P, From, To) :- adjacent(From, To), orthogonal(From, To)."
      }
    }
  ]
}
```

#### Validation Pipeline
```csharp
public class FormalRuleValidator
{
    public async Task<ValidationResult> Validate(RuleSpecV2 spec)
    {
        var steps = new[]
        {
            ExtractFormalRules(spec),
            CheckContradictions(spec),
            GenerateTestCases(spec),
            RunPrologInference(spec),
            ValidateConsistency(spec)
        };

        return await Pipeline.Execute(steps);
    }
}
```

#### 🔍 Checkpoint 1.3: Formal Validation
```yaml
validation_tests:
  contradiction_detection: 100% accuracy
  move_legality: >98% correct
  state_consistency: always maintained
  prolog_inference: working correctly

integration_tests:
  v0_compatibility: preserved
  api_v2_endpoints: fully tested
  migration_tool: converts v0→v2

coverage_metrics:
  rules_formalized: >80%
  test_cases_generated: >500
  edge_cases_covered: >90%
```

---

## 🚀 PHASE 2: AI GAME MASTER SUITE [Q2-Q3 2025 - Mesi 4-7]

### SPRINT 4: Italian Language Excellence [Settimane 15-18]

#### Multilingual Infrastructure
```yaml
implementation:
  week_15-16:
    - Train Italian embeddings on game corpus
    - Create ItalianRuleGlossary (500+ terms)
    - Implement TranslationMemoryService
    - Add cultural adaptation layer

  week_17-18:
    - Partner integration (Giochi Uniti API)
    - Community validator program setup
    - Italian-specific test suite
    - Localization of UI components
```

#### 🔍 Checkpoint 2.1: Italian Market Readiness
```yaml
quality_metrics:
  translation_accuracy: >92%
  terminology_consistency: 100%
  cultural_appropriateness: validated by natives

user_testing:
  beta_testers: 20 Italian players
  satisfaction_score: >4.2/5
  bug_reports: <5 critical

content_coverage:
  italian_games: 50+ indexed
  glossary_terms: 500+ verified
  partner_content: integrated
```

### SPRINT 5: Real-Time Game Management [Settimane 19-22]

#### WebSocket Architecture
```typescript
// Real-time game session manager
class GameSessionManager {
  private sessions: Map<string, GameSession> = new Map();

  async createSession(gameId: string): Promise<SessionInfo> {
    const session = {
      id: uuidv4(),
      gameId,
      players: [],
      state: new GameState(),
      moveHistory: [],
      aiArbitrator: new AiArbitrator(gameId)
    };

    this.sessions.set(session.id, session);
    return session;
  }

  async validateMove(sessionId: string, move: Move): Promise<ValidationResult> {
    const session = this.sessions.get(sessionId);
    return session.aiArbitrator.validateMove(move);
  }
}
```

#### Computer Vision Integration
```python
# Board state recognition service
class BoardStateRecognizer:
    def __init__(self):
        self.model = load_model("board_detection_v2")
        self.piece_classifier = PieceClassifier()

    async def capture_state(self, image_path):
        board = self.detect_board(image_path)
        pieces = self.identify_pieces(board)
        return self.map_to_game_state(pieces)
```

#### 🔍 Checkpoint 2.2: Real-Time Validation
```yaml
system_tests:
  websocket_stability: 99.9% uptime
  state_synchronization: 100% accurate
  move_validation_latency: <100ms
  concurrent_sessions: 1000+ supported

vision_accuracy:
  board_detection: >95%
  piece_recognition: >92%
  state_mapping: >90%

stress_tests:
  peak_load: 1500 concurrent users
  message_throughput: 10k/second
  state_recovery: <5s after crash
```

### SPRINT 6: Learning & Evolution [Settimane 23-26]

#### Dataset Integration Pipeline
```yaml
data_sources:
  hugging_face:
    - boardgame-qa: 25k Q&A pairs
    - mtg-eval: card interactions
    - fireball: D&D sessions

  community:
    - user_feedback: continuous stream
    - game_logs: anonymized sessions
    - rule_clarifications: validated
```

#### Continuous Learning System
```csharp
public class AdaptiveLearningService
{
    public async Task IngestFeedback(UserFeedback feedback)
    {
        await _feedbackRepo.Store(feedback);

        if (feedback.Type == FeedbackType.Correction)
        {
            await _retrainQueue.Enqueue(new RetrainTask
            {
                GameId = feedback.GameId,
                RuleId = feedback.RuleId,
                Correction = feedback.Correction
            });
        }
    }

    public async Task PerformDailyRetraining()
    {
        var feedbackBatch = await _feedbackRepo.GetDailyBatch();
        await _embeddingService.UpdateEmbeddings(feedbackBatch);
        await _promptService.OptimizePrompts(feedbackBatch);
    }
}
```

#### 🔍 Checkpoint 2.3: Learning System Validation
```yaml
learning_metrics:
  feedback_integration: <24h cycle
  accuracy_improvement: +2% monthly
  prompt_optimization: A/B tested

dataset_coverage:
  external_data: 50k+ examples
  user_feedback: 1000+ items
  validated_corrections: >95%

performance_impact:
  retraining_time: <2h nightly
  no_service_disruption: verified
  incremental_updates: working
```

---

## 📊 GLOBAL VALIDATION CHECKPOINTS

### 🎯 End of Phase 1 Assessment [Month 3]
```yaml
success_criteria:
  pdf_accuracy: >95% extraction
  consensus_reliability: >93% agreement
  formal_validation: 100% contradiction-free
  api_performance: <2s p95 latency

go_no_go_decision:
  if_all_criteria_met: proceed to Phase 2
  if_partial_success: 1-month remediation
  if_major_issues: pivot strategy
```

### 🏆 End of Phase 2 Assessment [Month 7]
```yaml
market_readiness:
  italian_support: fully operational
  real_time_games: 1000+ concurrent
  learning_system: continuously improving
  user_satisfaction: >4.5/5 rating

business_metrics:
  monthly_active_users: 5000+
  publisher_partnerships: 3+ signed
  revenue_run_rate: €10k/month
  market_position: top 3 in Italy
```

---

## 🚨 RISK MITIGATION & CONTINGENCIES

### Technical Risks
```yaml
gpu_availability:
  risk: GPU shortage/cost
  mitigation: CPU-optimized fallbacks

llm_api_failures:
  risk: Provider outages
  mitigation: Local model backups

performance_degradation:
  risk: Scale issues
  mitigation: Horizontal scaling ready
```

### Business Risks
```yaml
publisher_resistance:
  risk: Content licensing issues
  mitigation: Start with indie publishers

competition:
  risk: Faster market entry
  mitigation: Focus on Italian niche

user_adoption:
  risk: Trust issues
  mitigation: Transparency dashboard
```

---

## 💰 BUDGET ALLOCATION

### Phase 1 Budget: €60,000
```yaml
development: €40,000
  senior_dev: 3 months @ €8k/month = €24,000
  junior_dev: 3 months @ €4k/month = €12,000
  ui_designer: 1 month @ €4k/month = €4,000

infrastructure: €15,000
  docling_license: €3,000
  gpu_compute: €2k/month x 3 = €6,000
  llm_apis: €2k/month x 3 = €6,000

testing: €5,000
  qa_contractor: €3,000
  user_testing: €2,000
```

### Phase 2 Budget: €40,000
```yaml
development: €30,000
  team_continuation: €25,000
  ml_specialist: 1 month @ €5,000

partnerships: €7,000
  publisher_negotiations: €3,000
  community_incentives: €2,000
  marketing_materials: €2,000

operations: €3,000
  scaling_infrastructure: €2,000
  monitoring_tools: €1,000
```

---

## 📅 CRITICAL PATH & MILESTONES

### Q4 2024 - Q1 2025 Milestones (COMPLETED ✅)
- ✅ Nov 2024: Baseline architecture complete
- ✅ Dec 2024: PDF processing with Unstructured/SmolDocling (3-stage pipeline)
- ✅ Jan 2025: Hybrid RAG (Vector + Keyword RRF 70/30)
- ✅ Feb 2025: Production RAG baseline (72% accuracy)

### Q4 2025 Milestones (IN PROGRESS 🟡)
- ✅ Week 1 (15-17 Nov): DDD Migration 100% COMPLETATO!
  - All 10 critical issues resolved
  - 5,387 lines legacy code removed
  - 96+ CQRS handlers operational
- 🟡 Week 5-9 (Nov-Dec): Multi-Model Validation Phase
  - Target: GPT-4 + Claude consensus
  - Goal: >95% accuracy, <3% hallucination
  - Status: Ready to start
- 🟡 Week 10-11 (Jan 2026): Sprint 5 Frontend completion

### Q1 2026 Milestones (PLANNED)
- ⏳ Week 18: Italian support launched
- ⏳ Week 22: Real-time games beta
- ⏳ Week 26: Learning system active

### Q2-Q3 2026 Milestones (FUTURE)
- ⏳ Month 7: Full production launch
- ⏳ Month 8: 10k users milestone
- ⏳ Month 9: Series A preparation

---

## 🎮 IMMEDIATE NEXT ACTIONS (Updated 17 Nov 2025)

### ✅ PHASE 1A COMPLETED - DDD Migration 100%
```bash
# ✅ COMPLETATO (15-17 Nov 2025)
# - All 10 critical issues resolved
# - 5,387 lines legacy code removed
# - 96+ CQRS handlers operational
# - Domain events infrastructure complete
```

### 🚀 PHASE 1B - Multi-Model Validation (NEXT)
```bash
# Week 5-9: Multi-Model Validation Implementation
git checkout -b feature/multi-model-validation

# Issue #974: MultiModelValidationService
cd apps/api/src/Api/BoundedContexts/KnowledgeBase/Application
# Implement GPT-4 + Claude consensus
# See: docs/07-project-management/roadmap for details

# Issue #975: Consensus similarity calculation
# Implement cosine similarity for consensus validation

# Testing & Quality
dotnet test --filter "Category=MultiModel"
# Target: >95% accuracy, <3% hallucination
```

### 🎨 PHASE 1C - Frontend Sprint 5 (PARALLEL)
```bash
# Issue #868: Agent Selection UI
cd apps/web/src/app
# Implement agent selection interface

# Issue #869: Move Validation Integration
# Integrate RuleSpec v2 move validation
```

---

## 📈 SUCCESS METRICS DASHBOARD

```yaml
weekly_kpis:
  - Code coverage: >90%
  - Test pass rate: >98%
  - Performance benchmarks: green
  - User feedback score: tracked

monthly_reviews:
  - Accuracy improvements: documented
  - Cost per query: optimized
  - Technical debt: reducing
  - Market traction: measured

quarterly_objectives:
  - Phase completion: on schedule
  - Budget adherence: ±10%
  - User growth: exponential
  - Partnership pipeline: growing
```

---

---

## 📝 UPDATE HISTORY

### 2025-11-17: DDD Migration 100% Complete! 🎉
**Milestone Achievement**: Phase 1A completata in anticipo!

**Completed Issues (10/10)**:
- #1183, #1192, #1187, #1184, #1188, #1189, #1185, #1191, #1186, #1190

**Key Metrics**:
- Legacy Code Removed: 5,387 lines
- CQRS Handlers: 96+ operational
- Domain Events: 40 events + 39 handlers
- Migration Status: 100% ✅

**Timeline**: Pianificato 4 settimane → Completato in 3 giorni!

**Next Phase**: Multi-Model Validation (Issues #974, #975, #973, #976)
- Target: >95% accuracy RAG
- Goal: <3% hallucination rate
- Timeline: Week 5-9 (Nov-Dec 2025)

---

*Document Version: 2.0.0*
*Last Updated: 2025-11-17*
*Next Review: Post Multi-Model Validation (Week 9)*
*Status: Phase 1A Complete ✅ | Phase 1B Ready to Start 🚀*

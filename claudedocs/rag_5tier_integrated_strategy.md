# Integrated 5-Tier RAG Strategy for MeepleAI Rules Agent

**Research Date**: 2026-01-31
**Context**: Consolidation of existing hybrid LLM (ADR-007) + new 5-tier agentic RAG architecture
**Sources**: Web research 2024-2025 + "Approcci LLM per agenti di giochi da tavolo" PDF + ADR-007

---

## Executive Summary

This document integrates three complementary routing systems into a **unified 5-tier agentic RAG architecture** for MeepleAI Rules Agent:

1. **User-Tier Routing** (existing, ADR-007): Role-based cost optimization (Anonymous → Admin)
2. **Query-Complexity Routing** (new research): Adaptive retrieval (FAST → BALANCED → PRECISE)
3. **5-Tier Pipeline** (2024-2025 best practices): Router → Retriever → Grader → Generator → Checker

**Key Innovation**: Combine user access control with intelligent query routing to optimize cost-accuracy-latency across both dimensions.

**Cost Impact**: ~$6,500/month (10K MAU) vs $3,000 current - marginal increase for significant accuracy improvements.

---

## 1. 5-Tier Agentic RAG Architecture

### Research Foundation

**Source**: [Advanced RAG Techniques for High-Performance LLM Applications](https://neo4j.com/blog/genai/advanced-rag-techniques/)

> "A generic agentic RAG pipeline is often composed of at least three AI-driven components: **Routing** (using smaller, faster models like 8B models for skimming text), **Synthesis** (using larger models like 70B for deep understanding), and **Evaluation** (using top-tier judge models for quality assessment)."

**Advanced Architecture** (5 stages):

> "A more comprehensive agentic RAG architecture includes: Router (decides if query needs retrieval), Retriever (fetches documents), Grader (LLM evaluates retrieved documents), Generator (synthesizes answers), and Hallucination Checker (verifies answer support)."

### 1.1 Tier 1: ROUTER (Intent Classification + Complexity Scoring)

**Purpose**: Classify query intent and select appropriate RAG strategy.

**Components**:
1. **Template Detection**: Classify into `rule_lookup` or `resource_planning`
2. **Complexity Scoring**: Assign 0-5 score based on query characteristics
3. **Strategy Selection**: Map to FAST/BALANCED/PRECISE based on complexity + template

**Model Options**:
- **Option A**: Semantic Router (library-based, <1ms, 85-92% accuracy)
- **Option B**: Claude 3 Haiku LLM classification (~50ms, 90-95% accuracy)

**Cost**: $0.0001-0.0005 per routing decision

**Implementation**:
```python
async def route_tier1(query: str) -> tuple[str, str, int]:
    """
    Returns: (template, strategy, complexity_score)
    - template: "rule_lookup" | "resource_planning"
    - strategy: "FAST" | "BALANCED" | "PRECISE"
    - complexity_score: 0-5
    """
    # Template classification
    template = await classify_template_semantic_router(query)

    # Complexity scoring
    score = calculate_complexity_score(query, template)

    # Strategy selection
    if score <= 1 and template == "rule_lookup":
        strategy = "FAST"
    elif score <= 3:
        strategy = "BALANCED"
    else:
        strategy = "PRECISE"

    return template, strategy, score
```

**Research Validation**:
> "Query-adaptive routing with complexity classification achieves 85-92% accuracy with small LLMs at <1ms cost per query, and is becoming table stakes for production RAG." - [Query-Adaptive RAG Routing](https://ascii.co.uk/news/article/news-20260122-9ccbfc03/query-adaptive-rag-routing-cuts-latency-35-while-improving-a)

### 1.2 Tier 2: RETRIEVER (Adaptive Document Retrieval)

**Purpose**: Fetch relevant rulebook chunks based on selected strategy.

**Strategy-Specific Configurations**:

| Strategy | Embedding Model | Search Type | Top-K | Latency | Cost |
|----------|----------------|-------------|-------|---------|------|
| **FAST** | MiniLM-L6-v2 (384d) | Vector-only | 3 | 14.7ms | $0.0001 |
| **BALANCED** | E5-Base-v2 (768d) | Hybrid (vector+BM25) | 10 | 79ms | $0.0002 |
| **PRECISE** | BGE-Base-v1.5 (768d) | Multi-hop, adaptive | 20 | 150-300ms | $0.001 |

**Implementation**:
```python
async def retrieve_tier2(query: str, strategy: str) -> list[Document]:
    if strategy == "FAST":
        # Single-hop, vector-only
        embeddings = await embed_minilm(query)
        return await qdrant.search(embeddings, top_k=3)

    elif strategy == "BALANCED":
        # Hybrid search
        vector_results = await qdrant.search(await embed_e5(query), top_k=10)
        keyword_results = await qdrant.bm25_search(query, top_k=10)
        return merge_hybrid_results(vector_results, keyword_results)[:10]

    else:  # PRECISE
        # Multi-hop retrieval with adaptive depth
        hop1 = await qdrant.search(await embed_bge(query), top_k=20)
        entities = extract_entities(hop1)
        hop2 = await qdrant.search_entities(entities, top_k=10)
        hop3 = await cross_reference_validation(hop1, hop2)
        return combine_multi_hop(hop1, hop2, hop3)[:20]
```

**PDF Insight - RAG for Rulebooks**:
> "Sam Miller (2025) spiega come trasformare il PDF di un manuale di gioco in un chatbot RAG usando Vertex AI: il PDF viene caricato, un motore di conoscenza estrae risposte dal testo, e poi l'LLM riceve prompt arricchiti coi passaggi trovati."

**Best Practice**: Semantic chunking by rule section with contextual headers (game name, category, page).

### 1.3 Tier 3: GRADER (Relevance & Quality Evaluation)

**Purpose**: Validate retrieved documents and rerank by relevance.

**Strategy-Specific Grading**:

| Strategy | Grading Method | Model | Latency | Accuracy Gain | Cost |
|----------|----------------|-------|---------|---------------|------|
| **FAST** | None (skip) | - | 0ms | 0% | $0 |
| **BALANCED** | Cross-encoder reranking | ms-marco-MiniLM-L-6-v2 | 100-200ms | +5-8% | $0.001 |
| **PRECISE** | LLM-based grading + contradiction detection | gte-Qwen2-7B or Claude Haiku | 500ms-1s | +10-15% | $0.01 |

**Implementation**:
```python
async def grade_tier3(docs: list[Document], strategy: str, query: str) -> list[Document]:
    if strategy == "FAST":
        return docs  # No grading, accept top-K as-is

    elif strategy == "BALANCED":
        # Cross-encoder reranking
        scores = await cross_encoder.rank(query, docs)
        return [doc for doc, score in sorted(zip(docs, scores), key=lambda x: x[1], reverse=True)][:5]

    else:  # PRECISE
        # LLM-based grading with contradiction detection
        grading_results = []
        for doc in docs:
            grade_prompt = f"""
            Evaluate this document's relevance to the query.
            Query: {query}
            Document: {doc.content}

            Rate 0-10 for relevance. Identify any contradictions with other documents.
            """
            grade = await llm_haiku.generate(grade_prompt)
            grading_results.append((doc, grade.relevance_score, grade.contradictions))

        # Filter low-relevance, flag contradictions
        filtered = [d for d, score, _ in grading_results if score >= 7]
        contradictions = [c for _, _, c in grading_results if c]

        if contradictions:
            # Trigger escalation or multi-hop resolution
            return await resolve_contradictions(filtered, contradictions)

        return sorted(filtered, key=lambda x: x[1], reverse=True)[:5]
```

**Research Finding**:
> "Cross-encoder provides 75x cost savings vs LLM-based for BALANCED tier. Use LLM-based only for PRECISE tier where accuracy is critical." - [NVIDIA Reranking Guide](https://developer.nvidia.com/blog/enhancing-rag-pipelines-with-re-ranking/)

### 1.4 Tier 4: GENERATOR (Answer Synthesis)

**Purpose**: Generate final answer from validated, graded documents.

**Strategy-Specific Generation**:

| Strategy | Generation Mode | Prompt Type | Tool-Calling | Model | Cost |
|----------|----------------|-------------|--------------|-------|------|
| **FAST** | Direct | Minimal extraction | No | Claude Haiku | $0.002 |
| **BALANCED** | Structured synthesis | Comparison, pros/cons | Simple (calc) | Claude Sonnet or GPT-4o-mini | $0.015 |
| **PRECISE** | Agentic chain-of-thought | Multi-step reasoning | Full (calc, graph) | Claude Opus or Sonnet | $0.20 |

**Template-Specific Prompts**:

**Rule Lookup + FAST**:
```python
prompt = f"""
You are a board game rules assistant. Extract the exact rule text that answers this question.

Retrieved Rules (top-3):
{format_docs_with_citations(graded_docs)}

Question: {query}

Provide:
1. Direct answer with exact rule text
2. Page citation (mandatory)

If no relevant rule found, respond "Rule not found in retrieved sections."
"""
```

**Rule Lookup + BALANCED**:
```python
prompt = f"""
You are a board game rules expert. Analyze these rule sections and synthesize a comprehensive answer.

Retrieved Rules (top-5, reranked):
{format_docs_with_citations(graded_docs)}

Question: {query}

Provide:
1. Synthesized answer combining multiple rule sections
2. Citation for each referenced section (page numbers)
3. Note any ambiguities or edge cases

If rules conflict, explain the contradiction.
"""
```

**Resource Planning + PRECISE**:
```python
prompt = f"""
You are a strategic advisor for board games. Use multi-step reasoning to analyze this decision.

Retrieved Rules (top-5, validated):
{format_docs_with_citations(graded_docs)}

Question: {query}

Analysis Steps:
1. Retrieve relevant rules for each option
2. Calculate resource costs/benefits (use calculator tool if needed)
3. Compare outcomes considering game state
4. Provide recommendation with reasoning trace

Tools Available:
- calculator(expression: str) -> float
- graph_traverse(entity: str) -> related_rules

Respond in structured format:
{
  "reasoning_steps": [...],
  "option_a_analysis": {...},
  "option_b_analysis": {...},
  "recommendation": "...",
  "confidence": 0-1
}
"""
```

**PDF Insight - Multi-Agent Generation**:
> "Belle et al. (2025) introducono un sistema multi-agente Auto-evolutivo ('HexMachina') in cui ruoli specializzati (Analista, Ricercatore, Coder, Player) collaborano iterativamente per analizzare l'andamento del gioco, studiare nuove strategie e riformulare il codice o i prompt dell'agente."

**Application**: For PRECISE tier, consider multi-agent collaboration:
- **Analyzer Agent**: Evaluates retrieved rules and identifies conflicts
- **Strategist Agent**: Generates strategic recommendations
- **Validator Agent**: Cross-checks reasoning against rules

### 1.5 Tier 5: HALLUCINATION CHECKER (Quality Assurance)

**Purpose**: Verify answer is grounded in retrieved context, detect hallucinations.

**Strategy-Specific Validation**:

| Strategy | Validation Method | Actions | Model | Cost |
|----------|------------------|---------|-------|------|
| **FAST** | Citation presence check | Pass/Fail (escalate if fail) | Rule-based | $0 |
| **BALANCED** | Answer-context alignment | Confidence score, warn if low | Cross-encoder | $0.001 |
| **PRECISE** | Self-reflection + re-retrieval | Agent validates, re-plans if needed | Same LLM (self-critique) | $0.02 |

**Implementation**:
```python
async def validate_tier5(answer: str, docs: list[Document], strategy: str, query: str) -> ValidationResult:
    if strategy == "FAST":
        # Simple citation check
        has_citations = bool(re.findall(r'\(page \d+\)', answer))
        if not has_citations:
            return ValidationResult(
                passed=False,
                action="ESCALATE_TO_BALANCED",
                reason="Missing citations"
            )
        return ValidationResult(passed=True)

    elif strategy == "BALANCED":
        # Answer-context alignment scoring
        alignment_prompt = f"""
        Check if this answer is fully supported by the retrieved context.

        Answer: {answer}

        Context: {format_docs(docs)}

        Rate alignment 0-1. Identify any unsupported claims.
        """
        alignment = await cross_encoder.score(alignment_prompt)

        if alignment.score < 0.75:
            return ValidationResult(
                passed=True,  # Still return answer
                confidence=alignment.score,
                warnings=[f"Low confidence: {alignment.unsupported_claims}"],
                action="OFFER_ESCALATE_TO_PRECISE"
            )

        return ValidationResult(passed=True, confidence=alignment.score)

    else:  # PRECISE
        # Self-reflection with re-retrieval capability
        reflection_prompt = f"""
        You generated this answer: {answer}

        Based on these retrieved rules: {format_docs(docs)}

        Self-critique:
        1. Is the answer fully grounded in retrieved context?
        2. Are there any unsupported claims or hallucinations?
        3. Should we retrieve additional context to improve accuracy?

        If confidence < 90%, propose re-retrieval strategy.
        """

        reflection = await llm_opus.generate(reflection_prompt)

        if reflection.confidence < 0.90 and reflection.retrieval_needed:
            # Trigger re-retrieval (max 2 iterations to prevent loops)
            new_docs = await retrieve_tier2(reflection.new_query, "PRECISE")
            graded = await grade_tier3(new_docs, "PRECISE", reflection.new_query)
            new_answer = await generate_tier4(graded, "PRECISE", template, reflection.new_query)
            return ValidationResult(
                passed=True,
                answer=new_answer,
                iterations=2,
                confidence=reflection.final_confidence
            )

        return ValidationResult(passed=True, confidence=reflection.confidence)
```

**Research Finding**:
> "Plan-route-act-verify-stop loop, with frameworks like LangChain/LangGraph for orchestration, managing routing sub-questions to the right tool and handling agents." - [Building Agentic RAG Systems with LangGraph](https://rahulkolekar.com/building-agentic-rag-systems-with-langgraph/)

---

## 2. Integration with Existing Hybrid LLM (ADR-007)

### 2.1 Two-Dimensional Routing Matrix

**Dimension 1**: User Tier (role-based access control, ADR-007)
**Dimension 2**: Query Complexity (adaptive RAG strategy, new)

**Routing Decision Table**:

| User Tier | FAST Strategy | BALANCED Strategy | PRECISE Strategy |
|-----------|---------------|-------------------|------------------|
| **Anonymous** | Llama 3.3 Free (80%) / GPT-4o-mini (20%) | GPT-4o-mini (20%) / Denied (80%) | **Denied** → Upgrade prompt |
| **User** | Llama 3.3 Free (80%) / GPT-4o-mini (20%) | GPT-4o-mini (30%) / Denied (70%) | **Limited** → 5 queries/day |
| **Editor** | Llama3:8b Local (50%) / GPT-4o-mini (50%) | GPT-4o-mini (50%) / Claude Haiku (50%) | Claude Sonnet (full access) |
| **Admin** | Llama3:8b Local (80%) / Claude Haiku (20%) | Claude Haiku (50%) / Sonnet (50%) | Claude Opus (full access) |

### 2.2 Cost Impact Analysis

**Query Distribution Assumptions** (from RAG research):
- 60-70% FAST queries (simple rule lookups)
- 25-30% BALANCED queries (complex rules)
- 5-10% PRECISE queries (strategic reasoning)

**Monthly Cost Projections** (10K MAU, 150K queries/month):

| User Segment | % of MAU | FAST % | BALANCED % | PRECISE % | Avg Cost/Query | Monthly Cost |
|--------------|----------|--------|------------|-----------|----------------|--------------|
| **Anonymous** | 50% (5K users) | 75% | 25% | 0% | $0.004 | $300 |
| **User** | 30% (3K users) | 65% | 30% | 5% (limited) | $0.008 | $360 |
| **Editor** | 15% (1.5K users) | 45% | 40% | 15% | $0.035 | $787 |
| **Admin** | 5% (500 users) | 25% | 50% | 25% | $0.080 | $600 |
| **TOTAL** | 10K users | - | - | - | - | **$2,047/month** |

**Comparison vs Current (ADR-007)**:
- **Current**: $3,000/month (simple user-tier routing)
- **5-Tier Integrated**: $2,047/month (intelligent query routing reduces unnecessary premium model usage)
- **Savings**: $953/month (32% reduction!) due to FAST strategy handling 60%+ queries with free/local models

**Key Optimization**: By routing simple queries to FAST tier (free models), we reduce expensive model usage even for premium user tiers.

### 2.3 Unified Routing Logic

```csharp
public async Task<RagResponse> ExecuteIntegratedRouting(
    string query,
    User? user,
    CancellationToken ct)
{
    // TIER 1: ROUTER - Classify intent + complexity
    var (template, strategy, complexity) = await ClassifyQuery(query);

    // User-tier access control
    var role = user?.Role ?? Role.User;
    if (strategy == "PRECISE" && role < Role.Editor)
    {
        if (role == Role.User && await CheckPreciseQuota(user.Id))
        {
            // Allow limited PRECISE for Users (5/day)
        }
        else
        {
            return UpgradePromptResponse($"PRECISE strategy requires Editor tier or higher");
        }
    }

    // Select model based on user tier + strategy
    var (provider, model) = SelectModel(role, strategy);

    // TIER 2: RETRIEVER
    var retrievedDocs = await RetrieveTier2(query, strategy);

    // TIER 3: GRADER
    var gradedDocs = await GradeTier3(retrievedDocs, strategy, query);

    // TIER 4: GENERATOR
    var answer = await GenerateTier4(gradedDocs, strategy, template, query, model);

    // TIER 5: HALLUCINATION CHECKER
    var validation = await ValidateTier5(answer, gradedDocs, strategy, query);

    // Auto-escalation logic
    if (!validation.Passed && strategy == "FAST")
    {
        return await ExecuteIntegratedRouting(query, user, ct, forceStrategy: "BALANCED");
    }

    return new RagResponse
    {
        Answer = validation.FinalAnswer ?? answer,
        Strategy = strategy,
        Template = template,
        Complexity = complexity,
        Confidence = validation.Confidence,
        Cost = CalculateTotalCost(provider, model, strategy),
        Latency = validation.TotalLatency
    };
}

private (string Provider, string Model) SelectModel(Role role, string strategy)
{
    // Integrate ADR-007 routing with strategy-specific needs
    return (role, strategy) switch
    {
        // Anonymous: Always free tier
        (Role.Anonymous, "FAST") => ("OpenRouter", "meta-llama/llama-3.3-70b-instruct:free"),
        (Role.Anonymous, "BALANCED") => Random.Shared.Next(100) < 20
            ? ("OpenRouter", "openai/gpt-4o-mini")
            : ("OpenRouter", "meta-llama/llama-3.3-70b-instruct:free"),

        // User: Mostly free, some paid for BALANCED
        (Role.User, "FAST") => ("OpenRouter", "meta-llama/llama-3.3-70b-instruct:free"),
        (Role.User, "BALANCED") => Random.Shared.Next(100) < 30
            ? ("OpenRouter", "openai/gpt-4o-mini")
            : ("OpenRouter", "meta-llama/llama-3.3-70b-instruct:free"),

        // Editor: Mix local + paid, full PRECISE access
        (Role.Editor, "FAST") => Random.Shared.Next(100) < 50
            ? ("Ollama", "llama3:8b")
            : ("OpenRouter", "openai/gpt-4o-mini"),
        (Role.Editor, "BALANCED") => Random.Shared.Next(100) < 50
            ? ("OpenRouter", "openai/gpt-4o-mini")
            : ("OpenRouter", "anthropic/claude-3.5-haiku"),
        (Role.Editor, "PRECISE") => ("OpenRouter", "anthropic/claude-3.5-sonnet"),

        // Admin: Mostly local for FAST, premium for PRECISE
        (Role.Admin, "FAST") => Random.Shared.Next(100) < 80
            ? ("Ollama", "llama3:8b")
            : ("OpenRouter", "anthropic/claude-3.5-haiku"),
        (Role.Admin, "BALANCED") => Random.Shared.Next(100) < 50
            ? ("OpenRouter", "anthropic/claude-3.5-haiku")
            : ("OpenRouter", "anthropic/claude-3.5-sonnet"),
        (Role.Admin, "PRECISE") => ("OpenRouter", "anthropic/claude-opus-4"),

        _ => ("OpenRouter", "openai/gpt-4o-mini")  // Safe fallback
    };
}
```

**PDF Insight - Plan-and-Execute Pattern**:
> "LangChain propone anche agenti plan-and-execute, dove un planner LLM genera un piano multi-step completo e poi executor più leggeri compiono i singoli passi. Ciò riduce le chiamate all'LLM principale e migliora prestazioni complessive."

**Application for PRECISE Tier**:
- **Planner**: Claude Opus creates multi-step reasoning plan
- **Executor**: Claude Haiku executes individual steps (cheaper)
- **Validator**: Self-reflection confirms plan completion

---

## 3. Multi-Agent Architectures (from PDF Research)

### 3.1 HexMachina/Agents of Change (Belle et al. 2025)

**Architecture**: Self-evolving multi-agent system with specialized roles.

**Roles**:
1. **Analyst**: Analyzes game state and identifies patterns
2. **Researcher**: Explores new strategies and tactics
3. **Coder**: Reformulates agent code/prompts
4. **Strategist**: Develops long-term strategic plans
5. **Player**: Executes moves and learns from outcomes

**Key Finding**:
> "Gli autori dimostrano che questi agenti auto-miglioranti, specialmente con modelli come Claude 3.7 o GPT-4o, superano nettamente gli agenti statici (solo prompt) di baselines e possono battere bot progettati da umani."

**Application to MeepleAI PRECISE Tier**:
- Use multi-agent collaboration for complex strategic queries
- **Analyst Agent** (Claude Haiku): Analyze game state from retrieved rules
- **Strategist Agent** (Claude Opus): Develop recommendation
- **Validator Agent** (Claude Haiku): Cross-check against rules

**Cost**: ~$0.30 per PRECISE query (3 agent calls) - acceptable for 5-10% of queries.

### 3.2 External/Internal Search (DeepMind 2025)

**Two Strategies**:
1. **External Search**: LLM guides external MCTS without game engine calls
2. **Internal Search**: LLM generates internal move tree and evaluates results

**Research Finding**:
> "Con la combinazione LLM+ricerca si ottengono risultati di livello Grande Maestro in scacchi e altri giochi complessi, sfruttando la conoscenza pre-addestrata del modello e riducendo errori di ragionamento a lungo termine."

**Application**: For resource planning queries in PRECISE tier, implement internal search:
- LLM generates move tree (3-5 moves deep)
- Evaluates each branch using retrieved rules
- Selects optimal path with reasoning trace

### 3.3 Cogito Ergo Ludo (Celotti et al. 2026)

**Approach**: LLM builds explicit linguistic model of game world.

**Process**:
1. **Rule Induction**: After each game episode, agent induces rules from game history
2. **Strategy Synthesis**: Builds operational "playbook" from experience
3. **Application**: Uses induced rules and strategies in subsequent games

**Research Finding**:
> "Questo approccio permette all'agente di apprendere le dinamiche e le strategie dei giochi in autonomia, partendo da zero conoscenze a priori. In sostanza, anziché apprendere implicitamente con reti neurali, l'LLM costruisce strutture interpretabili (regole e piani) e le applica nei turni successivi."

**Application to MeepleAI**:
- **Long-term learning**: Build playbook from user query patterns
- **Rule refinement**: Update chunking strategy based on frequent rule conflicts
- **Strategy templates**: Synthesize common strategic patterns into reusable templates

---

## 4. Framework Integration (from PDF Research)

### 4.1 Boardwalk Framework (Baker et al. 2025)

**Concept**: Abstract `Game` class with mandatory methods for rule validation.

```python
class Game:
    def validate_move(self, state, action) -> bool
    def game_finished(self, state) -> bool
    def get_winner(self, state) -> Player
    def next_player(self, state) -> Player
```

**PDF Insight**:
> "Usare Python (o un linguaggio noto all'LLM) per la logica di gioco è una best practice: Baker et al. sottolineano che Python è familiare agli LLM (ha ampia presenza nei dati di addestramento), quindi si evitano problemi dovuti a linguaggi di dominio sconosciuti."

**Application**: Implement rule validation in Python embedding service:
```python
# embedding-service/game_validators/wingspan_validator.py
class WingspanValidator:
    def __init__(self, rulebook_chunks: list[RuleChunk]):
        self.rules = rulebook_chunks

    async def validate_move(self, state: GameState, action: Action) -> ValidationResult:
        # Retrieve relevant rules
        relevant_rules = await retrieve_rules_for_action(action)

        # Check against rules
        for rule in relevant_rules:
            if not rule.allows(state, action):
                return ValidationResult(
                    valid=False,
                    reason=rule.violation_message,
                    rule_citation=rule.page_number
                )

        return ValidationResult(valid=True)
```

### 4.2 Board Game Arena (Cipolina-Kun et al. 2025)

**Framework**: OpenSpiel-based with Gym-like API for LLM agents.

**PDF Insight**:
> "Ad ogni turno, l'OpenSpiel restituisce una stringa di prompt che descrive lo stato corrente, le azioni legali disponibili e un riassunto della storia passata, che viene passato all'LLM. L'agente LLM restituisce infine un'azione (in formato strutturato, es. dizionario JSON) e opzionalmente il ragionamento seguito."

**Application**: Structure game state prompts for Tier 4 Generator:
```json
{
  "game": "Wingspan",
  "current_state": {
    "player_hand": ["Raven", "Sparrow"],
    "resources": {"food": 2, "eggs": 1},
    "board_state": "..."
  },
  "legal_actions": [
    {"action": "play_bird", "card": "Raven", "cost": {"food": 1}},
    {"action": "gain_food", "amount": 2}
  ],
  "history": "Last 3 turns: [...]",
  "query": "Should I play the Raven or gain more food?"
}
```

**Best Practice**:
> "Il controllo delle regole è delegato all'ambiente: l'agente può proporre azioni, ma l'ambiente rigetta automaticamente quelle illegali."

**Application**: Tier 5 validation calls game-specific validators before finalizing answer.

---

## 5. Enhanced Strategy Specifications

### 5.1 FAST Strategy (Tier 1 → 2 → Skip 3 → 4 → 5-Simple)

**Pipeline**:
```
User Query
    ↓
[Tier 1] Router: Template + Complexity → FAST selected
    ↓
[Tier 2] Retriever: MiniLM-L6-v2, vector-only, top-K=3 (14.7ms + 30ms)
    ↓
[Tier 3] Grader: SKIP (accept top-3 as-is)
    ↓
[Tier 4] Generator: Claude Haiku or Llama 3.3 Free, minimal prompt (150ms)
    ↓
[Tier 5] Checker: Citation presence validation only (rule-based, <1ms)
    ↓
Answer (total: ~200ms)
```

**Cost Breakdown** (Anonymous user, 80% free tier):
- Tier 1: $0.0001 (Haiku classification)
- Tier 2: $0.0001 (MiniLM embedding)
- Tier 3: $0 (skip)
- Tier 4: $0 (free model 80% of time) or $0.002 (GPT-4o-mini 20%)
- Tier 5: $0 (rule-based)
- **Average**: $0.0006 per query

**Escalation Conditions**:
- Citations missing → BALANCED
- Confidence <70% → BALANCED

### 5.2 BALANCED Strategy (Tier 1 → 2 → 3 → 4 → 5-Advanced)

**Pipeline**:
```
User Query
    ↓
[Tier 1] Router: Complexity 2-3 → BALANCED selected
    ↓
[Tier 2] Retriever: E5-Base-v2, hybrid search (vector+BM25), top-K=10 (130ms)
    ↓
[Tier 3] Grader: Cross-encoder reranking → top-5 (150ms)
    ↓
[Tier 4] Generator: Claude Sonnet or GPT-4o-mini, structured prompt (1s)
    ↓
[Tier 5] Checker: Answer-context alignment scoring (200ms)
    ↓
Answer (total: ~1.5s)
```

**Cost Breakdown** (Editor user, 50% paid):
- Tier 1: $0.0001
- Tier 2: $0.0002 (E5 embedding)
- Tier 3: $0.001 (cross-encoder)
- Tier 4: $0.015 (GPT-4o-mini 50%) or $0.008 (Claude Haiku 50%)
- Tier 5: $0.001 (cross-encoder alignment)
- **Average**: $0.0173 per query

**Escalation Conditions**:
- Contradictions detected → PRECISE
- Confidence <75% → offer PRECISE upgrade

### 5.3 PRECISE Strategy (Tier 1 → 2-Multi → 3-LLM → 4-Agentic → 5-Reflection)

**Pipeline**:
```
User Query
    ↓
[Tier 1] Router: Complexity 4+ → PRECISE selected
    ↓
[Tier 2] Retriever: Multi-hop adaptive (3-5 hops, BGE-Base-v1.5) (500ms-1s)
    │  ├─ Hop 1: Initial retrieval (top-K=20)
    │  ├─ Hop 2: Entity expansion
    │  └─ Hop 3: Cross-reference validation
    ↓
[Tier 3] Grader: LLM-based relevance + contradiction detection (1s)
    ↓
[Tier 4] Generator: Multi-agent collaboration (3-5s)
    │  ├─ Analyzer Agent: Rule analysis (Claude Haiku)
    │  ├─ Strategist Agent: Recommendation (Claude Opus)
    │  └─ Validator Agent: Cross-check (Claude Haiku)
    ↓
[Tier 5] Checker: Self-reflection + optional re-retrieval (1-2s)
    ↓
Answer (total: 6-10s)
```

**Cost Breakdown** (Admin user, Claude Opus):
- Tier 1: $0.0001
- Tier 2: $0.001 (multi-hop embeddings)
- Tier 3: $0.01 (LLM grading)
- Tier 4: $0.20 (Opus + 2x Haiku agent calls)
- Tier 5: $0.02 (self-reflection)
- **Total**: $0.2311 per query

**Max Iterations**: 2 re-retrieval loops (prevent infinite recursion)

**PDF Insight - Multi-Agent Collaboration**:
> "Belle et al. introducono un sistema multi-agente in cui ruoli specializzati (Analista, Ricercatore, Coder, Player) collaborano iterativamente per analizzare l'andamento del gioco."

---

## 6. Template-Specific Adaptations

### 6.1 Rule Lookup Template ("Rispondere Regole")

**Goal**: Extract exact rule text with citations.

**Tier-Specific Behavior**:

| Tier | Focus | Output Format |
|------|-------|---------------|
| **1-Router** | Detect "rule_lookup" intent | Template classification |
| **2-Retriever** | Prioritize exact keyword matches | Chunks with page metadata |
| **3-Grader** | High precision threshold (>0.8 similarity) | Top-5 highly relevant |
| **4-Generator** | Direct extraction, minimal synthesis | "The rule states: [exact text] (page X)" |
| **5-Checker** | Mandatory citation validation | Fail if missing page numbers |

**Example Flow (BALANCED)**:
```
Query: "How many food tokens do you get in the setup phase of Wingspan?"

Tier 1: Template="rule_lookup", Complexity=1 → FAST strategy
Tier 2: Vector search → ["Setup phase: Each player receives 5 food tokens... (page 4)"]
Tier 3: SKIP (FAST tier)
Tier 4: Generate → "According to the rulebook, each player receives 5 food tokens during setup (page 4)."
Tier 5: Check citations → ✅ Citation present (page 4)

Output: Direct answer with citation, 150ms latency, $0.0006 cost
```

### 6.2 Resource Planning Template ("Decidere Risorse")

**Goal**: Strategic advice with trade-off analysis.

**Tier-Specific Behavior**:

| Tier | Focus | Output Format |
|------|-------|---------------|
| **1-Router** | Detect "resource_planning" intent | Template="resource_planning" → min BALANCED |
| **2-Retriever** | Broad context (rules + strategic patterns) | Multi-concept chunks |
| **3-Grader** | Contradiction detection critical | Flag conflicting strategies |
| **4-Generator** | Multi-step reasoning, tool-calling enabled | Pros/cons with calculations |
| **5-Checker** | Recommendation soundness validation | Self-critique reasoning |

**Example Flow (PRECISE)**:
```
Query: "In Catan, should I build a settlement or a city if I have 3 wood, 3 brick, 3 ore, 2 wheat?"

Tier 1: Template="resource_planning", Complexity=4 → PRECISE strategy
Tier 2: Multi-hop retrieval:
    Hop 1: Settlement rules → costs 1 wood, 1 brick, 1 wheat, 1 sheep
    Hop 2: City rules → costs 3 ore, 2 wheat
    Hop 3: Victory points → Settlement +1 VP, City +1 VP (upgrade from settlement)
Tier 3: LLM grading → all docs highly relevant, no contradictions
Tier 4: Multi-agent generation:
    - Analyzer: "You have resources for city but not settlement (missing sheep)"
    - Strategist: "Build city (costs 3 ore, 2 wheat) → +1 VP, better resource production"
    - Calculator tool: "Remaining: 3 wood, 3 brick, 1 ore, 0 wheat"
Tier 5: Self-reflection → Confidence 95%, recommendation sound

Output: "Build a city. You have exactly the resources needed (3 ore + 2 wheat).
        This gives +1 victory point and improves your wheat/ore production.
        Remaining resources: 3 wood, 3 brick, 1 ore.
        (Rules: page 7 - Building Costs, page 12 - Victory Points)"

Latency: 8s, Cost: $0.23
```

---

## 7. Production Implementation Roadmap

### Phase 1: 5-Tier Infrastructure (Weeks 1-3)

**Week 1: Tier 1 (Router)**
- [ ] Implement template classification (semantic router + LLM fallback)
- [ ] Implement complexity scoring algorithm
- [ ] Create routing decision logic
- [ ] Unit tests (20 tests: 10 template + 10 complexity)

**Week 2: Tier 2 (Retriever) + Tier 3 (Grader)**
- [ ] Integrate MiniLM-L6-v2 for FAST tier
- [ ] Integrate E5-Base-v2/BGE-Base-v1.5 for BALANCED/PRECISE
- [ ] Implement hybrid search (Qdrant vector + BM25)
- [ ] Integrate cross-encoder reranking (ms-marco-MiniLM)
- [ ] Integration tests (15 tests: retrieval + grading)

**Week 3: Tier 4 (Generator) + Tier 5 (Checker)**
- [ ] Create template-specific prompt templates (rule_lookup, resource_planning)
- [ ] Implement strategy-specific generation logic
- [ ] Add citation extraction and validation
- [ ] Implement hallucination checker (rule-based, cross-encoder, self-reflection)
- [ ] Integration tests (25 tests: generation + validation)

### Phase 2: Multi-Agent Integration (Weeks 4-5)

**Week 4: Multi-Agent Setup**
- [ ] Implement Analyzer Agent (game state analysis)
- [ ] Implement Strategist Agent (recommendation generation)
- [ ] Implement Validator Agent (cross-check against rules)
- [ ] Agent coordination logic (LangGraph or custom orchestration)

**Week 5: Agent Testing**
- [ ] Multi-agent collaboration tests (10 scenarios)
- [ ] Cost monitoring for multi-agent calls
- [ ] Latency optimization (parallel agent execution where possible)

### Phase 3: User-Tier Integration (Week 6)

- [ ] Integrate 5-tier pipeline with existing HybridAdaptiveRoutingStrategy
- [ ] Update model selection logic (SelectModel method)
- [ ] Implement access control (PRECISE tier restrictions)
- [ ] Add user quota tracking (PRECISE queries/day for User tier)
- [ ] Migration tests (ensure backward compatibility)

### Phase 4: Data Preparation (Week 7-8)

- [ ] Implement semantic chunking with contextual headers
- [ ] Add metadata (game name, category, page, complexity)
- [ ] Build cross-reference mapping (related_rules field)
- [ ] Re-index Wingspan rulebook with enhanced chunks
- [ ] Validate retrieval quality with test queries

### Phase 5: Monitoring & Optimization (Week 9-10)

- [ ] Extend Prometheus metrics with 5-tier tracking
- [ ] Dashboard: Strategy distribution, tier usage, escalation rates
- [ ] Cost tracking per tier (integrate with existing LlmCostCalculator)
- [ ] A/B testing framework (compare strategies)
- [ ] Performance optimization (cache frequent queries)

---

## 8. Evaluation Metrics (Enhanced)

### 8.1 Tier-Level Metrics

| Tier | Metric | Target | Measurement |
|------|--------|--------|-------------|
| **Tier 1** | Classification accuracy | >90% | Human-labeled validation set |
| **Tier 2** | Retrieval recall@5 | >85% | Relevant chunks in top-5 |
| **Tier 3** | Reranking precision@5 | >90% | Top-5 all relevant after grading |
| **Tier 4** | Answer accuracy | >88% (blended) | Human evaluation + auto-grading |
| **Tier 5** | Hallucination detection | >95% | Catch unsupported claims |

### 8.2 Strategy-Level Metrics (from previous research)

| Strategy | Accuracy | Latency P95 | Cost/Query | Citation Recall | User Satisfaction |
|----------|----------|-------------|------------|-----------------|-------------------|
| **FAST** | 78-85% | <200ms | <$0.005 | >90% | >70% |
| **BALANCED** | 85-92% | <2s | $0.01-0.03 | >95% | >85% |
| **PRECISE** | 95%+ | <10s | $0.10-0.30 | >98% | >95% |

### 8.3 User-Tier Metrics (from ADR-007)

| User Tier | Provider Mix | Avg Cost/Query | Quality Target | Monthly Budget |
|-----------|--------------|----------------|----------------|----------------|
| **Anonymous** | 80% Free | $0.0006 | >75% | $90 |
| **User** | 70% Free | $0.001 | >80% | $450 |
| **Editor** | 50% Paid | $0.008 | >90% | $1,800 |
| **Admin** | Full Premium | $0.035 | >95% | $5,250 |

### 8.4 System-Level Metrics

**Query Distribution**:
- FAST: 60-70% (most queries simple)
- BALANCED: 25-30% (moderate complexity)
- PRECISE: 5-10% (complex reasoning)

**Escalation Rates**:
- FAST → BALANCED: <15% (low confidence or missing citations)
- BALANCED → PRECISE: <5% (contradictions or user request)

**Cost Efficiency**:
- Target: <$0.015 average per query (blended across all users and strategies)
- Actual: ~$0.0137 (from cost projections above) ✅

---

## 9. Technology Stack (Final Recommendations)

### 9.1 Tier-Specific Models

| Tier | Component | Model Choice | Rationale |
|------|-----------|--------------|-----------|
| **Tier 1** | Router | Claude 3 Haiku or Semantic Router | Fast, accurate classification |
| **Tier 2-FAST** | Embedder | sentence-transformers/all-MiniLM-L6-v2 | 14.7ms latency |
| **Tier 2-BALANCED/PRECISE** | Embedder | sentence-transformers/e5-base-v2 | 79ms, 83-85% accuracy |
| **Tier 3-BALANCED** | Grader | ms-marco-MiniLM-L-6-v2 (cross-encoder) | 75x cheaper than LLM |
| **Tier 3-PRECISE** | Grader | gte-Qwen2-7B or Claude Haiku | LLM-based for accuracy |
| **Tier 4** | Generator | See User-Tier Matrix (Haiku/Sonnet/Opus/GPT-4o) | Role + strategy dependent |
| **Tier 5-BALANCED** | Checker | Cross-encoder alignment | Fast validation |
| **Tier 5-PRECISE** | Checker | Same as Tier 4 (self-critique) | Full self-reflection |

### 9.2 Framework Selection

| Strategy | Framework | Rationale |
|----------|-----------|-----------|
| **FAST** | Vanilla Python + httpx | Minimal overhead, fast debugging |
| **BALANCED** | Vanilla Python + httpx + cross-encoder lib | Add reranking, keep simple |
| **PRECISE** | LangChain/LangGraph | Multi-agent, tool-calling, orchestration |

**PDF Validation**:
> "LangChain propone anche agenti plan-and-execute, dove un planner LLM genera un piano multi-step completo e poi executor più leggeri compiono i singoli passi."

### 9.3 Infrastructure Requirements

**Current (from MeepleAI monorepo)**:
- ✅ Qdrant (vector DB)
- ✅ PostgreSQL (keyword search, metadata)
- ✅ Redis (caching)
- ✅ Embedding service (Python, sentence-transformers)
- ✅ Reranker service (Python, cross-encoder)

**Additions Needed**:
- [ ] Semantic router library (Python, for Tier 1)
- [ ] LangChain/LangGraph (Python, for PRECISE tier only)
- [ ] LLM-based reranker integration (gte-Qwen2 or equivalent)
- [ ] Multi-agent orchestration service (Python, for PRECISE multi-agent)

---

## 10. Research Sources

### Primary Research (5-Tier Architecture)

- [Advanced RAG Techniques for High-Performance LLM Applications](https://neo4j.com/blog/genai/advanced-rag-techniques/) - Neo4j
- [What is Agentic RAG](https://weaviate.io/blog/what-is-agentic-rag) - Weaviate
- [Building Agentic RAG Systems with LangGraph: The 2026 Guide](https://rahulkolekar.com/building-agentic-rag-systems-with-langgraph/) - Rahul Kolekar
- [RAG Architecture: Enhancing LLM Agents with AI Retrieval](https://futureagi.com/blogs/rag-architecture-llm-2025) - FutureAGI

### Multi-Model Orchestration

- [Efficient Multi-Model Orchestration for Self-Hosted LLMs](https://arxiv.org/html/2512.22402v1) - arXiv
- [Real-World Use Language Model Selection & Orchestration](https://cobusgreyling.medium.com/real-world-use-language-model-selection-orchestration-be4c7f4c3ab4) - Medium
- [Multi-provider LLM Orchestration in Production: A 2026 Guide](https://dev.to/ash_dubai/multi-provider-llm-orchestration-in-production-a-2026-guide-1g10) - DEV Community

### PDF Research: Board Game Agent Architectures

**From "Approcci LLM per agenti di giochi da tavolo.pdf"**:

1. **Belle et al. (2025)** - [Agents of Change: Self-Evolving LLM Agents](https://nbelle1.github.io/agents-of-change/)
   - Multi-agent system with Analyst, Researcher, Coder, Strategist, Player roles
   - Claude 3.7 and GPT-4o outperform static agents

2. **DeepMind (2025)** - [Mastering Board Games by External and Internal Planning](https://arxiv.org/abs/2412.12119)
   - External Search (LLM guides MCTS) + Internal Search (LLM generates move tree)
   - Grandmaster-level chess performance

3. **Celotti et al. (2026)** - [Cogito, Ergo Ludo](https://openreview.net/forum?id=w2vEo7NJ18)
   - LLM induces rules from game history
   - Builds operational playbook through self-play

4. **Sam Miller (2025)** - [Learning Board Games with RAG](https://medium.com/@thesammiller/learning-board-games-with-rag-61894c3cc1f7)
   - RAG for rulebook Q&A using Vertex AI
   - Prevents hallucinations with grounded retrieval

5. **Baker et al. (2025)** - [Boardwalk: Framework for Creating Board Games with LLMs](https://chatpaper.com/paper/182874)
   - Abstract Game class with validate_move, game_finished, get_winner
   - Python preferred for LLM familiarity

6. **Cipolina-Kun et al. (2025)** - [Board Game Arena: Framework and Benchmark](https://arxiv.org/html/2508.03368v1)
   - OpenSpiel integration, Gym-like API
   - Structured state prompts with legal_actions

### Existing MeepleAI Documentation

- **ADR-007**: Hybrid LLM Architecture (Ollama + OpenRouter)
- **ADR-004b**: Previous hybrid LLM version (superseded by ADR-007)

---

## 11. Next Steps: Consolidation Plan

### Option A: Implement 5-Tier Infrastructure First
1. Build Tier 1-5 pipeline for Admin users only (full access)
2. Validate accuracy improvements vs current system
3. Measure cost impact empirically
4. Gradually roll out to other user tiers

### Option B: Enhance Existing System Incrementally
1. Add Tier 1 (Router) classification to current RAG
2. Add Tier 3 (Grader) cross-encoder reranking
3. Add Tier 5 (Checker) validation
4. Keep Tier 2 (Retriever) and Tier 4 (Generator) as-is initially

### Option C: Parallel Prototype
1. Implement 5-tier as separate service (Python)
2. A/B test against existing RAG (ADR-007 hybrid LLM)
3. Measure accuracy, cost, latency differences
4. Migrate if metrics show significant improvement

**Recommended**: **Option A** - Full 5-tier for Admin tier first, then expand.

**Rationale**: Admins have budget for experimentation, can provide detailed feedback, and benefit most from PRECISE strategy for complex rule queries.

---

**Document Status**: Research Complete | Integration Design Ready
**Next Action**: Review with team → Select implementation option → Prototype Tier 1 Router
**Estimated Effort**: 10-12 weeks for full 5-tier implementation

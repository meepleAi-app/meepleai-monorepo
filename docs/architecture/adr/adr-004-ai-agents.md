# ADR-004: AI Agents Bounded Context Architecture

**Status**: Accepted
**Date**: 2025-11-12
**Deciders**: Engineering Lead, System Architect
**Context**: Sprint 5 - DDD Architecture Completion

---

## Context

MeepleAI requires AI agents for intelligent game rules interpretation, move validation, and conversational assistance. With the DDD migration now complete (100%), we must decide where AI agents belong in the bounded context architecture to unblock issues #866 (Agent Entity), #867 (Game Master Agent), and #869 (Move Validation).

**Problem**: Should AI agents be:
1. A new 8th bounded context ("AI Agents Context")?
2. Integrated within existing bounded contexts (primarily KnowledgeBase)?

**Current Architecture**: 11 bounded contexts
- Administration, Authentication, DocumentProcessing, GameManagement, KnowledgeBase, SessionTracking, SharedGameCatalog, SystemConfiguration, UserLibrary, UserNotifications, WorkflowIntegration

**KnowledgeBase Context** currently handles:
- RAG pipeline (hybrid vector + keyword search)
- Chat conversation management
- Vector search and embedding management
- Quality tracking and confidence scoring

**Research Foundation**:
- **Academic**: Nandi & Dey (2025) - "Designing Scalable Multi-Agent AI Systems with DDD" (IJCSE)
- **Industry**: Walmart's AI consolidation (WSJ 2025), Financial services multi-agent systems
- **DDD Patterns**: "Bounded Context as Workspace" vs "Agent as Bounded Context"
- **Anti-Patterns**: Shared Kernel, Context Proliferation, "Big Ball of Mud"

---

## Decision

**Extend the KnowledgeBase bounded context** to include AI agents rather than creating a separate "AI Agents" context.

### Architecture Components

```
┌─────────────────────────────────────────────────────────────┐
│              KnowledgeBase Bounded Context                  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Domain Layer:                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Aggregates                                           │  │
│  │  • Agent (NEW)          - AI agent lifecycle & state │  │
│  │  • ChatThread           - Conversation history       │  │
│  │  • VectorDocument       - Embedded knowledge         │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Domain Services                                      │  │
│  │  • AgentOrchestrationService (NEW) - Strategy routing│  │
│  │  • VectorSearchDomainService    - Similarity search │  │
│  │  • QualityTrackingDomainService - Confidence scoring│  │
│  │  • RrfFusionDomainService       - Hybrid search     │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Value Objects                                        │  │
│  │  • AgentType (NEW)      - Agent classification       │  │
│  │  • AgentStrategy (NEW)  - Execution strategy pattern│  │
│  │  • Confidence           - Score (0.0-1.0)            │  │
│  │  • Citation             - Source attribution        │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  Application Layer:                                         │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Commands/Queries                                     │  │
│  │  • InvokeAgentCommand (NEW) - Issue #867            │  │
│  │  • ConfigureAgentCommand (NEW) - Issue #866         │  │
│  │  • AskQuestionQuery     - Existing RAG query        │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
         │
         │ Publishes Domain Events
         ▼
    ┌────────────────────────────┐
    │ • AgentInvokedEvent        │
    │ • LowConfidenceDetectedEvent│
    │ • AgentConfiguredEvent     │
    └────────────────────────────┘
```

### Agent Classification

**Knowledge-Domain Agents** (in KnowledgeBase context):
- **RagAgent**: Hybrid search + LLM generation
- **CitationAgent**: Source validation and attribution
- **ConfidenceAgent**: Multi-layer quality assessment
- **RulesInterpreterAgent**: Game rules semantic search
- **ConversationAgent**: Chat thread management

**Game-Domain Agents** (in GameManagement context):
- **MoveValidationAgent**: Move legality checking (#869)
- **GameStateAgent**: State tracking and updates

### Pattern: "Bounded Context as Workspace"

AI agents are **specialized strategies within their domain context**, not separate infrastructure:
- Agents share ubiquitous language with their host context
- Agents collaborate using context-local domain services
- Agent state managed within context boundaries
- No artificial separation of "agent logic" from "domain logic"

---

## Rationale

### 1. Domain Cohesion (HIGH)

**Shared Business Capability**: Intelligent knowledge management
- RAG retrieval = knowledge access pattern
- Agent invocation = specialized knowledge access strategy
- Both serve: "answer game rules questions accurately"

**Shared Vocabulary** (Ubiquitous Language):
```yaml
Common_Terms:
  - Query, Confidence, Citation, Vector Similarity
  - Chunk, Retrieval Quality, Hybrid Search
  - RRF Fusion, Semantic Search
  - Rule Interpretation, Context Assembly
```

**Evidence from Blocked Issues**:
- #867 explicitly requires `InvokeAgentCommand` to use `VectorSearchDomainService` and `QualityTrackingDomainService`
- These are KnowledgeBase domain services, not external dependencies
- Agents orchestrate existing KnowledgeBase capabilities

### 2. Industry Pattern Alignment

**Pattern Match: "Bounded Context as Workspace"**

**Supply Chain Case Study** (Nandi & Dey, 2025):
```yaml
Supply_Chain_Context:
  Agents: [SupplyChainAgent, InventoryManagementAgent]
  Pattern: "Multiple specialized agents within single context"

Production_Context:
  Agents: [ProductionAgent]

Order_Fulfillment_Context:
  Agents: [OrderFulfillment, Logistics, Invoicing, Discount, Validation]
  Pattern: "5 specialized agents - not separate contexts"
```

**Financial Services Pattern**:
- Risk Assessment Context contained: MarketRiskAgent, CreditRiskAgent, OperationalRiskAgent
- All shared Risk domain services and vocabulary
- **Not** separate "AI Agents" context

**Walmart Anti-Pattern Avoidance**:
- Walmart initially: 30+ fragmented agents → user confusion
- Solution: Consolidated into 4 "super agents" by business domain
- **Lesson**: Group by business capability, not agent type

### 3. Anti-Pattern Avoidance

**❌ Shared Kernel Anti-Pattern** (if separate context):
```
AI Agents Context (separate)
    ├─ Depends on VectorSearchDomainService (KnowledgeBase)
    ├─ Depends on QualityTrackingDomainService (KnowledgeBase)
    └─ Cannot function without KnowledgeBase internals

Result: Shared Kernel - contexts not independently deployable
```

**❌ Context Proliferation**:
- 8th context adds coordination overhead
- No clear linguistic boundary justifies separation
- Risk of "Big Ball of Mud" from excessive fragmentation

**✅ Correct Pattern** (extend KnowledgeBase):
```
KnowledgeBase Context
    ├─ AgentOrchestrationService uses VectorSearchDomainService
    ├─ All services within same bounded context
    └─ Single transactional boundary, no cross-context calls
```

### 4. Technical Integration Simplicity

| Aspect | Separate Context | Extended KnowledgeBase |
|--------|------------------|------------------------|
| **Service Access** | Cross-context API calls | Direct domain service calls |
| **Transactions** | Distributed transactions | Local ACID transactions |
| **Error Handling** | Cross-boundary propagation | Standard exceptions |
| **Performance** | Network + serialization overhead | In-memory calls |
| **Testing** | Integration tests across contexts | Unit tests within context |

**Complexity Score**:
- Separate context: **HIGH** complexity
- Extended KnowledgeBase: **LOW** complexity

### 5. Scalability & Extensibility

**Future Agent Types**: FAQ bot, tutorial guide, game recommender, strategy analyzer

**Separate Context Challenge**:
- Each new agent: Add to "AI Agents" context?
- Risk: Context becomes grab-bag of unrelated agents
- Alternative: New context per type? → Context explosion

**Extended KnowledgeBase Approach**:
- Knowledge-domain agents: Add to KnowledgeBase
- Game-domain agents: Add to GameManagement (#869 follows this)
- Clear boundary: Agent serves the domain it enhances

---

## Consequences

### Positive

✅ **Low Integration Complexity**
- Single-context transactions for agent invocations
- Direct domain service reuse (VectorSearch, QualityTracking)
- No cross-context coordination overhead

✅ **High Domain Cohesion**
- Agents enhance KnowledgeBase capabilities naturally
- Shared ubiquitous language across RAG + agents
- Clear business capability: "intelligent knowledge management"

✅ **Industry Pattern Compliance**
- Matches successful multi-agent DDD implementations
- Avoids Walmart's fragmentation anti-pattern
- Aligns with academic research (Nandi & Dey, 2025)

✅ **Unblocks Downstream Issues**
- #866: Agent aggregate in KnowledgeBase/Domain
- #867: InvokeAgentCommand uses local domain services
- #869: Establishes "agent per context" pattern

✅ **Clear Extension Path**
- New knowledge agents: Add to KnowledgeBase
- New game agents: Add to GameManagement
- Domain-driven placement, not technical grouping

### Negative

⚠️ **KnowledgeBase Scope Expansion**
- Context grows from 3 to 4-5 domain services
- **Mitigation**: "Workspace pattern" explicitly allows this growth
- **Validation**: Supply chain case showed 5+ agents in single context successfully

⚠️ **Potential "Fat Context" Risk**
- **Monitoring criteria**: If agents start serving OTHER contexts (not knowledge), re-evaluate
- **Boundary rule**: Agents that orchestrate cross-context workflows → separate evaluation

### Trade-offs Accepted

**We accept**: Larger KnowledgeBase context (4-5 services)
**To gain**: Low complexity, high cohesion, pattern compliance

**We reject**: Artificial separation by technical type
**To avoid**: Shared Kernel anti-pattern, coordination overhead

---

## Implementation Plan

### Phase 1: Domain Model (Issue #866)

**Location**: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/`

```csharp
// Aggregates/Agent.cs
public class Agent : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public AgentType Type { get; private set; }
    public AgentStrategy Strategy { get; private set; }
    public bool IsActive { get; private set; }

    public void Configure(AgentStrategy strategy)
    {
        Strategy = strategy ?? throw new ArgumentNullException();
        AddDomainEvent(new AgentConfiguredEvent(Id));
    }

    public async Task<AgentResponse> InvokeAsync(
        AgentContext context,
        VectorSearchDomainService vectorSearch,
        QualityTrackingDomainService qualityTracking)
    {
        if (!IsActive)
            throw new DomainException("Agent not active");

        var searchResults = await vectorSearch.SearchAsync(
            context.QueryVector,
            context.CandidateEmbeddings,
            topK: 10,
            minScore: 0.70
        );

        var confidence = qualityTracking.AssessConfidence(searchResults);

        AddDomainEvent(new AgentInvokedEvent(Id, context.QueryId, confidence));

        return new AgentResponse(searchResults, confidence);
    }
}

// ValueObjects/AgentType.cs
public record AgentType
{
    public static AgentType RagAgent => new("RAG");
    public static AgentType CitationAgent => new("Citation");
    public static AgentType ConfidenceAgent => new("Confidence");
    public static AgentType RulesInterpreter => new("RulesInterpreter");

    public string Value { get; }
    private AgentType(string value) => Value = value;
}

// ValueObjects/AgentStrategy.cs
public record AgentStrategy
{
    public string Name { get; init; }
    public Dictionary<string, object> Parameters { get; init; }

    public static AgentStrategy HybridSearch(double vectorWeight = 0.7)
        => new()
        {
            Name = "HybridSearch",
            Parameters = new() { ["VectorWeight"] = vectorWeight }
        };
}
```

### Phase 2: Application Layer (Issue #867)

**Location**: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/`

```csharp
// Commands/InvokeAgentCommand.cs
public record InvokeAgentCommand(
    Guid AgentId,
    string Query,
    Guid? GameId,
    Guid? ChatThreadId
) : IRequest<AgentResponseDto>;

// Handlers/InvokeAgentCommandHandler.cs
public class InvokeAgentCommandHandler
    : IRequestHandler<InvokeAgentCommand, AgentResponseDto>
{
    private readonly IAgentRepository _agentRepo;
    private readonly VectorSearchDomainService _vectorSearch;
    private readonly QualityTrackingDomainService _qualityTracking;
    private readonly IEmbeddingService _embeddingService;

    public async Task<AgentResponseDto> Handle(
        InvokeAgentCommand request,
        CancellationToken ct)
    {
        var agent = await _agentRepo.GetByIdAsync(request.AgentId);

        var queryVector = await _embeddingService
            .GenerateEmbeddingAsync(request.Query);

        var candidates = await _agentRepo
            .GetCandidateEmbeddingsAsync(request.GameId);

        var context = new AgentContext(
            QueryId: Guid.NewGuid(),
            Query: request.Query,
            QueryVector: queryVector,
            CandidateEmbeddings: candidates
        );

        // Single-context transaction - no cross-boundary calls
        var response = await agent.InvokeAsync(
            context,
            _vectorSearch,
            _qualityTracking
        );

        return AgentResponseDto.FromDomain(response);
    }
}
```

### Phase 3: Domain Services

```csharp
// Services/AgentOrchestrationService.cs
public class AgentOrchestrationService : IDomainService
{
    public async Task<Agent> SelectAgentForQuery(
        string query,
        List<Agent> availableAgents)
    {
        // Strategy: Select based on query classification
        var queryType = ClassifyQuery(query);

        return queryType switch
        {
            QueryType.RulesInterpretation => availableAgents
                .FirstOrDefault(a => a.Type == AgentType.RulesInterpreter),
            QueryType.GeneralQuestion => availableAgents
                .FirstOrDefault(a => a.Type == AgentType.RagAgent),
            _ => availableAgents.First(a => a.IsActive)
        };
    }
}
```

### Phase 4: Event Integration

```csharp
// Events published by KnowledgeBase
public record AgentInvokedEvent(
    Guid AgentId,
    Guid QueryId,
    Confidence ConfidenceScore
) : IDomainEvent;

public record LowConfidenceDetectedEvent(
    Guid AgentId,
    Guid QueryId,
    Confidence Score
) : IDomainEvent;

// Consumed by Administration context for alerting
```

---

## Context Mapping

```
┌──────────────────────────────────────┐
│   KnowledgeBase Context              │
│   (RAG + Agents + Chat)              │
│                                      │
│   Pattern: Bounded Context as        │
│            Workspace                 │
└──────────────────────────────────────┘
            │
            │ Domain Events (Open-Host Service)
            │
   ┌────────┴─────────────────┐
   │                          │
   ▼                          ▼
┌────────────────┐    ┌─────────────────┐
│ GameManagement │    │ Administration  │
│                │    │                 │
│ Subscribes:    │    │ Subscribes:     │
│ • AgentInvoked │    │ • LowConfidence │
│   Event        │    │   (alerting)    │
│                │    │                 │
│ Contains:      │    │                 │
│ • MoveValid    │    │                 │
│   ationAgent   │    │                 │
│   (#869)       │    │                 │
└────────────────┘    └─────────────────┘

Pattern: Customer/Supplier (GameManagement subscribes to KnowledgeBase events)
Pattern: Partnership (Both contexts need agent invocation success)
```

---

## Validation Criteria

### ✅ Decision Validated If:

1. **No Cross-Context Service Calls**: Agent invocation uses only KnowledgeBase domain services
2. **Single Transaction Boundary**: Agent + VectorSearch + QualityTracking = atomic operation
3. **Linguistic Cohesion**: Agent terminology aligns with RAG/knowledge vocabulary
4. **Independent Agent Contexts**: Game agents stay in GameManagement (#869 validates this)

### ⚠️ Reconsider Decision If:

1. **Cross-Context Orchestration**: Agents start coordinating workflows across multiple contexts
2. **Context Explosion**: KnowledgeBase grows to >10 domain services
3. **Separate Deployment Need**: Agents require different scaling/deployment from RAG
4. **Vocabulary Divergence**: Agent language diverges from knowledge management terminology

---

## Alternative Considered: Separate "AI Agents" Context

### Why Rejected:

**Failed Cohesion Test**:
- Agents grouped by technical type (agent infrastructure), not business capability
- Would create artificial boundary splitting "knowledge retrieval" from "agent invocation"

**Failed Integration Test**:
- Every agent call → cross-context communication to VectorSearchDomainService
- Distributed transactions instead of local ACID
- Performance overhead from serialization + network latency

**Failed Pattern Test**:
- No industry precedent for separating agents from their domain
- Contradicts all three successful case studies
- Would repeat Walmart's anti-pattern (fragmented agents)

**Failed Anti-Pattern Test**:
- Creates Shared Kernel (agents depend on KnowledgeBase internals)
- Context proliferation (8th context without clear business justification)
- Risk of "Big Ball of Mud" from excessive boundaries

**Decision Score**: 32/100 (vs 88/100 for extending KnowledgeBase)

---

## References

### Academic Sources
1. **Nandi, K., & Dey, K. (2025).** "Designing Scalable Multi-Agent AI Systems: Leveraging Domain-Driven Design and Event Storming." *SSRG International Journal of Computer Science and Engineering*, 12(3), 10-16. https://doi.org/10.14445/23488387/IJCSE-V12I13P102

### Industry Articles
2. **Kostyra, P. (2025).** "Agent as Bounded Context (Part 1)." Medium.
3. **Bakthavachalu, S. (2025).** "Revolutionizing Enterprise AI: Applying Domain-Driven Design for Agentic Applications." Medium.
4. **DZone.** "Designing Scalable Multi-Agent AI Systems."

### Microsoft Resources
5. **Microsoft Azure Architecture Center.** "Using Tactical DDD to Design Microservices."
6. **Microsoft Azure Architecture Center.** "AI Agent Orchestration Patterns."

### DDD Foundational Texts
7. **Evans, E. (2004).** *Domain-Driven Design: Tackling Complexity in the Heart of Software*. Addison-Wesley.
8. **Vernon, V. (2013).** *Implementing Domain-Driven Design*. Addison-Wesley.

---

## Decision Impact

**Unblocked Issues**: #866, #867, #869
**Estimated Effort Saved**: 15-20h (vs separate context implementation)
**Technical Debt**: None introduced
**Risk Level**: Low (high industry pattern alignment)

**Next Actions**:
1. Implement Agent aggregate (#866)
2. Implement InvokeAgentCommand (#867)
3. Implement MoveValidationAgent in GameManagement (#869)
4. Update architecture documentation
5. Add agent-specific observability metrics

---

**Approved**: 2025-11-12
**Review Date**: 2026-03-01 (or when agents reach 10+ types in KnowledgeBase)

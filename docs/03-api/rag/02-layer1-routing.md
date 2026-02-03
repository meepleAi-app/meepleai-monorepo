# Layer 1: Intelligent Routing

**Purpose**: 3-dimensional routing (User tier + Template + Complexity) → Strategy + Model selection

**Token Cost**: ~320 tokens/query
**Latency**: <50ms

---

## Routing Dimensions

### Dimension 1: User Tier (from ADR-007)

Access control based on user role:

> **Note**: Anonymous users cannot access the RAG system. Authentication is required.

> **IMPORTANT: User Tier vs Cost**
>
> User tier affects **ACCESS CONTROL ONLY**, not cost calculations:
> - ✅ **Tier affects**: Which strategies are allowed, Cache TTL, Max tokens/query, Model access level
> - ❌ **Tier does NOT affect**: Cost per query, Token consumption, Model pricing
>
> **Cost is determined by STRATEGY + MODEL selection**, not by user tier.
> See [Appendix E - Model Pricing](appendix/E-model-pricing-2026.md) for cost details.

| User Tier | Max Tokens/Query | Allowed Strategies | Model Access | Cache TTL |
|-----------|------------------|-------------------|--------------|-----------|
| ~~Anonymous~~ | ❌ | **NO ACCESS** | Authentication required | - |
| User | 3,000 | FAST, BALANCED | Free + GPT-4o-mini | 48h |
| Editor | 5,000 | FAST, BALANCED, PRECISE | GPT-4o-mini, Haiku, Sonnet | 72h |
| Admin | 15,000 | All (FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, CUSTOM) | Full access (Opus) | 168h |
| Premium | 20,000+ | All | Priority access | 336h |

---

### Dimension 2: Template Classification

**Goal**: Classify query intent into 2 templates

**Templates**:
1. **rule_lookup**: User wants exact rule text from rulebook
2. **resource_planning**: User wants strategic advice or decision help

**Implementation Options**:

**A. Semantic Router** (Recommended):
```python
from semantic_router import SemanticRouter

routes = [
    {"name": "rule_lookup", "utterances": [
        "What does the rulebook say",
        "How many cards",
        "Setup procedure",
        "Is it legal to"
    ]},
    {"name": "resource_planning", "utterances": [
        "Should I trade or build",
        "Best strategy",
        "Which option is better"
    ]}
]
router = SemanticRouter(routes=routes)
template = router.route(query)  # <1ms, 85-92% accuracy
```

**B. LLM Classification** (Fallback):
```python
async def classify_template(query: str) -> str:
    response = await llm_haiku.generate(
        prompt=f"Classify: rule_lookup OR resource_planning\nQuery: {query}",
        max_tokens=10
    )
    return response.strip()  # ~50ms, 90-95% accuracy
```

**Token Cost**: 250-300 input + 10-20 output = ~270-320 tokens

---

### Dimension 3: Complexity Scoring

**Goal**: Assign 0-5 complexity score → select FAST/BALANCED/PRECISE

**Scoring Algorithm**:
```python
def calc_complexity(query: str, template: str) -> int:
    score = 0
    words = len(query.split())

    # Length-based
    if words > 50: score += 2
    elif words > 25: score += 1

    # Multi-concept keywords
    if any(k in query.lower() for k in ["and", "or", "both"]): score += 1

    # Edge cases / conditionals
    if any(k in query.lower() for k in ["what if", "except when"]): score += 1

    # Negation
    if any(k in query.lower() for k in ["not", "don't", "can't"]): score += 1

    # Template baseline
    if template == "resource_planning": score += 1

    return min(score, 5)
```

**Strategy Selection**:
```python
def select_strategy(complexity: int, template: str, user_tier: str,
                    requires_web: bool = False, requires_consensus: bool = False) -> str:
    """
    Select strategy based on query characteristics and user tier.

    Strategies:
    - FAST: Simple FAQ, quick responses (complexity ≤1)
    - BALANCED: Standard queries with CRAG validation (complexity 2-3)
    - PRECISE: Critical decisions, multi-agent pipeline (complexity ≥4)
    - EXPERT: Web search + multi-hop reasoning (requires external info)
    - CONSENSUS: Multi-LLM voting (high-stakes arbitration)
    - CUSTOM: Admin-configured (explicit selection only)
    """
    # CONSENSUS: High-stakes decisions requiring multi-LLM agreement
    if requires_consensus and user_tier in ["Admin", "Premium"]:
        return "CONSENSUS"

    # EXPERT: When external information is needed
    if requires_web and user_tier in ["Admin", "Premium"]:
        return "EXPERT"

    # Standard routing based on complexity
    if complexity <= 1 and template == "rule_lookup":
        return "FAST"
    elif complexity <= 3:
        return "BALANCED"
    else:
        return "PRECISE"
```

---

## Model Selection Matrix

Based on User Tier + Strategy:

```python
def select_model(user_role: str, strategy: str) -> tuple[str, str]:
    """Returns (provider, model_name)"""

    # Anonymous users cannot access the system
    if user_role == "Anonymous":
        raise AuthenticationRequiredException("Authentication required to use RAG system")

    if user_role == "User":
        if strategy == "FAST":
            return random_split(0.8, ("OpenRouter", "llama-3.3-70b:free"),
                                     ("OpenRouter", "gpt-4o-mini"))
        elif strategy == "BALANCED":
            return random_split(0.5, ("OpenRouter", "gpt-4o-mini"),
                                     ("OpenRouter", "claude-3.5-haiku"))
        else:  # PRECISE, EXPERT, CONSENSUS, CUSTOM denied
            raise UpgradeRequiredException(f"{strategy} requires Editor tier or higher")

    elif user_role == "Editor":
        if strategy == "FAST":
            return random_split(0.5, ("Ollama", "llama3:8b"),
                                     ("OpenRouter", "gpt-4o-mini"))
        elif strategy == "BALANCED":
            return random_split(0.5, ("OpenRouter", "gpt-4o-mini"),
                                     ("OpenRouter", "claude-3.5-haiku"))
        elif strategy == "PRECISE":
            return ("OpenRouter", "claude-3.5-sonnet")
        else:  # EXPERT, CONSENSUS, CUSTOM denied
            raise UpgradeRequiredException(f"{strategy} requires Admin tier")

    elif user_role in ["Admin", "Premium"]:
        if strategy == "FAST":
            return random_split(0.8, ("Ollama", "llama3:8b"),
                                     ("OpenRouter", "claude-3.5-haiku"))
        elif strategy == "BALANCED":
            return random_split(0.5, ("OpenRouter", "claude-3.5-haiku"),
                                     ("OpenRouter", "claude-3.5-sonnet"))
        elif strategy == "PRECISE":
            return ("OpenRouter", "claude-opus-4")  # Full premium access
        elif strategy == "EXPERT":
            return ("OpenRouter", "claude-3.5-sonnet")  # Web search + multi-hop
        elif strategy == "CONSENSUS":
            # Multi-LLM voting uses multiple providers
            return ("Multi", "consensus-voting")  # Special handling
        elif strategy == "CUSTOM":
            return ("Configured", "admin-custom")  # Uses phase-configured models

def random_split(prob: float, option_a: tuple, option_b: tuple) -> tuple:
    """A/B split based on probability"""
    import random
    return option_a if random.random() < prob else option_b
```

---

## Complete Routing Flow

```python
@dataclass
class RoutingDecision:
    strategy: str  # "FAST" | "BALANCED" | "PRECISE" | "EXPERT" | "CONSENSUS" | "CUSTOM"
    template: str  # "rule_lookup" | "resource_planning" | "setup_guide" | "strategy_advice" | "educational"
    complexity: int  # 0-5
    provider: str  # "Ollama" | "OpenRouter" | "Multi" | "Configured"
    model: str  # Model identifier
    user_tier: str
    tokens_budget: int

async def route_query(query: str, user: User) -> RoutingDecision:
    # Authentication check - Anonymous users cannot access the system
    if user is None or user.role == "Anonymous":
        raise AuthenticationRequiredException("Authentication required to use RAG system")

    role = user.role

    # Check user override flags
    if "--fast" in query:
        strategy = "FAST"
        template = await classify_template(query.replace("--fast", ""))
    elif "--balanced" in query:
        template = await classify_template(query.replace("--balanced", ""))
        strategy = "BALANCED"
    elif "--precise" in query:
        template = await classify_template(query.replace("--precise", ""))
        strategy = "PRECISE"
    elif "--expert" in query:
        template = await classify_template(query.replace("--expert", ""))
        strategy = "EXPERT"
    elif "--consensus" in query:
        template = await classify_template(query.replace("--consensus", ""))
        strategy = "CONSENSUS"
    elif "--custom" in query:
        template = await classify_template(query.replace("--custom", ""))
        strategy = "CUSTOM"
    else:
        # Standard routing
        template = await classify_template(query)
        complexity = calc_complexity(query, template)
        requires_web = detect_web_search_need(query)
        requires_consensus = detect_consensus_need(query)
        strategy = select_strategy(complexity, template, role, requires_web, requires_consensus)

    # Access control check
    if not can_access(role, strategy):
        raise AccessDeniedException(f"{strategy} requires {required_tier(strategy)}")

    # Model selection
    provider, model = select_model(role, strategy)

    return RoutingDecision(
        strategy=strategy,
        template=template,
        complexity=complexity,
        provider=provider,
        model=model,
        user_tier=role,
        tokens_budget=get_budget(role)
    )
```

---

## Testing

**Unit Tests** (20 tests):
- Template classification accuracy (10 tests, target >90%)
- Complexity scoring consistency (5 tests)
- Strategy selection logic (5 tests)

**Integration Tests** (10 tests):
- End-to-end routing with real queries
- Access control enforcement
- Model selection validation

**Source**: Design from TOMAC-RAG final spec

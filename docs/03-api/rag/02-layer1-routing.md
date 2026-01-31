# Layer 1: Intelligent Routing

**Purpose**: 3-dimensional routing (User tier + Template + Complexity) → Strategy + Model selection

**Token Cost**: ~320 tokens/query
**Latency**: <50ms

---

## Routing Dimensions

### Dimension 1: User Tier (from ADR-007)

Access control based on user role:

| User Tier | Max Tokens/Query | Allowed Strategies | Model Access |
|-----------|------------------|-------------------|--------------|
| Anonymous | 1,500 | FAST only | Free models (Llama 3.3) |
| User | 3,000 | FAST, BALANCED | Free + GPT-4o-mini |
| Editor | 5,000 | FAST, BALANCED, PRECISE | GPT-4o-mini, Haiku, Sonnet |
| Admin | 15,000 | All (incl. Multi-Agent) | Full access (Opus) |

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
def select_strategy(complexity: int, template: str) -> str:
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

    if user_role == "Anonymous":
        if strategy == "FAST":
            return random_split(0.8, ("OpenRouter", "llama-3.3-70b:free"),
                                     ("OpenRouter", "gpt-4o-mini"))
        elif strategy == "BALANCED":
            return random_split(0.2, ("OpenRouter", "gpt-4o-mini"),
                                     ("OpenRouter", "llama-3.3-70b:free"))
        else:  # PRECISE denied
            raise UpgradeRequiredException("PRECISE requires User tier or higher")

    elif user_role == "User":
        # Similar to Anonymous but with PRECISE quota
        if strategy == "PRECISE":
            if await check_quota(user_id, "precise", limit=5, period="day"):
                return ("OpenRouter", "claude-3.5-haiku")
            else:
                raise QuotaExceededException("PRECISE: 5/day limit reached")
        # ... (similar to Anonymous for FAST/BALANCED)

    elif user_role == "Editor":
        if strategy == "FAST":
            return random_split(0.5, ("Ollama", "llama3:8b"),
                                     ("OpenRouter", "gpt-4o-mini"))
        elif strategy == "BALANCED":
            return random_split(0.5, ("OpenRouter", "gpt-4o-mini"),
                                     ("OpenRouter", "claude-3.5-haiku"))
        else:  # PRECISE
            return ("OpenRouter", "claude-3.5-sonnet")

    elif user_role == "Admin":
        if strategy == "FAST":
            return random_split(0.8, ("Ollama", "llama3:8b"),
                                     ("OpenRouter", "claude-3.5-haiku"))
        elif strategy == "BALANCED":
            return random_split(0.5, ("OpenRouter", "claude-3.5-haiku"),
                                     ("OpenRouter", "claude-3.5-sonnet"))
        else:  # PRECISE
            return ("OpenRouter", "claude-opus-4")  # Full premium access

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
    strategy: str  # "FAST" | "BALANCED" | "PRECISE"
    template: str  # "rule_lookup" | "resource_planning"
    complexity: int  # 0-5
    provider: str  # "Ollama" | "OpenRouter"
    model: str  # Model identifier
    user_tier: str
    tokens_budget: int

async def route_query(query: str, user: User) -> RoutingDecision:
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
    else:
        # Standard routing
        template = await classify_template(query)
        complexity = calc_complexity(query, template)
        strategy = select_strategy(complexity, template)

    # Access control check
    role = user.role if user else "Anonymous"
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

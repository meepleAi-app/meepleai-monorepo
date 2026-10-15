# Arbiter `/validate` (Python validatore puro + client C#) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `orchestration-service` espone `POST /validate` che, dati stato, mossa e i claim v3 della card pubblicata, restituisce un verdetto deterministico (`Valid | Invalid | Unknown`) senza LLM e con `confidence` mai fissa; il C# lo chiama dalla disputa sulle regole della partita (ADR-098, dopo la fetta 8 di ADR-095) e, in caso di timeout o errore, risponde `Unknown` senza inventare citazioni.

**Architecture:** Un modulo Python puro (`src/application/validation_kernel.py`) implementa la risoluzione defettibile leggera (trigger → scarto sovrascritte → priorità massima → conflitto ⇒ Unknown); FastAPI lo espone con schemi Pydantic; un client C# tipizzato in `KnowledgeBase/Infrastructure` lo chiama via l'`HttpClient` nominato `OrchestrationService` già registrato, con SSRF pin e timeout dedicato; la disputa sulla pipeline lo usa come strumento e fa spiegare il verdetto formale dal modello (Task 4, sospeso fino alla fetta 8). Dipende dal piano gemello `2026-10-08-mechanic-claims-v3.md` (Task 7: `PublishedMechanicCardClaimDto` con `Kind/Priority/Overrides/Trigger`).

**Tech Stack:** Python 3.11, FastAPI, Pydantic v2, pytest (`asyncio_mode = auto`); .NET 9, `IHttpClientFactory`, MediatR, xUnit + Moq.

**Spec:** `docs/for-developers/specs/2026-10-08-mechanic-claims-v3-defeasible-rules-design.md` §6 · ADR-092.

> ✏️ 2026-10-15, ADR-098: il consumatore C# del verdetto è la disputa sulle regole della partita, quando passa sulla pipeline con la fetta 8 di ADR-095 (#4171). I Task 1–3 e 5 restano; il Task 4 è sospeso e si riscrive su quella fetta; il Task 6 verifica tramite una disputa. Né `AskArbiterCommandHandler` (#4386) né `ValidateMoveCommandHandler` (ADR-098) sono il punto d'innesto.

## Global Constraints

- Branch da `main-dev` pulito: `feature/<issue>-arbiter-validate`; parent `main-dev`; PR verso `main-dev`. Mergiare **dopo** il piano gemello (dipendenza sul DTO).
- ADR-092 §2: `confidence` è `null` salvo misura reale. Nessun valore fisso in nessun ramo, incluso l'errore.
- ADR-092 §4: timeout o errore ⇒ `Unknown`, mai `Valid` per default; l'handler C# non lancia per un'indisponibilità del validatore, registra una metrica.
- Nessuna chiamata LLM dentro `/validate`.
- Ogni `HttpClient` verso un host fisso usa `ConfigureSsrfPin` (`SharedKernel/Infrastructure/Http/SsrfPinnedHttpClientBuilderExtensions.cs:44`).
- Endpoint C#: solo `IMediator.Send()`; nessun servizio iniettato negli endpoint.
- Python: `pytest` dalla cartella `apps/orchestration-service` (`conftest.py` imposta `OPENROUTER_API_KEY=test-key`); nuovi test in `tests/`.
- Polarità (permette/vieta) derivata da un classificatore lessicale IT/EN **documentato nei test** (spec §6): la lista di verbi è dato di test, non intelligenza nascosta.
- Commit ≤ 72 caratteri, `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.

## Review Focus

1. Due regole applicabili allo stesso livello, polarità opposte, senza `Overrides` fra loro ⇒ `Unknown` con entrambe in `applied_rule_ids`, non la prima che capita. Test in Task 1 (`conflict_same_level_is_unknown`).
2. Regola sovrascritta da un'eccezione il cui trigger **non** combacia con la mossa: l'eccezione non si applica e la regola generale resta attiva. Test in Task 1 (`override_only_counts_when_overrider_applies`).
3. Claim con `kind = Example` o `Clarification` nella lista: non devono mai produrre un verdetto. Test in Task 1 (`examples_and_clarifications_never_decide`).
4. Timeout del servizio Python (`HttpClient.Timeout`): la disputa deve restituire il verdetto del modello sui passaggi recuperati con `FormalVerdict = "Unknown"` e una metrica incrementata, non 500. Test in Task 4 (`Validate_Timeout_YieldsUnknown`).
5. Regole con `trigger` nullo (jolly) devono applicarsi a qualunque mossa, anche con `state` vuoto. Test in Task 1 (`wildcard_trigger_applies_to_any_move`).

---

## File Structure

| Path | Responsabilità |
|---|---|
| `apps/orchestration-service/src/application/validation_kernel.py` (new) | kernel puro: dataclass `Rule`, `Verdict`, funzione `resolve(rules, state, move) -> Verdict` |
| `apps/orchestration-service/src/application/polarity.py` (new) | `classify_polarity(text: str) -> Literal["permit","forbid","neutral"]` con liste di verbi IT/EN |
| `apps/orchestration-service/src/api/schemas.py` | `ValidateRequest`, `ValidateRule`, `ValidateTrigger`, `ValidateResponse`, `ExplanationStep` |
| `apps/orchestration-service/main.py` | route `POST /validate`, metrica `validate_requests_total{verdict}` |
| `apps/orchestration-service/tests/test_validation_kernel.py`, `tests/test_polarity.py`, `tests/test_validate_api.py` (new) | test |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/Validation/IArbiterValidationClient.cs` (new) | interfaccia + record `ArbiterValidationRequest/Result` |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/External/ArbiterValidationClient.cs` (new) | implementazione HTTP |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs` | registrazione client + `AddHttpClient("ArbiterValidation")` con SSRF pin |
| DTO del verdetto della disputa (si definisce nella fetta 8) | campi `FormalVerdict`, `AppliedRuleIds`, `ViolatedRuleIds`, `FormalConfidence`, `FormalExplanation` |
| gestore della disputa sulla pipeline (fetta 8, #4171) | chiamata al validatore; il verdetto formale arriva al prompt del compito «arbitro» (Task 4, sospeso) |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/ArbiterValidationClientTests.cs`, i test della disputa si scrivono con la fetta 8 | test |

---

### Task 1: Kernel di risoluzione (Python, puro)

**Files:**
- Create: `apps/orchestration-service/src/application/validation_kernel.py`
- Create: `apps/orchestration-service/src/application/polarity.py`
- Test: `apps/orchestration-service/tests/test_validation_kernel.py`, `tests/test_polarity.py`

**Interfaces:**
- Produces:
  ```python
  @dataclass(frozen=True)
  class Trigger: phase: str | None; action: str | None; component: str | None
  @dataclass(frozen=True)
  class Rule: id: str; kind: str; priority: int; overrides: tuple[str, ...]; trigger: Trigger | None; text: str; polarity: str  # "permit"|"forbid"|"neutral"
  @dataclass(frozen=True)
  class ExplanationStep: rule_id: str; step: str
  @dataclass(frozen=True)
  class Verdict: verdict: str; applied_rule_ids: list[str]; violated_rule_ids: list[str]; explanation_steps: list[ExplanationStep]; confidence: None
  def normalize(s: str) -> str  # trim, lower, collassa spazi (stessa regola di MechanicTrigger.Normalize in C#)
  def trigger_matches(trigger: Trigger | None, state: dict, move: dict) -> bool
  def resolve(rules: list[Rule], state: dict, move: dict) -> Verdict
  ```
  `move` ha chiavi opzionali `action`, `component`, `text`; `state` ha `phase` opzionale. Un campo di trigger nullo è jolly; un campo valorizzato combacia se è uguale (normalizzato) al campo omonimo di `state`/`move`, oppure se compare come sottostringa in `normalize(move["text"])` quando il campo omonimo manca.
  `PRIORITY = {"Base": 0, "Expansion": 1, "Card": 2, "Scenario": 3}`.

- [ ] **Step 1: Scrivi i test del kernel**

```python
# apps/orchestration-service/tests/test_validation_kernel.py
"""Spec 2026-10-08 §6 — deterministic defeasible resolution (ADR-092 §3)."""
from src.application.validation_kernel import Rule, Trigger, resolve


def rule(id_, *, kind="Rule", priority=0, overrides=(), trigger=None, polarity="forbid", text=""):
    return Rule(id=id_, kind=kind, priority=priority, overrides=tuple(overrides),
                trigger=trigger, text=text, polarity=polarity)


def test_no_rules_is_unknown():
    v = resolve([], {}, {"action": "muovere"})
    assert v.verdict == "Unknown"
    assert v.applied_rule_ids == [] and v.confidence is None


def test_wildcard_trigger_applies_to_any_move():
    r = rule("a", polarity="forbid")
    v = resolve([r], {}, {"action": "qualunque"})
    assert v.verdict == "Invalid"
    assert v.applied_rule_ids == ["a"] and v.violated_rule_ids == ["a"]


def test_trigger_must_match_state_and_move():
    r = rule("a", trigger=Trigger(phase="azione", action="muovere", component=None), polarity="permit")
    assert resolve([r], {"phase": "Azione"}, {"action": "Muovere"}).verdict == "Valid"
    assert resolve([r], {"phase": "setup"}, {"action": "muovere"}).verdict == "Unknown"


def test_trigger_matches_free_text_when_structured_field_missing():
    r = rule("a", trigger=Trigger(phase=None, action=None, component="carta fretta"), polarity="permit")
    v = resolve([r], {}, {"text": "Anna gioca la Carta Fretta e fa tre azioni"})
    assert v.verdict == "Valid"


def test_exception_overrides_general_rule():
    general = rule("g", polarity="forbid", text="non puoi fare tre azioni")
    exc = rule("e", kind="Exception", priority=2, overrides=("g",),
               trigger=Trigger(None, None, "carta fretta"), polarity="permit", text="con Fretta puoi fare tre azioni")
    v = resolve([general, exc], {}, {"text": "gioco carta fretta, tre azioni"})
    assert v.verdict == "Valid"
    assert v.applied_rule_ids == ["e"]
    assert any(s.rule_id == "g" and "overridden" in s.step for s in v.explanation_steps)


def test_override_only_counts_when_overrider_applies():
    general = rule("g", polarity="forbid")
    exc = rule("e", kind="Exception", priority=2, overrides=("g",), trigger=Trigger(None, None, "carta fretta"), polarity="permit")
    v = resolve([general, exc], {}, {"text": "mossa normale"})
    assert v.verdict == "Invalid" and v.applied_rule_ids == ["g"]


def test_higher_priority_wins_without_explicit_override():
    base = rule("b", priority=0, polarity="forbid")
    card = rule("c", priority=2, polarity="permit")
    v = resolve([base, card], {}, {"action": "x"})
    assert v.verdict == "Valid" and v.applied_rule_ids == ["c"]


def test_conflict_same_level_is_unknown():
    a = rule("a", polarity="forbid")
    b = rule("b", polarity="permit")
    v = resolve([a, b], {}, {"action": "x"})
    assert v.verdict == "Unknown"
    assert sorted(v.applied_rule_ids) == ["a", "b"]


def test_examples_and_clarifications_never_decide():
    ex = rule("x", kind="Example", polarity="permit")
    cl = rule("c", kind="Clarification", polarity="forbid")
    v = resolve([ex, cl], {}, {"action": "x"})
    assert v.verdict == "Unknown"
    assert v.applied_rule_ids == []
    assert any(s.rule_id == "c" for s in v.explanation_steps)


def test_neutral_polarity_does_not_decide():
    n = rule("n", polarity="neutral")
    assert resolve([n], {}, {"action": "x"}).verdict == "Unknown"


def test_confidence_is_always_none():
    for rules in ([], [rule("a")], [rule("a", polarity="permit")]):
        assert resolve(rules, {}, {"action": "x"}).confidence is None
```

```python
# apps/orchestration-service/tests/test_polarity.py
import pytest
from src.application.polarity import classify_polarity


@pytest.mark.parametrize("text,expected", [
    ("Non puoi muovere il cavaliere attraverso altri pezzi", "forbid"),
    ("È vietato scambiare carte durante la fase di azione", "forbid"),
    ("You cannot build on water", "forbid"),
    ("Puoi fare due azioni per turno", "permit"),
    ("Players may trade resources with the bank", "permit"),
    ("Il mazzo contiene 60 carte", "neutral"),
])
def test_classify_polarity(text, expected):
    assert classify_polarity(text) == expected


def test_forbid_wins_over_permit_when_both_present():
    assert classify_polarity("Puoi muovere, ma non puoi saltare") == "forbid"
```

- [ ] **Step 2: Esegui e verifica che fallisca**

Run: `cd apps/orchestration-service && pytest tests/test_validation_kernel.py tests/test_polarity.py -q`
Expected: FAIL (`ModuleNotFoundError`).

- [ ] **Step 3: Implementa `polarity.py`**

```python
# apps/orchestration-service/src/application/polarity.py
"""Lexical polarity classifier for rule claims (spec 2026-10-08 §6). Deliberately simple and
test-documented: FORBID markers win over PERMIT markers; no marker → neutral."""
import re
from typing import Literal

Polarity = Literal["permit", "forbid", "neutral"]

_FORBID = [
    r"\bnon (si )?(puoi|può|possono|potete|è possibile)\b", r"\bnon (è|e') (consentito|permesso|ammesso)\b",
    r"\b(è|e') vietato\b", r"\bvietat[oaie]\b", r"\bmai\b", r"\bnon devi\b", r"\bnon deve\b",
    r"\bcannot\b", r"\bcan't\b", r"\bmay not\b", r"\bmust not\b", r"\bnot allowed\b", r"\bnever\b", r"\bforbidden\b",
]
_PERMIT = [
    r"\bpuoi\b", r"\bpuò\b", r"\bpossono\b", r"\bpotete\b", r"\b(è|e') (consentito|permesso|ammesso)\b",
    r"\bcan\b", r"\bmay\b", r"\ballowed\b",
]
_FORBID_RE = re.compile("|".join(_FORBID), re.IGNORECASE)
_PERMIT_RE = re.compile("|".join(_PERMIT), re.IGNORECASE)


def classify_polarity(text: str) -> Polarity:
    if not text:
        return "neutral"
    if _FORBID_RE.search(text):
        return "forbid"
    if _PERMIT_RE.search(text):
        return "permit"
    return "neutral"
```

- [ ] **Step 4: Implementa `validation_kernel.py`**

```python
# apps/orchestration-service/src/application/validation_kernel.py
"""Deterministic defeasible resolution (ADR-092 §3, spec 2026-10-08 §6). No LLM, no I/O."""
from __future__ import annotations

import re
from dataclasses import dataclass, field

PRIORITY = {"Base": 0, "Expansion": 1, "Card": 2, "Scenario": 3}
DECIDING_KINDS = {"Rule", "Exception"}
_SPACES = re.compile(r"\s+")


def normalize(s: str) -> str:
    return _SPACES.sub(" ", s.strip()).lower()


@dataclass(frozen=True)
class Trigger:
    phase: str | None
    action: str | None
    component: str | None


@dataclass(frozen=True)
class Rule:
    id: str
    kind: str
    priority: int
    overrides: tuple[str, ...]
    trigger: Trigger | None
    text: str
    polarity: str  # permit | forbid | neutral


@dataclass(frozen=True)
class ExplanationStep:
    rule_id: str
    step: str


@dataclass(frozen=True)
class Verdict:
    verdict: str  # Valid | Invalid | Unknown
    applied_rule_ids: list[str]
    violated_rule_ids: list[str]
    explanation_steps: list[ExplanationStep] = field(default_factory=list)
    confidence: None = None  # ADR-092 §2: never a fixed number


def _field_matches(expected: str | None, actual: str | None, free_text: str) -> bool:
    if expected is None:
        return True
    exp = normalize(expected)
    if actual is not None:
        return normalize(actual) == exp
    return exp in free_text


def trigger_matches(trigger: Trigger | None, state: dict, move: dict) -> bool:
    if trigger is None:
        return True
    free_text = normalize(str(move.get("text") or ""))
    return (
        _field_matches(trigger.phase, state.get("phase"), free_text)
        and _field_matches(trigger.action, move.get("action"), free_text)
        and _field_matches(trigger.component, move.get("component"), free_text)
    )


def resolve(rules: list[Rule], state: dict, move: dict) -> Verdict:
    steps: list[ExplanationStep] = []

    applicable: list[Rule] = []
    for r in rules:
        if r.kind not in DECIDING_KINDS:
            steps.append(ExplanationStep(r.id, f"{r.kind} does not decide"))
            continue
        if trigger_matches(r.trigger, state, move):
            applicable.append(r)
            steps.append(ExplanationStep(r.id, "trigger match" if r.trigger else "wildcard trigger"))
        else:
            steps.append(ExplanationStep(r.id, "trigger does not match"))

    overridden = {t for r in applicable for t in r.overrides}
    survivors = []
    for r in applicable:
        if r.id in overridden:
            steps.append(ExplanationStep(r.id, "overridden by an applicable exception"))
        else:
            survivors.append(r)

    deciding = [r for r in survivors if r.polarity in ("permit", "forbid")]
    for r in survivors:
        if r.polarity == "neutral":
            steps.append(ExplanationStep(r.id, "neutral polarity, does not decide"))
    if not deciding:
        return Verdict("Unknown", [], [], steps)

    top = max(r.priority for r in deciding)
    winners = [r for r in deciding if r.priority == top]
    for r in deciding:
        if r.priority < top:
            steps.append(ExplanationStep(r.id, f"lower priority ({r.priority} < {top})"))

    polarities = {r.polarity for r in winners}
    ids = [r.id for r in winners]
    if len(polarities) > 1:
        for r in winners:
            steps.append(ExplanationStep(r.id, "conflict at the same priority without override"))
        return Verdict("Unknown", ids, [], steps)

    if polarities == {"forbid"}:
        return Verdict("Invalid", ids, ids, steps)
    return Verdict("Valid", ids, [], steps)
```

- [ ] **Step 5: Esegui e verifica che passi**

Run: `pytest tests/test_validation_kernel.py tests/test_polarity.py -q`
Expected: PASS (tutti). Se `test_forbid_wins_over_permit_when_both_present` fallisce, l'ordine di controllo in `classify_polarity` è sbagliato: FORBID prima.

- [ ] **Step 6: Commit**

```bash
git add apps/orchestration-service/src/application/validation_kernel.py apps/orchestration-service/src/application/polarity.py apps/orchestration-service/tests/test_validation_kernel.py apps/orchestration-service/tests/test_polarity.py
git commit -m "feat(orchestration): kernel deterministico di risoluzione defettibile"
```

---

### Task 2: Schemi e route `POST /validate`

**Files:**
- Modify: `apps/orchestration-service/src/api/schemas.py`, `src/api/__init__.py`
- Modify: `apps/orchestration-service/main.py` (route + metrica)
- Test: `apps/orchestration-service/tests/test_validate_api.py`

**Interfaces:**
- Consumes: Task 1.
- Produces (JSON, snake_case):
  - `ValidateTrigger {phase?: str, action?: str, component?: str}`
  - `ValidateRule {id: str, kind: "Rule"|"Exception"|"Clarification"|"Example", priority: "Base"|"Expansion"|"Card"|"Scenario", overrides: list[str] = [], trigger: ValidateTrigger | None = None, claim: str, citations: list[dict] = []}`
  - `ValidateRequest {game_id: UUID, state: dict = {}, move: dict = {}, rules: list[ValidateRule]}`
  - `ExplanationStep {rule_id: str, step: str}`
  - `ValidateResponse {verdict: str, applied_rule_ids: list[str], violated_rule_ids: list[str], explanation_steps: list[ExplanationStep], confidence: float | None = None, execution_time_ms: float}`
  - metrica in `metrics`: `validate_requests_total`, `validate_verdict_valid_total`, `validate_verdict_invalid_total`, `validate_verdict_unknown_total` (esposte da `/metrics` come le altre).

- [ ] **Step 1: Scrivi i test API**

```python
# apps/orchestration-service/tests/test_validate_api.py
"""ADR-092: POST /validate is pure (no LLM), confidence is null, verdict never fixed."""
from uuid import uuid4

import pytest
from fastapi.testclient import TestClient


@pytest.fixture
def client():
    from main import app
    return TestClient(app)


def _rule(id_, kind="Rule", priority="Base", overrides=None, trigger=None, claim="non puoi fare tre azioni"):
    return {"id": id_, "kind": kind, "priority": priority, "overrides": overrides or [],
            "trigger": trigger, "claim": claim, "citations": [{"pdf_page": 4}]}


def test_validate_returns_invalid_for_forbidding_rule(client):
    body = {"game_id": str(uuid4()), "state": {}, "move": {"text": "faccio tre azioni"},
            "rules": [_rule("g")]}
    r = client.post("/validate", json=body)
    assert r.status_code == 200
    data = r.json()
    assert data["verdict"] == "Invalid"
    assert data["applied_rule_ids"] == ["g"]
    assert data["confidence"] is None
    assert data["execution_time_ms"] >= 0


def test_validate_exception_with_trigger_wins(client):
    body = {"game_id": str(uuid4()), "state": {"phase": "azione"}, "move": {"text": "gioco carta fretta e faccio tre azioni"},
            "rules": [_rule("g"),
                      _rule("e", kind="Exception", priority="Card", overrides=["g"],
                            trigger={"component": "carta fretta"}, claim="con Fretta puoi fare tre azioni")]}
    data = client.post("/validate", json=body).json()
    assert data["verdict"] == "Valid"
    assert data["applied_rule_ids"] == ["e"]


def test_validate_without_rules_is_unknown(client):
    body = {"game_id": str(uuid4()), "rules": []}
    data = client.post("/validate", json=body).json()
    assert data["verdict"] == "Unknown" and data["applied_rule_ids"] == []


def test_validate_rejects_bad_kind(client):
    body = {"game_id": str(uuid4()), "rules": [_rule("g", kind="Banana")]}
    assert client.post("/validate", json=body).status_code == 422


def test_metrics_count_verdicts(client):
    before = client.get("/metrics").text
    client.post("/validate", json={"game_id": str(uuid4()), "rules": []})
    after = client.get("/metrics").text
    assert "validate_verdict_unknown_total" in after
    assert after != before
```

- [ ] **Step 2: Esegui e verifica che fallisca** — `pytest tests/test_validate_api.py -q` → FAIL (404 su `/validate`).

- [ ] **Step 3: Implementa gli schemi**

In `schemas.py` aggiungi:

```python
from typing import Literal, Optional
from pydantic import BaseModel, Field


class ValidateTrigger(BaseModel):
    phase: Optional[str] = None
    action: Optional[str] = None
    component: Optional[str] = None


class ValidateRule(BaseModel):
    id: str
    kind: Literal["Rule", "Exception", "Clarification", "Example"] = "Rule"
    priority: Literal["Base", "Expansion", "Card", "Scenario"] = "Base"
    overrides: list[str] = Field(default_factory=list)
    trigger: Optional[ValidateTrigger] = None
    claim: str = ""
    citations: list[dict] = Field(default_factory=list)


class ValidateRequest(BaseModel):
    game_id: UUID
    state: dict = Field(default_factory=dict)
    move: dict = Field(default_factory=dict)
    rules: list[ValidateRule] = Field(default_factory=list)


class ExplanationStepDto(BaseModel):
    rule_id: str
    step: str


class ValidateResponse(BaseModel):
    verdict: Literal["Valid", "Invalid", "Unknown"]
    applied_rule_ids: list[str]
    violated_rule_ids: list[str]
    explanation_steps: list[ExplanationStepDto]
    confidence: Optional[float] = None  # ADR-092 §2
    execution_time_ms: float
```

ed esportali in `src/api/__init__.py`.

- [ ] **Step 4: Implementa la route in `main.py`**

Aggiungi alle metriche: `"validate_requests_total": 0, "validate_verdict_valid_total": 0, "validate_verdict_invalid_total": 0, "validate_verdict_unknown_total": 0`. Import: `from src.api import ValidateRequest, ValidateResponse, ExplanationStepDto` e `from src.application.validation_kernel import Rule, Trigger, PRIORITY, resolve` e `from src.application.polarity import classify_polarity`. Route, dopo `/execute`:

```python
@app.post("/validate", response_model=ValidateResponse, tags=["Arbiter"])
async def validate_move(request: ValidateRequest):
    """ADR-092: deterministic verdict from v3 claims. No LLM call. Confidence is null."""
    start = time.perf_counter()
    rules = [
        Rule(
            id=r.id, kind=r.kind, priority=PRIORITY[r.priority], overrides=tuple(r.overrides),
            trigger=Trigger(r.trigger.phase, r.trigger.action, r.trigger.component) if r.trigger else None,
            text=r.claim, polarity=classify_polarity(r.claim),
        )
        for r in request.rules
    ]
    verdict = resolve(rules, request.state, request.move)
    metrics["validate_requests_total"] += 1
    metrics[f"validate_verdict_{verdict.verdict.lower()}_total"] += 1
    return ValidateResponse(
        verdict=verdict.verdict,
        applied_rule_ids=verdict.applied_rule_ids,
        violated_rule_ids=verdict.violated_rule_ids,
        explanation_steps=[ExplanationStepDto(rule_id=s.rule_id, step=s.step) for s in verdict.explanation_steps],
        confidence=None,
        execution_time_ms=(time.perf_counter() - start) * 1000,
    )
```

Verifica che `/metrics` serializzi tutte le chiavi di `metrics` (leggi la funzione a `main.py:284`); se elenca le chiavi a mano, aggiungi le quattro nuove.

- [ ] **Step 5: Esegui tutta la suite Python**

Run: `pytest -q`
Expected: PASS, inclusi i test esistenti (`test_api.py` ecc.).

- [ ] **Step 6: Commit**

```bash
git add apps/orchestration-service/src/api apps/orchestration-service/main.py apps/orchestration-service/tests/test_validate_api.py
git commit -m "feat(orchestration): POST /validate deterministico (ADR-092)"
```

---

### Task 3: Client C# `IArbiterValidationClient`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/Validation/IArbiterValidationClient.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/External/ArbiterValidationClient.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs` (registrazione)
- Modify: `apps/api/src/Api/appsettings.json` (sezione `ArbiterValidation: { "TimeoutSeconds": 3 }`)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/ArbiterValidationClientTests.cs`

**Interfaces:**
- Consumes: `PublishedMechanicCardDto` con `Kind/Priority/Overrides/Trigger` (piano gemello Task 7); `ConfigureSsrfPin(sink, allowedHostSuffixes)`.
- Produces:
  ```csharp
  public sealed record ArbiterValidationRequest(Guid GameId, IReadOnlyDictionary<string, object?> State, IReadOnlyDictionary<string, object?> Move, IReadOnlyList<PublishedMechanicCardClaimDto> Rules);
  public sealed record ArbiterValidationResult(string Verdict, IReadOnlyList<Guid> AppliedRuleIds, IReadOnlyList<Guid> ViolatedRuleIds, IReadOnlyList<(Guid RuleId, string Step)> ExplanationSteps, double? Confidence)
  {
      public static ArbiterValidationResult Unknown(string reason) => new("Unknown", [], [], [(Guid.Empty, reason)], null);
  }
  public interface IArbiterValidationClient { Task<ArbiterValidationResult> ValidateAsync(ArbiterValidationRequest request, CancellationToken ct); }
  ```
  Comportamento: `POST /validate` con JSON snake_case (`game_id`, `state`, `move`, `rules[{id, kind, priority, overrides, trigger{phase,action,component}, claim, citations[{pdf_page}]}]`); su `HttpRequestException`, `TaskCanceledException` (timeout), status non 2xx o JSON non deserializzabile ⇒ `ArbiterValidationResult.Unknown("<motivo>")` **senza lanciare**, log a livello Warning, metrica `meepleai_arbiter_validation_unavailable_total` (usa il `Meter` del progetto: `grep -rn "new Meter(" apps/api/src/Api --include=*.cs | head -1` e segui la convenzione dei nomi in memoria: l'unità finisce nel nome).

- [ ] **Step 1: Scrivi i test del client con `HttpMessageHandler` finto**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/ArbiterValidationClientTests.cs
using System.Net;
using System.Text;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Validation;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.External;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class ArbiterValidationClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _respond;
        public HttpRequestMessage? Last;
        public string? LastBody;
        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) => _respond = respond;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return await _respond(request);
        }
    }

    private static ArbiterValidationClient Client(StubHandler handler, TimeSpan? timeout = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://orchestration-service:8004"), Timeout = timeout ?? TimeSpan.FromSeconds(3) };
        return new ArbiterValidationClient(http, NullLogger<ArbiterValidationClient>.Instance);
    }

    private static ArbiterValidationRequest Request() => new(
        Guid.NewGuid(),
        new Dictionary<string, object?> { ["phase"] = "azione" },
        new Dictionary<string, object?> { ["text"] = "tre azioni" },
        new[] { new PublishedMechanicCardClaimDto(Guid.NewGuid(), "non puoi fare tre azioni", Array.Empty<PublishedMechanicCardCitationDto>(), MechanicClaimKind.Rule, MechanicRulePriority.Base, Array.Empty<Guid>(), null) });

    [Fact]
    public async Task ValidateAsync_PostsSnakeCaseContract_AndParsesResult()
    {
        var ruleId = Guid.NewGuid();
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""{"verdict":"Invalid","applied_rule_ids":["{{ruleId}}"],"violated_rule_ids":["{{ruleId}}"],"explanation_steps":[{"rule_id":"{{ruleId}}","step":"wildcard trigger"}],"confidence":null,"execution_time_ms":0.4}""", Encoding.UTF8, "application/json")
        }));

        var result = await Client(handler).ValidateAsync(Request(), CancellationToken.None);

        handler.Last!.RequestUri!.AbsolutePath.Should().Be("/validate");
        handler.LastBody.Should().Contain("\"game_id\"").And.Contain("\"rules\"").And.Contain("\"kind\":\"Rule\"").And.Contain("\"priority\":\"Base\"");
        result.Verdict.Should().Be("Invalid");
        result.AppliedRuleIds.Should().Equal(ruleId);
        result.Confidence.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_Timeout_ReturnsUnknownWithoutThrowing()
    {
        var handler = new StubHandler(async _ => { await Task.Delay(TimeSpan.FromSeconds(5)); return new HttpResponseMessage(HttpStatusCode.OK); });
        var result = await Client(handler, TimeSpan.FromMilliseconds(100)).ValidateAsync(Request(), CancellationToken.None);
        result.Verdict.Should().Be("Unknown");
        result.AppliedRuleIds.Should().BeEmpty();
        result.Confidence.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_Non2xx_ReturnsUnknown()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var result = await Client(handler).ValidateAsync(Request(), CancellationToken.None);
        result.Verdict.Should().Be("Unknown");
    }

    [Fact]
    public async Task ValidateAsync_MalformedJson_ReturnsUnknown()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{not json", Encoding.UTF8, "application/json") }));
        var result = await Client(handler).ValidateAsync(Request(), CancellationToken.None);
        result.Verdict.Should().Be("Unknown");
    }
}
```

- [ ] **Step 2: Esegui e verifica che fallisca** — `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~ArbiterValidationClientTests"` → FAIL (compilazione).

- [ ] **Step 3: Implementa interfaccia e client**

```csharp
// BoundedContexts/KnowledgeBase/Application/Services/Validation/IArbiterValidationClient.cs
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Validation;

public sealed record ArbiterValidationRequest(
    Guid GameId,
    IReadOnlyDictionary<string, object?> State,
    IReadOnlyDictionary<string, object?> Move,
    IReadOnlyList<PublishedMechanicCardClaimDto> Rules);

public sealed record ArbiterValidationResult(
    string Verdict,
    IReadOnlyList<Guid> AppliedRuleIds,
    IReadOnlyList<Guid> ViolatedRuleIds,
    IReadOnlyList<(Guid RuleId, string Step)> ExplanationSteps,
    double? Confidence)
{
    public const string ValidVerdict = "Valid";
    public const string InvalidVerdict = "Invalid";
    public const string UnknownVerdict = "Unknown";

    /// <summary>ADR-092 §4: unavailability is Unknown, never Valid.</summary>
    public static ArbiterValidationResult Unknown(string reason) =>
        new(UnknownVerdict, Array.Empty<Guid>(), Array.Empty<Guid>(), new[] { (Guid.Empty, reason) }, null);
}

/// <summary>Deterministic verdict from orchestration-service POST /validate (ADR-092).</summary>
public interface IArbiterValidationClient
{
    Task<ArbiterValidationResult> ValidateAsync(ArbiterValidationRequest request, CancellationToken cancellationToken);
}
```

```csharp
// BoundedContexts/KnowledgeBase/Infrastructure/External/ArbiterValidationClient.cs
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Validation;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.External;

internal sealed class ArbiterValidationClient : IArbiterValidationClient
{
    public const string HttpClientName = "ArbiterValidation";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly ILogger<ArbiterValidationClient> _logger;

    public ArbiterValidationClient(HttpClient http, ILogger<ArbiterValidationClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ArbiterValidationResult> ValidateAsync(ArbiterValidationRequest request, CancellationToken cancellationToken)
    {
        var body = new
        {
            game_id = request.GameId,
            state = request.State,
            move = request.Move,
            rules = request.Rules.Select(r => new
            {
                id = r.Id,
                kind = r.Kind.ToString(),
                priority = r.Priority.ToString(),
                overrides = r.Overrides ?? Array.Empty<Guid>(),
                trigger = r.Trigger is null ? null : new { phase = r.Trigger.Phase, action = r.Trigger.Action, component = r.Trigger.Component },
                claim = r.Claim,
                citations = r.Citations.Select(c => new { pdf_page = c.PdfPage })
            })
        };

        try
        {
            using var response = await _http.PostAsJsonAsync("/validate", body, Json, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Arbiter validation returned {Status}; verdict Unknown.", (int)response.StatusCode);
                return ArbiterValidationResult.Unknown($"validator status {(int)response.StatusCode}");
            }

            var dto = await response.Content.ReadFromJsonAsync<ValidateResponseDto>(Json, cancellationToken).ConfigureAwait(false);
            if (dto is null)
            {
                return ArbiterValidationResult.Unknown("validator returned empty body");
            }

            return new ArbiterValidationResult(
                dto.Verdict,
                dto.AppliedRuleIds,
                dto.ViolatedRuleIds,
                dto.ExplanationSteps.Select(s => (s.RuleId, s.Step)).ToList(),
                dto.Confidence);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // TaskCanceledException here is the HttpClient timeout (the caller's token is observed by PostAsJsonAsync too;
            // if cancellationToken is cancelled we still return Unknown rather than throwing, per ADR-092 §4).
            _logger.LogWarning(ex, "Arbiter validation unavailable; verdict Unknown.");
            return ArbiterValidationResult.Unknown(ex.GetType().Name);
        }
    }

    private sealed record ValidateResponseDto(
        string Verdict,
        List<Guid> AppliedRuleIds,
        List<Guid> ViolatedRuleIds,
        List<StepDto> ExplanationSteps,
        double? Confidence,
        double ExecutionTimeMs);

    private sealed record StepDto(Guid RuleId, string Step);
}
```

Registrazione in `KnowledgeBaseServiceExtensions.cs` (accanto alla registrazione del reranker, riga ~559):

```csharp
        services.AddHttpClient<IArbiterValidationClient, ArbiterValidationClient>(ArbiterValidationClient.HttpClientName, (sp, client) =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var baseUrl = cfg["ORCHESTRATION_SERVICE_URL"] ?? "http://orchestration-service:8004";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(cfg.GetValue<int?>("ArbiterValidation:TimeoutSeconds") ?? 3);
        })
        .ConfigureSsrfPin("arbiter-validation", allowedHostSuffixes: new[] { "orchestration-service", "localhost" });
```

Leggi la firma reale di `ConfigureSsrfPin` e il valore usato da un sink esistente a host fisso (`grep -n "ConfigureSsrfPin(" apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs -A4`) e replica la forma. Aggiungi `"ArbiterValidation": { "TimeoutSeconds": 3 }` in `appsettings.json`.

- [ ] **Step 4: Esegui e verifica che passi** — `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~ArbiterValidationClientTests"` → PASS. Esegui anche il gate SSRF se esiste (`--filter "FullyQualifiedName~Ssrf"`).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/Validation apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/External/ArbiterValidationClient.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs apps/api/src/Api/appsettings.json apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/ArbiterValidationClientTests.cs
git commit -m "feat(kb): client HTTP verso /validate con timeout e verdetto Unknown"
```

---

### Task 4: Il validatore come strumento della disputa — SOSPESO fino alla fetta 8

> ⏸️ **Sospeso il 2026-10-15 da ADR-098.** Il consumatore C# del verdetto formale è la disputa sulle regole della partita dal vivo, che oggi chiama il modello senza recupero (`SubmitRuleDisputeCommandHandler`, in `GameManagement`) e che la fetta 8 di ADR-095 (#4171) porta sulla pipeline. Innestare il validatore nel gestore di oggi vorrebbe dire riscriverlo con la fetta: questo task si scrive lì, sul gestore della disputa sulla pipeline.
>
> Le due stesure precedenti sono superate: innestavano il validatore in `AskArbiterCommandHandler` (che #4386 toglie) e poi in `ValidateMoveCommandHandler` (che nessuna pagina chiama e che ADR-098 toglie con tutta la rotta `/agents/arbitro/*`).

Cosa deve restare vero quando il task si riscrive, dalla stesura precedente:

- su un gioco con claim v3 pubblicati la disputa chiama `IArbiterValidationClient` (Task 3); senza card o senza claim non lo chiama e il verdetto formale è `Unknown`;
- il verdetto formale entra nel prompt del compito «arbitro» solo se diverso da `Unknown`, con l'istruzione di spiegarlo e non contraddirlo;
- la risposta porta `FormalVerdict`, `AppliedRuleIds`, `ViolatedRuleIds`, `FormalConfidence` (sempre `null` in questa versione) e `FormalExplanation`;
- timeout o errore del validatore ⇒ `Unknown`, una metrica, e nessun 500 (Review Focus 4);
- prima di chiamare `IMechanicCardProvider.GetActiveCardAsync(sharedGameId)` va verificato che l'id del gioco della sessione sia lo `SharedGameId` atteso: con l'id sbagliato il provider risponde `null` e i test con il mock passano lo stesso.

---

### Task 5: Confidence onesta nell'Arbitro Python legacy e ritiro del doppione C#

**Files:**
- Modify: `apps/orchestration-service/src/application/arbitro_agent.py:192` (e il fallback a `0.75`)
- Modify: `apps/orchestration-service/tests/test_arbitro_agent.py` (asserzioni su `confidence_score`)
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Services/MoveValidationDomainService.cs` (attributo `[Obsolete("ADR-092: verdict moves to orchestration-service /validate")]` sulla classe)
- Test: aggiornare i test che costruiscono `MoveValidationDomainService` con `#pragma warning disable CS0618` locale, se il build tratta gli warning come errori (`grep -n "TreatWarningsAsErrors" apps/api/src/Api/Api.csproj`).

**Interfaces:** nessuna nuova.

- [ ] **Step 1: Scrivi/aggiorna il test Python**

In `tests/test_arbitro_agent.py` aggiungi:

```python
@pytest.mark.asyncio
async def test_confidence_is_none_when_no_rule_was_evaluated(arbitro_agent, base_state):
    base_state.move_notation = "Zz9"  # nessuna regola combacia
    base_state.applied_rule_ids = []
    out = await arbitro_agent._generate_explanation_node(base_state)
    assert out["confidence_score"] is None
```

e, dove un test esistente asserisce `0.95`/`0.90`/`0.75`, sostituisci con: `None` quando `applied_rule_ids` è vuoto, altrimenti `is None` resta valido finché non esiste una misura (ADR-092 §2). Documenta nel docstring del test.

- [ ] **Step 2: Esegui e verifica che fallisca** — `pytest tests/test_arbitro_agent.py -q` → FAIL sul nuovo test.

- [ ] **Step 3: Implementa**

In `_generate_explanation_node`: sostituisci `"confidence_score": 0.95 if state.is_valid else 0.90` con `"confidence_score": None` e il fallback `0.75` con `None`; aggiungi il commento `# ADR-092 §2: no fixed confidence; a real measure (golden-set agreement) will replace None.` Verifica che `ExecuteWorkflowResponse.confidence` accetti `None`: in `schemas.py` cambia `confidence: float = Field(ge=0.0, le=1.0, ...)` in `confidence: Optional[float] = Field(default=None, ge=0.0, le=1.0, ...)`, e lato C# `TutorQueryCommandHandler.OrchestrationResponse.Confidence` in `double?` con il consumatore `TutorQueryResponse` aggiornato (`grep -rn "TutorQueryResponse(" apps/api --include=*.cs`).

In C#, marca `MoveValidationDomainService` con `[Obsolete("ADR-092: the formal verdict is produced by orchestration-service POST /validate; this keyword heuristic is scheduled for removal.")]`.

- [ ] **Step 4: Esegui le suite** — `pytest -q` → PASS; `dotnet build` senza nuovi errori; `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~MoveValidation|FullyQualifiedName~TutorQuery"` → PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/orchestration-service/src/application/arbitro_agent.py apps/orchestration-service/src/api/schemas.py apps/orchestration-service/tests/test_arbitro_agent.py apps/api/src/Api/BoundedContexts/GameManagement/Domain/Services/MoveValidationDomainService.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/TutorQueryCommandHandler.cs
git commit -m "fix(arbitro): confidence mai fissa e doppione C# marcato obsoleto (ADR-092)"
```

---

### Task 6: Accensione su staging e verifica

**Files:**
- Modify: `infra/compose.staging.tutor.yml` (già opt-in: verificare che `ORCHESTRATION_SERVICE_URL` sia passato all'API nello stesso override), `docs/for-developers/operations/operations-manual.md` (sezione «Orchestration service: /validate»)

- [ ] **Step 1: Contratto da entrambi i lati**

Aggiungi `apps/orchestration-service/tests/test_validate_contract.py` che carica `apps/api/tests/Api.Tests/Fixtures/arbiter-validate-request.json` (crealo: la stessa request del test C# `ValidateAsync_PostsSnakeCaseContract_AndParsesResult`, salvata come file) e la posta a `/validate` con `TestClient`, asserendo 200. Lato C#, `ArbiterValidationClientTests` legge lo stesso file e lo confronta con `handler.LastBody` dopo normalizzazione JSON (`JsonNode.Parse(...).ToJsonString()` su entrambi). Un cambio di contratto rompe entrambi.

- [ ] **Step 2: Avvio su staging**

Con il profilo opt-in (`compose.staging.tutor.yml`), dopo il deploy: `curl -s https://<staging>/api/v1/health` deve mostrare `orchestration` sano; `pwsh -c "docker logs meepleai-orchestration --tail=50"` deve mostrare l'avvio; una disputa aperta dalla scheda «Arbitro» di una partita, su un gioco con card v3 pubblicata, deve produrre un `formalVerdict` diverso da `Unknown` (piano gemello Task 10). Si può verificare solo dopo la fetta 8 e il Task 4. Salva la risposta nel PR.

- [ ] **Step 3: Documenta** la sezione nel manuale operativo: variabili (`ORCHESTRATION_SERVICE_URL`, `ArbiterValidation:TimeoutSeconds`), metriche (`validate_verdict_*_total`, `meepleai_arbiter_validation_unavailable_total`), comportamento `Unknown`. Commit `docs(ops): orchestration /validate`.

---

## Self-Review

1. **Copertura**: spec §6 contratto → Task 2–3; kernel → Task 1; `confidence` null → Task 1, 2, 5; C# client + `Unknown` su timeout → Task 3–4; prerequisiti operativi ADR-092 §6 (avvio su staging, metriche) → Task 6; checkpointer LangGraph (ADR-092 §6) **non** è in questo piano: è una riga di roadmap separata (`langgraph-checkpoint-postgres`), dichiarata fuori scope qui perché non serve a `/validate`.
2. **Segnaposto**: Task 4 Step 1 e Task 6 Step 1 lasciano helper da completare con istruzioni precise e riferimenti a file esistenti; nessun TBD.
3. **Tipi**: `ArbiterValidationRequest/Result`, `IArbiterValidationClient`, `ValidateRequest/Response`, `Rule/Trigger/Verdict/resolve` coerenti fra task; il DTO `PublishedMechanicCardClaimDto` con i parametri opzionali viene dal piano gemello Task 7.
4. **Review Focus**: 1 → Task 1 `conflict_same_level_is_unknown`; 2 → Task 1 `override_only_counts_when_overrider_applies`; 3 → Task 1 `examples_and_clarifications_never_decide`; 4 → Task 3 `ValidateAsync_Timeout_ReturnsUnknownWithoutThrowing` + Task 4 `Validate_Timeout_YieldsUnknown`; 5 → Task 1 `wildcard_trigger_applies_to_any_move`.

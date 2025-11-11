# MeepleAI Testing Strategy

**Status**: Approved for Phase 1 Implementation
**Version**: 1.0
**Date**: 2025-01-15
**Owner**: QA & Engineering Team

---

## Testing Philosophy

### Core Principles

1. **"One Mistake Ruins Session"**: Accuracy bar significantly higher than typical AI → Quality gate: 95%+ accuracy target
2. **Test Pyramid**: 70% unit, 20% integration, 5% quality tests, 5% E2E
3. **Shift-Left Testing**: Catch issues early (unit tests) vs late (E2E)
4. **Risk-Based Prioritization**: 60% effort on high-risk areas (hallucination prevention, citation accuracy)
5. **Continuous Validation**: Weekly regression on golden dataset, monthly model re-evaluation

---

## Test Pyramid Architecture

```
           /\
          /  \     E2E Tests (5%)
         /____\    - User journey scenarios
        /      \   - Playwright browser automation
       /________\  - Mobile responsive validation

      /          \
     /  Quality   \ Quality Tests (5%)
    /______________\ - 5-metric framework
   /                \ - Accuracy, Hallucination, Confidence
  /                  \ - Citation, Latency benchmarks
 /____________________\

/                      \
/ Integration Tests     \ Integration Tests (20%)
/_________________________\ - RAG pipeline end-to-end
                            - Multi-service interaction
                            - Testcontainers (Weaviate, PostgreSQL, Redis)

___________________________________________________
|                                                 |
|              Unit Tests (70%)                   |
|_________________________________________________|
- PDF processing, chunking, embeddings
- Validation layers, confidence scoring
- 90%+ code coverage requirement
```

---

## Test Layers

### Layer 1: Unit Tests (70% of tests, <5s execution)

**Coverage Target**: ≥90% code coverage

**Scope**:
- PDF processing functions (LLMWhisperer, SmolDocling, dots.ocr adapters)
- Chunking logic (sentence-aware boundary detection)
- Embedding generation (multilingual-e5-large adapter)
- Citation extraction (page number parser)
- Confidence scoring calculation
- Validation layers (multi-model consensus, forbidden keywords)

**Framework**:
- **Python**: pytest + pytest-cov (coverage)
- **TypeScript**: Jest (frontend components)

**Example Test** (Python):
```python
# tests/unit/test_chunking.py
import pytest
from services.chunking import TextChunkingService

def test_semantic_chunking_preserves_sentences():
    # Given
    text = "Questa è una frase. Questa è un'altra frase. Questa è la terza."
    service = TextChunkingService(max_chunk_size=40, overlap_ratio=0.20)

    # When
    chunks = service.chunk(text, game_id="test-game")

    # Then
    assert len(chunks) == 2
    assert "Questa è una frase." in chunks[0].text
    assert "Questa è un'altra frase." in chunks[1].text
    # Verify no chunk splits mid-sentence
    for chunk in chunks:
        assert not chunk.text.endswith(" ")
        assert chunk.text[0].isupper()  # Starts with capital
```

**Run Command**:
```bash
# Backend
cd apps/api
pytest tests/unit/ --cov=src --cov-report=html

# Frontend
cd apps/web
pnpm test --coverage
```

---

### Layer 2: Integration Tests (20%, <2min execution)

**Scope**:
- RAG pipeline end-to-end (PDF upload → indexing → query → response)
- Multi-model validation (GPT-4 + Claude consensus)
- Vector search + keyword search → RRF fusion
- Cache hit/miss scenarios (Redis semantic cache)
- Circuit breaker state transitions (LLM API failures)

**Framework**: pytest + testcontainers (Docker containers for dependencies)

**Example Test**:
```python
# tests/integration/test_rag_pipeline.py
import pytest
from testcontainers.postgres import PostgresContainer
from testcontainers.compose import DockerCompose
from services.qa import QuestionAnsweringService

@pytest.fixture(scope="module")
def containers():
    compose = DockerCompose("docker-compose.test.yml")
    compose.start()
    yield compose
    compose.stop()

def test_rag_pipeline_with_ambiguous_question(containers):
    # Given: Terraforming Mars rulebook indexed
    qa_service = QuestionAnsweringService()
    question = "Posso usare Standard Projects dopo aver passato?"
    game_id = "terraforming-mars"

    # When
    response = qa_service.ask(question, game_id, language="it")

    # Then
    assert response.confidence >= 0.70
    assert "pag. 8" in str(response.citations)
    assert "FAQ 2023 Q12" in str(response.sources)
    assert "No, Standard Projects" in response.answer
    # Verify vector search was called
    assert qa_service.retrieval_service.vector_search_called
    # Verify RRF fusion applied (Phase 3+)
    if qa_service.hybrid_search_enabled:
        assert qa_service.retrieval_service.rrf_fusion_applied
```

**Run Command**:
```bash
cd apps/api
pytest tests/integration/ --verbose
```

---

### Layer 3: Quality Tests (5%, ~15min execution)

**Scope**: 5-metric testing framework validating AI quality

**5 Core Metrics**:

| Metric | Threshold | Measurement Method |
|--------|-----------|-------------------|
| **Accuracy** | ≥80% | Keyword matching on golden dataset (1000 Q&A pairs) |
| **Hallucination Rate** | ≤10% | Forbidden keywords detection (500+ blocklist) |
| **Avg Confidence** | ≥0.70 | RAG retrieval quality scoring |
| **Citation Correctness** | ≥80% | Page number + text snippet validation |
| **Avg Latency** | ≤3000ms | P95 response time measurement |

**Golden Dataset Structure**:
```json
{
  "game_id": "terraforming-mars",
  "language": "it",
  "test_cases": [
    {
      "id": "tm_001",
      "question": "Posso usare Standard Projects dopo aver passato?",
      "expected_answer_keywords": ["No", "durante il proprio turno", "prima di passare"],
      "expected_citations": [{"page": 8, "snippet_contains": "Durante il proprio turno"}],
      "difficulty": "easy",
      "category": "gameplay"
    },
    {
      "id": "tm_002",
      "question": "Cosa succede se finisco i meeple durante la partita?",
      "expected_answer_keywords": ["scenario specifico", "regolamento avanzato"],
      "expected_citations": [{"page": 15, "snippet_contains": "miniature esaurite"}],
      "difficulty": "hard",
      "category": "edge_case"
    }
  ]
}
```

**Example Quality Test**:
```python
# tests/quality/test_accuracy_metric.py
import pytest
from services.qa import QuestionAnsweringService
from tests.fixtures.golden_dataset import load_golden_dataset

def test_accuracy_on_golden_dataset():
    # Given
    qa_service = QuestionAnsweringService()
    golden_dataset = load_golden_dataset()  # 1000 Q&A pairs

    # When
    results = []
    for test_case in golden_dataset:
        response = qa_service.ask(
            question=test_case["question"],
            game_id=test_case["game_id"],
            language=test_case["language"]
        )
        # Calculate accuracy: keyword match
        accuracy = calculate_keyword_accuracy(
            response.answer,
            test_case["expected_answer_keywords"]
        )
        results.append(accuracy)

    # Then
    avg_accuracy = sum(results) / len(results)
    assert avg_accuracy >= 0.80, f"Accuracy {avg_accuracy:.2%} below 80% threshold"

    # P95 accuracy (95% of queries should be ≥70% accurate)
    p95_accuracy = sorted(results)[int(len(results) * 0.95)]
    assert p95_accuracy >= 0.70, f"P95 accuracy {p95_accuracy:.2%} below 70%"

def calculate_keyword_accuracy(answer: str, expected_keywords: list[str]) -> float:
    """Calculate accuracy as % of expected keywords present in answer."""
    answer_lower = answer.lower()
    matches = sum(1 for keyword in expected_keywords if keyword.lower() in answer_lower)
    return matches / len(expected_keywords) if expected_keywords else 0.0
```

**Run Command**:
```bash
cd apps/api
pytest tests/quality/ --verbose --timeout=1200  # 20 min timeout
```

---

### Layer 4: E2E Tests (5%, ~5min execution)

**Scope**: User journey scenarios via Playwright browser automation

**Test Scenarios**:
1. **Happy Path**: Upload rulebook → Ask question → View citation → Provide feedback
2. **Uncertainty Handling**: Ask edge case question → Receive explicit uncertainty → View suggestions
3. **Mobile Responsive**: Perform query on 320px, 768px, 1920px viewports
4. **Error Recovery**: LLM API failure → Fallback message displayed → Retry successful

**Framework**: Playwright (TypeScript)

**Example E2E Test**:
```typescript
// tests/e2e/qa-journey.spec.ts
import { test, expect } from '@playwright/test';

test('User asks ambiguous question and receives validated answer with citation', async ({ page }) => {
  // Given: Navigate to Terraforming Mars game page
  await page.goto('https://app.meepleai.dev/games/terraforming-mars');

  // When: User asks question
  await page.fill('#question-input', 'Posso usare Standard Projects dopo aver passato?');
  await page.click('#ask-button');

  // Then: Streaming response appears
  await expect(page.locator('#response-streaming')).toBeVisible({ timeout: 5000 });

  // Then: Answer contains expected content
  await expect(page.locator('#response-answer')).toContainText('No, Standard Projects');

  // Then: Confidence badge displayed
  const confidenceBadge = page.locator('#confidence-badge');
  await expect(confidenceBadge).toBeVisible();
  const confidenceText = await confidenceBadge.textContent();
  const confidence = parseFloat(confidenceText!);
  expect(confidence).toBeGreaterThanOrEqual(0.70);

  // Then: Citation is clickable
  await page.click('a[href*="pag-8"]');
  await expect(page.locator('#pdf-viewer')).toBeVisible({ timeout: 3000 });

  // Then: PDF opens to correct page
  const pdfPage = page.locator('#pdf-page-number');
  await expect(pdfPage).toHaveText('8');

  // Then: Feedback prompt appears
  await expect(page.locator('#feedback-thumbs-up')).toBeVisible();
  await expect(page.locator('#feedback-thumbs-down')).toBeVisible();
});

test('Mobile responsive: Query from smartphone viewport', async ({ page }) => {
  // Given: Set mobile viewport
  await page.setViewportSize({ width: 375, height: 667 });  // iPhone SE
  await page.goto('https://app.meepleai.dev/games/wingspan');

  // When: Ask question
  await page.fill('#question-input', 'Posso attivare poteri durante setup?');
  await page.click('#ask-button');

  // Then: Response renders correctly on mobile
  await expect(page.locator('#response-answer')).toBeVisible();

  // Then: Citation links are tappable (min 44x44px touch target)
  const citationLink = page.locator('a[href*="pag"]').first();
  const box = await citationLink.boundingBox();
  expect(box!.width).toBeGreaterThanOrEqual(44);
  expect(box!.height).toBeGreaterThanOrEqual(44);
});
```

**Run Command**:
```bash
cd apps/web
pnpm test:e2e          # Headless mode
pnpm test:e2e:ui       # With browser UI (debugging)
```

---

## Test Data Strategy

### Golden Dataset (1000 Q&A Pairs)

**Composition**:
- **10 Games**: Terraforming Mars, Wingspan, Azul, Scythe, Ticket to Ride, Catan, Pandemic, 7 Wonders, Agricola, Splendor
- **100 Q&A pairs per game** (manually annotated by board game experts)
- **Difficulty Distribution**: 55% easy, 25% medium, 20% hard
- **Category Distribution**: 40% gameplay, 25% setup, 20% endgame, 15% edge cases

**Example Entry**:
```json
{
  "id": "tm_042",
  "game_id": "terraforming-mars",
  "game_name": "Terraforming Mars",
  "language": "it",
  "question": "Cosa succede se due giocatori raggiungono contemporaneamente 8 città?",
  "expected_answer_keywords": ["milestone", "primo giocatore", "simultaneo", "turno"],
  "expected_citations": [
    {"page": 11, "snippet_contains": "milestone viene assegnato al primo giocatore"}
  ],
  "forbidden_keywords": ["condiviso", "pareggio", "entrambi vincono"],
  "difficulty": "medium",
  "category": "edge_case",
  "annotated_by": "expert_marco_rossi",
  "annotated_at": "2025-01-10T10:00:00Z"
}
```

**Storage**: `tests/data/golden_dataset.json` (version controlled)

---

### Synthetic Adversarial Dataset (100+ Test Cases)

**Purpose**: Trigger hallucination detection, edge case handling

**Categories**:
1. **Fabricated Rules** (30 tests): Questions about non-existent game elements
   - "In Terraforming Mars, quante volte posso usare il Will of the West?" (invented item)
   - Expected: "Non ho informazioni su 'Will of the West'"

2. **Ambiguous Questions** (30 tests): Multiple valid interpretations
   - "Posso giocare una carta?" (which card? when? which game phase?)
   - Expected: Disambiguation request or low confidence

3. **Cross-Game Confusion** (20 tests): Mix elements from different games
   - "In Wingspan, posso terraformare Marte?" (wrong game)
   - Expected: "Terraforming Mars è un gioco diverso da Wingspan"

4. **Edge Cases** (20 tests): Rare scenarios from rulebook edge cases section
   - "Cosa succede se finiscono tutti i meeple disponibili?"
   - Expected: Specific rulebook section cited (varies by game)

**Storage**: `tests/data/adversarial_dataset.json`

---

## Quality Gates

### Pull Request Gates (Automated in CI)

**Required Checks** (must pass before merge):
- ✅ Unit tests: 100% pass + ≥90% coverage
- ✅ Integration tests: 100% pass
- ✅ Linting: flake8 (Python), ESLint (TypeScript) zero errors
- ✅ Type checking: mypy (Python), tsc --noEmit (TypeScript) zero errors
- ✅ Security scan: Snyk, Trivy (no critical/high vulnerabilities)

**GitHub Actions Workflow**:
```yaml
# .github/workflows/ci.yml
name: CI

on: [push, pull_request]

jobs:
  backend-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-python@v4
        with:
          python-version: '3.11'
      - run: pip install -r requirements.txt
      - run: pytest tests/unit/ --cov=src --cov-report=xml
      - run: pytest tests/integration/ --verbose
      - uses: codecov/codecov-action@v3

  frontend-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-node@v3
        with:
          node-version: '20'
      - run: pnpm install
      - run: pnpm lint
      - run: pnpm typecheck
      - run: pnpm test --coverage
```

---

### Pre-Production Gates (Manual + Automated)

**Required Before Deploy**:
1. ✅ All PR gates passing
2. ✅ Quality tests (5-metric framework) passing:
   - Accuracy ≥80%
   - Hallucination ≤10%
   - Confidence ≥0.70
   - Citation ≥80%
   - Latency ≤3000ms P95
3. ✅ E2E tests: 100% pass
4. ✅ Load testing: 100 RPS sustained for 10 min, error rate <1%
5. ✅ Security scan: No critical/high vulnerabilities
6. ✅ Manual QA: 10 games smoke tested by QA team

**Load Testing** (Phase 2+):
```bash
# Using Locust (Python load testing tool)
locust -f tests/load/locustfile.py \
       --host https://staging-api.meepleai.dev \
       --users 100 \
       --spawn-rate 10 \
       --run-time 10m
```

---

## Testing Tools & Infrastructure

### Testing Stack

| Category | Tool | Purpose |
|----------|------|---------|
| **Unit Testing** | pytest, Jest | Fast, isolated tests |
| **Coverage** | pytest-cov, Jest --coverage | Track code coverage |
| **Integration** | pytest + testcontainers | Docker-based integration tests |
| **E2E** | Playwright | Browser automation |
| **Load Testing** | Locust (Phase 2+) | Performance benchmarking |
| **Security** | Snyk, Trivy | Dependency + container scanning |
| **Mocking** | pytest-mock, Jest mock | Isolate dependencies |
| **Fixtures** | pytest fixtures | Test data management |

### Test Infrastructure

**Local Development**:
```bash
# Start test dependencies
docker compose -f docker-compose.test.yml up -d

# Run tests
pytest tests/

# Stop dependencies
docker compose -f docker-compose.test.yml down
```

**CI/CD (GitHub Actions)**:
- Testcontainers automatically start/stop Docker services
- Parallel job execution (unit + integration + frontend)
- Artifact upload (coverage reports, test results)
- Slack notifications on failure

---

## Test Maintenance Strategy

### Weekly Activities

1. **Regression Testing**: Run quality tests on golden dataset (1000 Q&A)
2. **Failure Analysis**: Review failed tests, add to golden/adversarial datasets
3. **Coverage Review**: Identify untested code paths, add unit tests

### Monthly Activities

1. **Golden Dataset Expansion**: Add 50-100 new Q&A pairs from production queries
2. **Adversarial Dataset Update**: Add user-reported hallucinations to test suite
3. **Model Re-Evaluation**: Re-run quality tests after LLM model updates

### Quarterly Activities

1. **Test Strategy Review**: Assess test pyramid balance (70/20/5/5 still optimal?)
2. **Performance Baseline**: Update latency/throughput benchmarks
3. **Test Debt Sprint**: Refactor flaky tests, improve test performance

---

## Troubleshooting Common Test Failures

### "Accuracy below 80% threshold"

**Possible Causes**:
- LLM model regression (OpenAI/Anthropic updated model)
- Golden dataset outdated (rulebook errata not reflected)
- Retrieval quality degradation (embedding model changed)

**Resolution**:
1. Check LLM provider status (OpenAI/Anthropic API versioning)
2. Review failed test cases: `pytest tests/quality/test_accuracy.py --verbose`
3. Update golden dataset if rulebook errata published
4. Re-evaluate embedding model performance

---

### "Integration test timeout"

**Possible Causes**:
- Docker containers slow to start (Weaviate indexing)
- Network latency (LLM API calls)
- Database connection pool exhausted

**Resolution**:
1. Increase timeout: `pytest tests/integration/ --timeout=300`
2. Check Docker logs: `docker compose logs weaviate`
3. Verify connection pool settings (PostgreSQL max_connections)

---

### "E2E test flaky"

**Possible Causes**:
- Race conditions (async operations)
- Network flakiness (API latency variability)
- Element selectors outdated (frontend refactor)

**Resolution**:
1. Add explicit waits: `await expect(element).toBeVisible({ timeout: 5000 })`
2. Retry on failure: `test.retries(2)`
3. Update selectors: Use `data-testid` attributes (stable)
4. Isolate tests: Ensure no shared state between tests

---

## Appendix: Test Execution Commands

```bash
# Backend Tests
cd apps/api

# Unit tests (fast, <5s)
pytest tests/unit/

# Integration tests (medium, <2min)
pytest tests/integration/

# Quality tests (slow, ~15min)
pytest tests/quality/

# All tests
pytest

# With coverage
pytest --cov=src --cov-report=html

# Single test file
pytest tests/unit/test_chunking.py -v

# Single test function
pytest tests/unit/test_chunking.py::test_semantic_chunking_preserves_sentences -v

# Frontend Tests
cd apps/web

# Unit tests
pnpm test

# With coverage
pnpm test --coverage

# E2E tests (headless)
pnpm test:e2e

# E2E tests (with browser UI)
pnpm test:e2e:ui

# E2E tests (specific file)
pnpm test:e2e tests/e2e/qa-journey.spec.ts

# Combined (All)
cd apps/api && pytest && cd ../web && pnpm test && pnpm test:e2e
```

---

**Document Metadata**:
- **Version**: 1.0
- **Last Updated**: 2025-01-15
- **Next Review**: 2025-04-15 (quarterly)
- **Approvers**: QA Lead, Engineering Lead
- **Status**: APPROVED for Phase 1 Implementation

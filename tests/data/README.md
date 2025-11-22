# Test Data for Quality Testing

This directory contains datasets for validating RAG (Retrieval-Augmented Generation) quality metrics.

## Overview

**Reference**: See [Testing Strategy](../../docs/02-development/testing/testing-strategy.md) for complete testing pyramid architecture and quality gates.

## Datasets

### 1. Golden Dataset (`golden_dataset.json`)

**Purpose**: Validate AI quality metrics on real-world board game questions

**Composition**:
- **1000 Q&A pairs** across 10 popular board games
- Manually annotated by board game experts
- Version controlled for regression testing

**Games Included**:
1. Terraforming Mars
2. Wingspan
3. Azul
4. Scythe
5. Ticket to Ride
6. Catan
7. Pandemic
8. 7 Wonders
9. Agricola
10. Splendor

**Difficulty Distribution**:
- Easy: 55% (550 test cases)
- Medium: 25% (250 test cases)
- Hard: 20% (200 test cases)

**Category Distribution**:
- Gameplay: 40% (400 test cases)
- Setup: 25% (250 test cases)
- Endgame: 20% (200 test cases)
- Edge Cases: 15% (150 test cases)

**Quality Thresholds**:
- **Accuracy**: ≥80% (keyword matching on expected answers)
- **Hallucination Rate**: ≤10% (forbidden keywords detection)
- **Avg Confidence**: ≥0.70 (RAG retrieval quality scoring)
- **Citation Correctness**: ≥80% (page number + snippet validation)
- **Avg Latency**: ≤3000ms (P95 response time)

### 2. Adversarial Dataset (`adversarial_dataset.json`)

**Purpose**: Trigger hallucination detection, edge case handling, and ambiguity resolution

**Composition**:
- **100+ test cases** designed to challenge the RAG system
- Focus on failure modes and boundary conditions

**Categories**:

1. **Fabricated Rules** (30 tests)
   - Questions about non-existent game elements
   - Expected: System refuses to answer with low confidence
   - Example: "In Terraforming Mars, how does the Will of the West card work?" (invented card)

2. **Ambiguous Questions** (30 tests)
   - Questions with multiple valid interpretations
   - Expected: Disambiguation request or low confidence
   - Example: "Can I play a card?" (which card? when? which phase?)

3. **Cross-Game Confusion** (20 tests)
   - Mix elements from different games
   - Expected: Detection of game context mismatch
   - Example: "In Wingspan, can I terraform Mars?" (wrong game)

4. **Edge Cases** (20 tests)
   - Rare scenarios from rulebook appendices
   - Expected: Specific rulebook section cited
   - Example: "What happens if all meeples run out?"

**Expected Behaviors**:
- **Hallucination Prevention**: ≥90% rate (refuse fabricated questions)
- **Disambiguation**: ≥80% rate (ask for clarification on ambiguous questions)
- **Cross-Game Detection**: ≥85% rate (detect game context mismatches)
- **Edge Case Accuracy**: ≥70% (find and cite rare scenarios)

## Usage

### Running Quality Tests

```bash
# Backend (C#/.NET)
cd apps/api
dotnet test --filter "FullyQualifiedName~RagEvaluationIntegrationTests"

# Python (if applicable)
cd apps/api
pytest tests/quality/ --verbose --timeout=1200
```

### Adding New Test Cases

1. **Golden Dataset**: Add new Q&A pairs for games in `golden_dataset.json`
   - Follow the existing JSON structure
   - Include expected keywords, citations, and forbidden keywords
   - Annotate with expert name and timestamp

2. **Adversarial Dataset**: Add new adversarial test cases in `adversarial_dataset.json`
   - Categorize appropriately (fabricated, ambiguous, cross-game, edge case)
   - Define expected behavior and confidence thresholds

### Test Case Structure

#### Golden Dataset Test Case

```json
{
  "id": "tm_042",
  "question": "Cosa succede se due giocatori raggiungono contemporaneamente 8 città?",
  "expected_answer_keywords": ["milestone", "primo giocatore", "simultaneo", "turno"],
  "expected_citations": [
    {
      "page": 11,
      "snippet_contains": "milestone viene assegnato al primo giocatore"
    }
  ],
  "forbidden_keywords": ["condiviso", "pareggio", "entrambi vincono"],
  "difficulty": "medium",
  "category": "edge_case",
  "annotated_by": "expert_marco_rossi",
  "annotated_at": "2025-01-10T10:00:00Z"
}
```

#### Adversarial Dataset Test Case

```json
{
  "id": "fab_001",
  "game_id": "terraforming-mars",
  "question": "In Terraforming Mars, quante volte posso usare il Will of the West?",
  "expected_response_contains": ["Non ho informazioni", "Will of the West", "non trovato"],
  "expected_confidence": "<0.50",
  "forbidden_keywords": ["puoi usare", "si può utilizzare"],
  "notes": "Will of the West is an invented card name - should detect hallucination",
  "annotated_by": "expert_marco_rossi",
  "annotated_at": "2025-01-12T10:00:00Z"
}
```

## Quality Metrics Calculation

### 1. Accuracy

**Formula**: `matches / total_keywords`

```python
def calculate_accuracy(answer: str, expected_keywords: list[str]) -> float:
    answer_lower = answer.lower()
    matches = sum(1 for kw in expected_keywords if kw.lower() in answer_lower)
    return matches / len(expected_keywords) if expected_keywords else 0.0
```

### 2. Hallucination Rate

**Formula**: `forbidden_matches / total_answers`

```python
def calculate_hallucination_rate(answer: str, forbidden_keywords: list[str]) -> float:
    answer_lower = answer.lower()
    matches = sum(1 for kw in forbidden_keywords if kw.lower() in answer_lower)
    return matches / len(forbidden_keywords) if forbidden_keywords else 0.0
```

### 3. Citation Correctness

**Formula**: `correct_citations / total_citations`

```python
def validate_citation(citation: dict, expected: dict) -> bool:
    page_match = citation["page"] == expected["page"]
    snippet_match = expected["snippet_contains"].lower() in citation["snippet"].lower()
    return page_match and snippet_match
```

### 4. Latency (P95)

**Formula**: `sorted(latencies)[int(len(latencies) * 0.95)]`

```python
def calculate_p95_latency(latencies: list[float]) -> float:
    sorted_latencies = sorted(latencies)
    p95_index = int(len(sorted_latencies) * 0.95)
    return sorted_latencies[p95_index]
```

## Maintenance

### Weekly Activities
- Run quality tests on golden dataset (1000 Q&A)
- Review failed tests, add to golden/adversarial datasets
- Update test cases for rulebook errata

### Monthly Activities
- Expand golden dataset with 50-100 new Q&A pairs from production queries
- Add user-reported hallucinations to adversarial dataset
- Re-run quality tests after LLM model updates

### Quarterly Activities
- Review test pyramid balance (70/20/5/5 still optimal?)
- Update latency/throughput benchmarks
- Refactor flaky tests

## References

- [Testing Strategy](../../docs/02-development/testing/testing-strategy.md) - Complete testing pyramid architecture
- [Testing Guide](../../docs/02-development/testing/testing-guide.md) - Practical test writing guide
- [System Architecture](../../docs/01-architecture/overview/system-architecture.md) - Testing & Quality Assurance section

## Version History

- **v1.0** (2025-01-15): Initial golden and adversarial datasets created
  - 1000 Q&A pairs across 10 games (golden)
  - 100+ adversarial test cases (adversarial)

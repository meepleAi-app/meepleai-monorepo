# Golden Test Set Schemas

## qa-questions.jsonl

Each line is a JSON object with fields:

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique ID (e.g., `qa-001`) |
| `game` | string | Game slug (e.g., `tainted-grail`) — must match games in OCR validation |
| `game_id` | UUID string | UUID matching SharedGameCatalog entry |
| `question_it` | string | Question in Italian |
| `expected_answer_it` | string | Expected answer in Italian (publication-ready) |
| `expected_citations` | object | `{primary_pages: int[], match_policy: enum}` |
| `category` | enum | `setup` \| `combat` \| `narrative` \| `character` \| `items` \| `edge-cases` |
| `difficulty` | enum | `easy` \| `medium` \| `hard` |
| `expected_confidence` | enum | `high` \| `medium` \| `low` |

### Match policy enum

| Value | Behavior |
|-------|----------|
| `exact` | actual citations == expected exactly (page numbers and order) |
| `overlap_at_least_one` | intersection of actual ∩ expected is non-empty |
| `subset` | actual ⊆ expected (no extra pages cited) |
| `superset` | actual ⊇ expected (all expected pages present) |

## translation-paragraphs.jsonl

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique ID (e.g., `tr-001`) |
| `game` | string | Game slug |
| `game_id` | UUID string | SharedGameCatalog UUID |
| `source_lang` | enum | `en` \| `de` \| `fr` \| `es` \| `nl` \| `pt` |
| `paragraph_id` | string | Paragraph identifier (numbered or chapter-segment) |
| `source_text` | string | Original text |
| `expected_translation_it` | string | Expected Italian translation |
| `tone` | string | Tone descriptor (e.g., `fantasy-dramatic`, `family-mysterious`) |
| `glossary` | object | `{ [source_term]: italian_term }` map |
| `evaluation_criteria` | object | See below |

### evaluation_criteria

| Field | Type | Description |
|-------|------|-------------|
| `preserve_glossary` | bool | If true, glossary mappings MUST appear in translation |
| `preserve_tone` | string | Same as `tone` for verification |
| `max_bleu_delta` | float | Max acceptable BLEU score delta vs expected (0.0-1.0) |
| `min_human_score` | int | Minimum MOS (1-5) from human raters |

## Coverage requirements (full set, blocked on contractor)

- **Q&A**: 100 questions = 5 manuals × 20 questions each, distributed:
  - 60 easy (well-documented in manual, expected_confidence: high)
  - 30 medium (require multi-page synthesis)
  - 10 hard (edge cases, ambiguous, no-answer expected)
  - Per game: 4 setup + 5 combat + 4 narrative + 3 character + 2 items + 2 edge-cases
- **Translations**: 50 paragraphs distributed:
  - 20 narrative descriptive
  - 15 dialogue
  - 10 climactic/dramatic
  - 5 technical (rules embedded in narrative)

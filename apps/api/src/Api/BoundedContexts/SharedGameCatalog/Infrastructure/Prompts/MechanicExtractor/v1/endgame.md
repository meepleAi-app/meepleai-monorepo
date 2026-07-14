# End of Game & Scoring Section (v1.1.0)

Extract **when the game ends** and **how final scoring works**.

## Output schema

```json
{
  "endgame": [
    {
      "name": "string (short Italiano label, e.g. 'Trigger di fine partita', 'Punteggio maggioranze')",
      "description": "string (Italiano, reformulated, ≤280 chars)",
      "citations": [
        {
          "pdf_page": 12,
          "quote": "string ≤25 words",
          "chunk_id": "string (optional)"
        }
      ]
    }
  ]
}
```

## Field rules

- `name`: A short label for an end-of-game trigger or a scoring rule.
- `description`: Italiano, reformulated explanation of the end condition or how those points are scored.
- `citations`: At least one citation per entry.

## Extraction guidance

- Include (a) the **end-of-game trigger(s)** — what causes the game to end — and (b) the **final scoring rules** — how points are counted at the end.
- Between **1 and 10 entries**.
- The primary *win* condition (who wins) belongs to the Victory section; here focus on the end trigger and the scoring breakdown.
- Do not invent scoring not stated in the retrieved chunks.

# Phases Section (v1.0.0)

Extract the **turn-level or round-level phases** in the order they occur in a typical turn.

## Output schema

```json
{
  "phases": [
    {
      "name": "string (canonical phase name, Italiano or original if proper noun)",
      "description": "string (Italiano, reformulated, ≤240 chars)",
      "order": 1,
      "isOptional": false,
      "citations": [
        {
          "pdf_page": 8,
          "quote": "string ≤25 words",
          "chunk_id": "string (optional)"
        }
      ]
    }
  ]
}
```

## Field rules

- `name`: Short phase name. Use **Italiano for generic phases** ("Fase Azioni", "Fase Produzione", "Fase Pulizia"). Keep **proper nouns in original language** when the game uses a specific branded name (e.g. "Upkeep", "Mythos Phase").
- `description`: Italiano reformulated description of what happens in this phase.
- `order`: Integer, 1-based position in the sequence.
- `isOptional`: `true` if the phase is skipped in certain conditions; `false` if always executed.
- `citations`: At least one citation per phase.

## Extraction guidance

- Include between **0 and 10 phases**. Omit the field entirely if the game does not have a structured phase sequence (e.g. pure real-time games).
- Phases must be in execution order. Use `order` starting at 1.
- Do not include micro-steps within a phase as separate phases (e.g. "draw a card" is not a phase — it is part of an Upkeep phase).
- Round-level phases (that bracket all player turns) and turn-level phases (within a single player's turn) can both be listed — describe the scope in `description`.

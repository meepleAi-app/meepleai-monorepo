# Setup Section (v1.1.0)

Extract the **one-time pre-game setup steps** players perform BEFORE play begins.

## Output schema

```json
{
  "setup": [
    {
      "description": "string (Italiano, reformulated setup step, ≤240 chars)",
      "order": 1,
      "playerCountNote": "string (optional, Italiano — how the step changes with player count)",
      "citations": [
        {
          "pdf_page": 3,
          "quote": "string ≤25 words",
          "chunk_id": "string (optional)"
        }
      ]
    }
  ]
}
```

## Field rules

- `description`: Italiano, reformulated. A single preparation step (board layout, decks, starting resources/hand, player positions, tokens).
- `order`: Integer, 1-based, in the order the rulebook performs setup.
- `playerCountNote`: Optional. Only when the step differs by player count (e.g. "con 2 giocatori si rimuovono 20 tessere").
- `citations`: At least one citation per step.

## Extraction guidance

- Extract only the **one-time PRE-game preparation**. Do NOT include recurring turn/round phases — those belong to the Phases section.
- Between **0 and 15 steps**, in setup order (use `order` starting at 1).
- Keep each step atomic. Merge "shuffle the deck and deal 4 cards" into one step unless the rulebook treats them as separate.
- Omit the `setup` field entirely if the rulebook has no distinct setup (rare).

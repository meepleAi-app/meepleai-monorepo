# Victory Section (v1.0.0)

Extract the **victory conditions** exactly as described by the rulebook. Do not invent alternatives.

## Output schema

```json
{
  "victory": {
    "primary": "string (Italiano, reformulated, ≤280 chars)",
    "alternatives": [
      "string (Italiano, reformulated, ≤280 chars)"
    ],
    "isPointBased": true,
    "targetPoints": 15,
    "citations": [
      {
        "pdf_page": 12,
        "quote": "string ≤25 words",
        "chunk_id": "string (optional)"
      }
    ]
  }
}
```

## Field rules

- `primary`: The main victory condition in Italiano, reformulated. Example: *"Vince il giocatore con il maggior numero di punti vittoria al termine dell'ultimo round."*
- `alternatives`: Array of 0+ additional winning conditions **explicitly stated** in the rulebook (e.g. instant-win conditions, tiebreakers that decide the game). Omit if none are described.
- `isPointBased`: `true` if victory is decided by accumulated points; `false` for objective/last-player-standing/elimination games.
- `targetPoints`: Integer threshold if the game ends when a player reaches a specific point total. Omit otherwise.
- `citations`: At least one citation covering `primary`. Add more when alternatives or `targetPoints` come from different pages.

## Failure modes

- ❌ Do not infer tiebreakers from generic board-game convention — cite them from the rulebook.
- ❌ Do not translate "most points" into a specific point target unless the rulebook states a number.
- ❌ Do not add alternatives from your own imagination.

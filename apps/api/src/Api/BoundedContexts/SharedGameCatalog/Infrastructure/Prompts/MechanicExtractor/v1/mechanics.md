# Mechanics Section (v1.0.0)

Extract the **core design mechanics** the rulebook describes, using short canonical names (not paragraphs).

## Output schema

```json
{
  "mechanics": [
    {
      "name": "string (short canonical name, e.g. 'Worker Placement')",
      "description": "string (1-2 sentences, Italiano, reformulated, ≤280 chars)",
      "citations": [
        {
          "pdf_page": 5,
          "quote": "string ≤25 words",
          "chunk_id": "string (optional)"
        }
      ]
    }
  ]
}
```

## Field rules

- `name`: Use **short canonical mechanic names** from the board-game design vocabulary (Worker Placement, Deck Building, Area Control, Set Collection, Tile Laying, Hand Management, Drafting, Auction, etc.). Keep names in English — they are canonical domain terms.
- `description`: A short Italiano sentence explaining how the mechanic is used in **this specific game**. Reformulate, do not transcribe.
- `citations`: Each mechanic must have at least one citation from the chunks where the mechanic is described.

## Extraction guidance

- List between **2 and 10 mechanics**. Do not invent mechanics not supported by the chunks.
- Prefer fewer well-cited mechanics over many weakly-supported ones.
- If two chunks describe the same mechanic with different framings, merge into one entry with both citations.
- Do not include meta/structural elements (e.g. "turn order", "end game") as mechanics — those belong in the Phases or Victory sections.

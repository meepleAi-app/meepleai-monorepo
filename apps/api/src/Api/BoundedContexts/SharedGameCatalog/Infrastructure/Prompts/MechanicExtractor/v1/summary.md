# Summary Section (v1.0.0)

Extract a **neutral, reformulated descriptive summary** of the game's core identity from the retrieved rulebook chunks.

## Output schema

```json
{
  "summary": {
    "text": "string (100-500 chars, Italiano, reformulated)",
    "playerCountMin": 1,
    "playerCountMax": 4,
    "playTimeMinutes": 60,
    "minAge": 10,
    "citations": [
      {
        "pdf_page": 1,
        "quote": "string ≤25 words",
        "chunk_id": "string (optional)"
      }
    ]
  }
}
```

## Field rules

- `text`: One or two sentences describing what the game is about and what players do. **Reformulate in your own words in Italiano**. Do not transcribe the rulebook's marketing copy.
- `playerCountMin` / `playerCountMax`: Integers from the rulebook's player count range. Omit the field if not stated.
- `playTimeMinutes`: Integer representing typical game duration. If a range is given (e.g. "45-60 minutes"), use the **upper bound**. Omit if not stated.
- `minAge`: Integer minimum age from the box/rulebook. Omit if not stated.
- `citations`: **Required**. At least one citation supporting the summary text. Add a citation for each numeric field whose source page differs.

## Examples of acceptable reformulation

- Source: *"Players take on the role of merchants trading goods between islands."*
- ✅ Good: *"I giocatori interpretano mercanti che scambiano risorse tra isole."*
- ❌ Bad (verbatim translation): *"I giocatori prendono il ruolo di mercanti che commerciano beni tra isole."*

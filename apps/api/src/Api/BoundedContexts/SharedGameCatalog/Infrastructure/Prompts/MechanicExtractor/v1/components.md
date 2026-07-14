# Components Section (v1.1.0)

Extract the **physical game components** (the box contents / materials list).

## Output schema

```json
{
  "components": [
    {
      "name": "string (component name, Italiano or original)",
      "description": "string (Italiano, ≤200 chars, what it is / what it is for)",
      "quantity": "string (optional, e.g. '100', '4 per giocatore')",
      "citations": [
        {
          "pdf_page": 2,
          "quote": "string ≤25 words",
          "chunk_id": "string (optional)"
        }
      ]
    }
  ]
}
```

## Field rules

- `name`: The component name (e.g. "Tessere", "Plancia giocatore", "Segnalini punteggio").
- `description`: Short Italiano note on what it is or its role. Reformulate, do not transcribe.
- `quantity`: Optional. As stated in the rulebook (a number or a per-player expression).
- `citations`: At least one citation per component.

## Extraction guidance

- Extract the **physical inventory** described in the components/contents section of the rulebook.
- Between **0 and 20 components**. Merge trivial duplicates into one entry.
- This is the physical materials list — abstract resources/currencies spent during play belong to the Resources section.

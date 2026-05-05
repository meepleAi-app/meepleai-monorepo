# Resources Section (v1.0.0)

Extract the **resources, currencies, materials, action tokens and cards** the game uses mechanically.

## Output schema

```json
{
  "resources": [
    {
      "name": "string (canonical resource name, keep original if proper noun)",
      "type": "string (one of: currency | material | token | card | unit | other)",
      "usage": "string (Italiano, reformulated, ≤240 chars)",
      "isLimited": true,
      "citations": [
        {
          "pdf_page": 7,
          "quote": "string ≤25 words",
          "chunk_id": "string (optional)"
        }
      ]
    }
  ]
}
```

## Field rules

- `name`: The resource name. **Keep proper nouns and game-specific terms in their original language** (e.g. "Victory Points", "Gold", "Wood", "Meeple", "Florino"). Do not translate proper nouns.
- `type`: One of `currency`, `material`, `token`, `card`, `unit`, `other`.
- `usage`: A short Italiano reformulated description of what the resource does in the game's economy.
- `isLimited`: `true` if the resource has a finite supply from a common pool; `false` if unlimited / generated on demand.
- `citations`: At least one citation per resource.

## Extraction guidance

- Include between **0 and 15 resources**. Omit the field entirely if none are described.
- Do not include story-flavor objects that have no mechanical role.
- Merge near-identical resources (e.g. "gold coins" and "coins") into one entry.
- Action tokens (e.g. worker meeples, action cubes) belong here with `type: token` or `unit`.

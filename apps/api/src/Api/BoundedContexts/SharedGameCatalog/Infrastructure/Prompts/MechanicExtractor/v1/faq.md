# FAQ Section (v1.0.0)

Produce **5-10 frequently asked questions** about the rules, answered strictly from the retrieved chunks.

## Output schema

```json
{
  "faq": [
    {
      "question": "string (Italiano, ≤200 chars)",
      "answer": "string (Italiano, reformulated, ≤500 chars)",
      "citations": [
        {
          "pdf_page": 4,
          "quote": "string ≤25 words",
          "chunk_id": "string (optional)"
        }
      ]
    }
  ]
}
```

## Field rules

- `question`: A natural Italiano question a new player would ask. Phrase as a real question, not a section heading.
- `answer`: Italiano reformulated answer. Every factual claim must map to a citation.
- `citations`: **At least one citation per FAQ entry.** If the answer combines facts from multiple chunks, cite each one.

## Extraction guidance

- Generate **between 5 and 10 entries**. If the retrieved chunks cannot support 5, omit the `faq` field entirely rather than inventing questions.
- Prefer questions about **rule edge cases, setup, tiebreakers, end-of-game conditions, and common misunderstandings** — not trivia that is obvious from the Summary section.
- Do not ask questions you cannot answer from the chunks. Grounding is mandatory.
- Answer in complete Italiano sentences. Do not copy the question into the answer.

## Example (format only, content illustrative)

```json
{
  "question": "Cosa succede se si esaurisce la riserva di un bene durante la produzione?",
  "answer": "La produzione di quel bene si interrompe finché la riserva non viene ripristinata; gli altri beni continuano a essere prodotti normalmente.",
  "citations": [
    { "pdf_page": 6, "quote": "production halts until the supply is replenished", "chunk_id": "chunk-42" }
  ]
}
```

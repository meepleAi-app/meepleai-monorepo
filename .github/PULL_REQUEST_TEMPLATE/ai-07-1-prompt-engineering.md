# AI-07.1: Advanced Prompt Engineering for RAG Responses

## Summary

Implements advanced prompt engineering with few-shot examples, structured templates, and game-specific guidance to improve RAG answer quality by +10-15%.

**Technical Design**: [docs/technic/ai-07-rag-optimization-phase1.md](../docs/technic/ai-07-rag-optimization-phase1.md#2-optimization-1-advanced-prompt-engineering)

## Related Issue

Closes #468

## Type of Change

- [x] New feature (non-breaking change which adds functionality)
- [ ] Performance improvement
- [ ] Configuration/infrastructure change

## Changes Made

### Core Implementation

- [ ] Created `PromptTemplateService.cs` with template loading and rendering
- [ ] Implemented `GetTemplate(gameType, questionType)` method
- [ ] Implemented `RenderPrompt(template, context, query)` with variable substitution
- [ ] Added question classification logic (setup, gameplay, winning, edge_case)
- [ ] Integrated `PromptTemplateService` into `StreamingQaService`

### Configuration

- [ ] Added `RagPrompts` section to `appsettings.json`
- [ ] Defined few-shot examples for each category (min 3 per category):
  - [ ] Setup questions (e.g., "How do I set up Chess?")
  - [ ] Gameplay rules (e.g., "Can I move my pawn backward?")
  - [ ] Winning conditions (e.g., "How do I win?")
  - [ ] Edge cases (e.g., "What is en passant?")
- [ ] Created game-specific templates (Chess, Tic-Tac-Toe, generic)

### Prompt Template Structure

- [ ] System prompt with role and expertise definition
- [ ] Explicit instructions (cite sources, handle ambiguity, confidence levels)
- [ ] Few-shot examples with Q&A pairs
- [ ] Context and question placeholders
- [ ] Citation formatting guidelines

## Testing

### Test Coverage

- [x] Unit tests added/updated (`PromptTemplateServiceTests.cs`)
  - [ ] Template loading from configuration
  - [ ] Variable substitution (context, question, game_name)
  - [ ] Few-shot example selection by question type
  - [ ] Prompt length validation (LLM token limits)
  - [ ] Question classification keyword matching
- [x] Integration tests added/updated (`StreamingQaServiceTests.cs`)
  - [ ] End-to-end prompt rendering in QA flow
  - [ ] Answer quality with/without few-shot examples
  - [ ] Citation format validation in LLM responses
- [x] All tests passing locally
- [x] Test names follow BDD convention

### RAG Evaluation

- [ ] Baseline evaluation run (before prompt changes)
- [ ] Post-implementation evaluation run
- [ ] Metrics comparison:
  - [ ] Precision@5 improvement: ____% (target: +10-15%)
  - [ ] MRR improvement: ____% (target: +10-20%)
  - [ ] Answer quality (manual review): Improved / Same / Regressed

**Evaluation Results**:
```
<!-- Paste RAG evaluation results here -->
```

### Manual Testing

- [ ] Tested with Chess queries (setup, piece movement, special moves)
- [ ] Tested with Tic-Tac-Toe queries (setup, winning conditions)
- [ ] Verified citation formatting in responses
- [ ] Tested edge case queries (ambiguous questions, missing context)
- [ ] Confirmed graceful degradation if template loading fails

**Sample Q&A** (before/after):
```
<!-- Paste example Q&A demonstrating improvement -->
Q: How do I castle in Chess?

Before (generic prompt):
[Paste old answer]

After (few-shot prompt):
[Paste new answer with better formatting and citations]
```

## Acceptance Criteria (from #468)

- [ ] `PromptTemplateService` created with `GetTemplate(gameType, questionType)` method
- [ ] Few-shot examples stored in config (min 3 per category)
- [ ] `StreamingQaService.AskStreamAsync()` uses new prompts
- [ ] Unit tests verify prompt structure and few-shot inclusion
- [ ] Evaluation shows +10-15% improvement in answer accuracy
- [ ] Documentation updated in `docs/technic/ai-07-rag-optimization-phase1.md`

## Checklist

- [ ] Code follows project style guidelines
- [ ] Self-review of code completed
- [ ] Comments added for prompt template structure and classification logic
- [ ] Documentation updated (technical design doc already exists)
- [ ] No new warnings introduced
- [ ] Tests added/updated and passing
- [ ] Test names follow BDD-style naming convention
- [ ] Changes are backwards compatible (old prompts still work if config missing)
- [ ] No secrets or API keys committed

## Configuration Example

**appsettings.json** (excerpt):
```json
{
  "RagPrompts": {
    "DefaultTemplate": "generic",
    "Templates": {
      "generic": {
        "SystemPrompt": "You are an expert board game rules assistant...",
        "FewShotExamples": [
          {
            "question": "How do I set up Chess?",
            "answer": "To set up Chess, place the board so each player has a white square on their right [p.5]...",
            "category": "setup"
          }
        ],
        "MaxExamples": 3
      }
    }
  }
}
```

## Performance Impact

- [ ] Latency impact measured: +____ ms (expect minimal, <50ms for template rendering)
- [ ] Memory usage: No significant increase expected
- [ ] Token usage: +____% per query (longer prompts with few-shot examples)

## Rollback Plan

If metrics regress:
1. Set `RagPrompts.DefaultTemplate` to empty string (falls back to old prompt)
2. Restart API pods
3. Re-run evaluation to confirm rollback successful

## Additional Notes

<!-- Any additional context or implementation decisions -->

**Next Steps** (after merge):
- Monitor user feedback for 1-2 weeks
- A/B test with 50% rollout before full deployment
- Proceed to AI-07.2 (Semantic Chunking)

**Cost Impact**: $0/month (uses existing LLM, no additional API calls)

# Page-mock fixtures (DS-17-6-v2)

Centralized fixtures consumed by page-mock stories under `src/app/.../X.stories.tsx`.
Distinct from per-component fixtures (`__tests__/fixtures/common-fixtures.ts`,
`__tests__/fixtures/play-records-stats/`, …) — these represent **entire pages** of mockup data.

## Pattern

Each entity export must follow:

```ts
export const MOCK_<NAME>: <Type> = { /* full populated state */ };
export const MOCK_<NAME>_EMPTY: <Type> = { /* empty state */ };
```

Use per-fixture imports in story files (granular MSW handlers per endpoint). Avoid composite fixtures
that bundle multiple entities — they push the story to import less + know less about handler granularity,
which Phase 3 sweep contributors should keep flexible.

**Anti-pattern**: NON hardcodare `Date.now()` o valori dipendenti dalla data corrente.
Usa `__DATE_REF__` placeholder + replace al wrapper level.

## Refs

- Pattern docs: `docs/for-developers/frontend/page-mock-story-pattern.md`
- Spec: `docs/superpowers/specs/2026-06-09-ds-17-phase-2-design.md`
- Sub-issue: #2092 (DS-17-6-v2), umbrella #2063.

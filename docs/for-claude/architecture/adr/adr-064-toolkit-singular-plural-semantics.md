# ADR-064 — `/toolkit` vs `/toolkits`: intentional singular/plural split

**Date**: 2026-06-14
**Status**: Accepted
**Issue**: #2315 (spawned from #2152)

## Context

DS-17 Phase B audit Section C flagged a naming drift between two surviving
top-level routes:

- `/toolkit/*` (6 pages — **singular**)
- `/toolkits/*` (2 pages — **plural**)

The audit recommended deciding canonical form (sing. vs plur.) and rationalising
to one. The navigation test
(`apps/web/src/config/__tests__/navigation.test.ts:308-312`) already documented
the current contract: the `toolkit` nav voice's `activePattern` matches
`/toolkit/*` but explicitly NOT `/toolkits/*`.

## Decision

**Keep both as-is — they encode an intentional semantic split.**

| Route | Pattern | Owner | Examples |
|---|---|---|---|
| `/toolkit/*` | resource-singleton (per-user space) | the signed-in user | `/profile`, `/library`, `/dashboard` |
| `/toolkits/*` | resource-collection (community catalog) | the community | `/games`, `/agents`, `/players` |

Concretely:

- `/toolkit` = "**my** toolkit working space". Today renders a stub ("Toolkit in arrivo") that will grow into the user's active toolkit, history, play, stats, templates, and `[sessionId]` sub-routes.
- `/toolkits` = "**catalog** of community toolkits". `/toolkits` is the discover-style list, `/toolkits/[id]` is per-item detail.

The pattern is the same as `/profile` (singular user surface) vs
`/profiles/[id]` (collection lookup of public profiles). Collapsing to one
form would erase a real conceptual distinction.

## Consequences

### Positive
- Conceptual clarity preserved: collection vs singleton are visibly distinct in the URL.
- Zero migration cost: the navigation contract is already correct.
- Consistent with sibling patterns (`/library` vs `/library/shared`, `/profile` vs `/profiles`).

### Negative / debt
- Readers unfamiliar with the convention may parse `/toolkit` and `/toolkits` as a typo. Mitigated by this ADR + a comment block on `navigation.ts` and the test contract.

### Follow-up clean-up (NOT this ADR)

The audit flagged 1 slug-naming inconsistency that survives independently:

- `/toolkit/[sessionId]` uses `[sessionId]` (semantic slug).
- `/toolkits/[id]` uses `[id]` (generic slug).

Both are valid Next.js App Router conventions; harmonising them is a
separate concern from the singular/plural decision and is **deferred** —
the slug semantics encode the relationship to the underlying entity (a
toolkit session ≠ a toolkit catalog entry), and standardising could mask
the distinction.

## References

- DS-17 Phase B audit: `audits/2026-06-10-nav-chrome-bgg-naming-audit.md` § Section C.5b
- Existing navigation contract: `apps/web/src/config/__tests__/navigation.test.ts:308-312`
- `/toolkits` introduction PR: #1480
- `/toolkits/[id]` introduction PR: #1145
- Parent umbrella: #2152

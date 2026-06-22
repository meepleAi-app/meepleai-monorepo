# ADR Draft — Pre-commit Lychee --offline hook

**Status**: Draft — proposal post #979 release-PR triage
**Date**: 2026-05-11
**Context**: Lychee fails on release PRs reveal docs link debt accumulated over months. Cheaper to catch broken links at commit time vs CI time.

## Problem

Today's flow:
1. Developer moves/deletes a doc file
2. Commit + push, PR opens
3. CI Lychee runs on PR-touched files, fails
4. Developer iterates fix-push cycles

Each cycle: ~3min round-trip. Multiple iterations common when fixing cascade refs.

## Proposal

Add pre-commit hook (`.husky/pre-commit` or `lefthook.yml`) running `lychee --offline` on staged Markdown files:

```bash
#!/bin/bash
# Lint only staged *.md files for broken relative links
staged_md=$(git diff --cached --name-only --diff-filter=AM | grep -E '\.md$' || true)
if [ -z "$staged_md" ]; then
  exit 0
fi

if ! command -v lychee >/dev/null 2>&1; then
  echo "lychee not installed — skipping. Install: cargo install lychee"
  exit 0
fi

# Same args as CI workflow
lychee --no-progress --offline --include-fragments --root-dir "$(git rev-parse --show-toplevel)" --max-cache-age 1d $staged_md
```

## Trade-offs

| Pro | Con |
|-----|-----|
| Shift-left: catch at commit, not CI | Requires lychee installed locally |
| Sub-second on small changes | Hook adds latency |
| Same args as CI = consistent verdict | Optional flag if no lychee |

## Alternatives

- **Lefthook** (more polyglot): orchestrates multiple hooks. Adopt only if other lint tools also benefit.
- **CI-only** (status quo): cheaper to maintain. But cycle latency stays.
- **GitHub action `on: push`** in dev branches: cheaper than per-PR but bypasses developer feedback loop.

## Decision

TBD. Low-cost experiment: add as opt-in hook (no enforcement) → measure adoption + delta. If 80% devs adopt voluntarily, formalize.

## References

- #1002, #1013 (Lychee CI fixes that motivated this)
- ADR docs.lock.json proposal (companion proposal)

# For Claude (and architects)

> **Audience contract**: AI agent + technical architect. Dense, token-efficient, machine-friendly.
> If you're a human contributor looking for "how to set up the project", you want [`../for-developers/`](../for-developers/).
> If you're an end user wondering what the app does, you want [`../for-users/`](../for-users/).

---

## What lives here

| Subdir | Content |
|--------|---------|
| `architecture/` | ADRs (numbered, canonical), DDD overview, system diagrams |
| `architecture/bounded-contexts/` | One reference doc per BC (mirrors code under `apps/api/src/Api/BoundedContexts/`) |
| `api-reference/` | REST endpoint reference, RAG architecture, contracts |
| `patterns/` | Reusable patterns: agent architecture, multi-LLM consensus, retrieval |
| `skills-reference.md` | Index of available Claude Code skills used in this repo |

---

## Conventions

- **No tutorials**, no setup steps. Pure reference.
- Cross-link to code with file paths (`apps/api/src/Api/BoundedContexts/{Context}/...`).
- Diagrams: prefer Mermaid in code blocks; static SVG/PNG live under `assets/diagrams/` and are linked from here.
- ADRs are append-only. Superseding decisions get a new ADR that links the old one.

---

## Status

🚧 **Stub root** — Phase 0 of [docs reorg](../MIGRATION-PLAN.md). Content moves in Phase 3.

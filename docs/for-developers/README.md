# For Developers

> **Audience contract**: human contributor (backend, frontend, devops, QA, security).
> Task-oriented: "how do I…" not "what is…".
> If you want dense reference for AI/architects, see [`../for-claude/`](../for-claude/).
> If you're an end user, see [`../for-users/`](../for-users/).

---

## Start here

- **First time?** → `workflows/getting-started.md` (TBD)
- **Setting up locally?** → `workflows/local-setup.md` (TBD)
- **Adding a feature?** → `workflows/feature-development.md` (TBD)
- **Debugging CI?** → `workflows/ci-troubleshooting.md` (TBD)

---

## What lives here

| Subdir | Content |
|--------|---------|
| `workflows/` | Git workflow, secrets, migrations, feature dev, specs, templates |
| `testing/` | Backend (xUnit + Testcontainers), frontend (Vitest + Playwright), e2e, fixtures |
| `frontend/` | Component guides, design tokens, MeepleCard, design system |
| `deployment/` | Infrastructure, CI/CD pipelines, runbooks, monitoring, operations |
| `security/` | OWASP, vulnerabilities, compliance |

---

## Conventions

- One task = one page. No 500-line megadocs.
- Every guide opens with: prerequisites → steps → verification → troubleshooting.
- Code samples must be copy-pasteable and tested.
- Link to canonical source instead of duplicating (e.g., link to `CLAUDE.md` for backend rules).

---

## Status

🚧 **Stub root** — Phase 0 of [docs reorg](../MIGRATION-PLAN.md). Content moves in Phase 4.

# For Developers

> **Audience contract**: human contributor (backend, frontend, devops, QA, security).
> Task-oriented: "how do I…" not "what is…".
> AI/architect reference? → [`../for-claude/`](../for-claude/).
> End-user docs? → [`../for-users/`](../for-users/).

---

## Start here

- **Setup locale + workflow** → [CLAUDE.md](../../CLAUDE.md) (single source of truth)
- **Snapshot/seed dev DB** → [`workflows/snapshot-seed-workflow.md`](./workflows/snapshot-seed-workflow.md)
- **Git branching/PR** → [`workflows/git-workflow.md`](./workflows/git-workflow.md)
- **Secrets management** → [`workflows/secrets-management.md`](./workflows/secrets-management.md)
- **Alpha scope** → [`alpha-zero-scope.md`](./alpha-zero-scope.md)

---

## What lives here

| Subdir | Content |
|--------|---------|
| `workflows/` | snapshot-seed, git-workflow, secrets-management, README |
| `testing/` | README + 1 patterns doc per area: `backend/`, `frontend/`, `e2e/` |
| `frontend/` | `meeple-card-design-tokens.md` + `v2-migration-matrix.md` (referenced from CLAUDE.md) |
| `deployment/` | 5 critical deploy/CI/CD guides (infra checklist, deploy workflows, monitoring, secrets, prod runbook) |
| `operations/` | `operations-manual.md` (single canonical production runbook) |
| `security/` | OWASP + DPIA + Q1/Q2 reviews + iac-scans (Hadolint) + GitHub Actions pinning |
| `templates/` | Bounded-context template |
| `specs/` | 4 active design specs (referenced by ADRs/CLAUDE.md) |
| `plans/` | 1 in-flight plan (`2026-05-06-sp6-libro-game-migration.md`) |

---

## Conventions

- One task = one page. No megadocs.
- Every guide: prerequisites → steps → verification → troubleshooting.
- Code samples copy-pasteable.
- Link to canonical source instead of duplicating (CLAUDE.md = SSOT for backend rules).

---

## Status

✅ **Phase 4 complete** (2026-05-06) — `development/`, `testing/`, `frontend/`, `deployment/`, `operations/`, `security/`, `templates/`, active `superpowers/{specs,plans}/` consolidated under this audience root. See [MIGRATION-PLAN.md](../MIGRATION-PLAN.md).

# Contributing to MeepleAI

Thanks for your interest in contributing! This document covers the basics for getting changes merged into MeepleAI.

> **Status**: bootstrapped 2026-05-06 to satisfy `SECURITY.md` reference (Q2 2026 Security Review #186 follow-up). Expand with project-specific conventions as needed.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Reporting Bugs](#reporting-bugs)
- [Reporting Security Vulnerabilities](#reporting-security-vulnerabilities)
- [Suggesting Enhancements](#suggesting-enhancements)
- [Pull Request Process](#pull-request-process)
- [Development Setup](#development-setup)
- [Coding Standards](#coding-standards)
- [Commit Message Convention](#commit-message-convention)
- [Branching Strategy](#branching-strategy)

---

## Code of Conduct

By participating in this project, you agree to maintain a respectful, professional environment. Harassment, discriminatory language, and personal attacks are not tolerated. Disagreements are expected; keep them technical.

If a contributor's behavior violates these expectations, contact the maintainer (see [SECURITY.md](./SECURITY.md) for contact info).

---

## Reporting Bugs

For non-security bugs, [open a GitHub issue](https://github.com/meepleAi-app/meepleai-monorepo/issues/new). Include:

- **Reproduction steps** — what did you do?
- **Expected behavior** — what should have happened?
- **Actual behavior** — what happened instead?
- **Environment** — OS, browser, MeepleAI version (commit SHA if from source)
- **Logs** — relevant error messages or screenshots

For security-sensitive bugs, see the next section.

---

## Reporting Security Vulnerabilities

**DO NOT open a public GitHub issue for security vulnerabilities.**

Follow the responsible disclosure process documented in [SECURITY.md](./SECURITY.md):

- Use [GitHub Security Advisories](https://github.com/meepleAi-app/meepleai-monorepo/security/advisories/new) for confidential reporting.
- Acknowledgment within 48 hours.
- Coordinated disclosure timing.
- Credit in the security advisory (or anonymous if you prefer).

---

## Suggesting Enhancements

[Open an issue](https://github.com/meepleAi-app/meepleai-monorepo/issues/new) describing:

- The problem you're trying to solve (not just the solution).
- Why existing functionality doesn't address it.
- Proposed approach (if any) and trade-offs.

Discussion happens in the issue thread before implementation begins.

---

## Pull Request Process

1. **Fork the repository** (external contributors) or create a feature branch (organization members).
2. **Branch naming**: `feature/<short-description>` or `feature/issue-<n>-<desc>` for issue work.
3. **Branch from `main-dev`**: see [`CLAUDE.md`](./CLAUDE.md#git-workflow) §"Git Workflow" for the canonical flow.
4. **Keep PRs focused** — one logical change per PR. Easier to review, easier to revert.
5. **Self-review before requesting review**:
   - All tests pass locally
   - Pre-commit hooks pass (`husky` runs typecheck, lint, conventional commit format)
   - No new HIGH/CRITICAL findings in `pnpm audit` or CodeQL local scan
6. **Open the PR against the correct base branch** — same parent you branched from, NOT always `main`. See `CLAUDE.md` §"PR Target Rule".
7. **Fill in the PR body**:
   - Summary of changes
   - Why (link to issue if applicable)
   - Test plan
   - Risk assessment for non-trivial changes
8. **Address review feedback** with new commits (don't force-push during review unless asked).
9. **Squash on merge** is the default merge strategy.

---

## Development Setup

See [`CLAUDE.md`](./CLAUDE.md#development) §"Development" for the full quick-start. Summary:

```bash
# Clone
git clone https://github.com/meepleAi-app/meepleai-monorepo.git
cd meepleai-monorepo

# Backend
cd apps/api/src/Api && dotnet restore

# Frontend
cd ../../../web && pnpm install

# Secrets (one-time)
cd ../../infra && make secrets-setup && make secrets-sync

# Start dev environment
make dev
```

API docs: <http://localhost:8080/scalar/v1>

---

## Coding Standards

### Backend (C#)

- **Naming**: `PascalCase` (public), `_camelCase` (private), `I` prefix for interfaces
- **Entities**: private setters + factory methods (`Game.Create()`)
- **Value Objects**: immutable record + validation in factory (`Email.Create()`)
- **Domain exceptions** over generic ones (`GameNotFoundException`, not `InvalidOperationException`)
- **CQRS strict**: endpoints use ONLY `IMediator.Send()`. NO direct service injection in endpoints.

See [`CLAUDE.md`](./CLAUDE.md#code-standards) §"Code Standards" + bounded-context READMEs.

### Frontend (TypeScript)

- **Naming**: `PascalCase` (components/types), `camelCase` (functions/vars), `UPPER_SNAKE_CASE` (constants)
- **Component**: typed props + explicit `JSX.Element` return
- **State**: Zustand with TypeScript interface
- **Cards**: use `MeepleCard` for entity displays — never the deprecated `GameCard`/`PlayerCard`. See `apps/web/src/components/ui/data-display/meeple-card`

### Tests

- **Backend target**: 90%+ coverage on critical paths
- **Frontend target**: 85%+ coverage
- **Security**: every Critical/High security finding fixed must have a regression test in `Category=SecurityRegression`

---

## Commit Message Convention

We use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

[optional body]

[optional footer]
```

**Types**: `feat`, `fix`, `docs`, `test`, `refactor`, `perf`, `ci`, `chore`, `style`, `build`, `revert`.

**Subject**: ≤72 chars, imperative mood ("add", not "added"), no period.

**Header total length**: ≤100 chars.

Examples:
```
feat(auth): add 2FA enforcement attribute for admin commands
fix(api): resolve null reference in OAuth callback handler
docs(security): add SHA pinning policy for GitHub Actions
```

Husky `commit-msg` hook validates on every commit; CI re-validates.

---

## Branching Strategy

| Branch | Purpose |
|--------|---------|
| `main` | Production releases |
| `main-staging` | Pre-production release branch (PRs from `main-dev`) |
| `main-dev` | Active development integration |
| `feature/issue-<n>-<desc>` | Issue-driven feature work |
| `feature/<descriptive-name>` | Non-issue feature work |

**🔴 PR Target Rule**: feature branches MUST merge to their **parent** branch, NOT always `main`.

```bash
# Track parent for clarity
git config branch.<branch-name>.parent <parent-branch>

# Or detect via:
git merge-base HEAD <suspected-parent>
```

After a PR merges, delete the local branch:
```bash
git branch -D feature/issue-123-foo
git remote prune origin
```

### 🔴 Branch Hygiene — Before Creating a Feature Branch

> **Root cause of issue #806** (PR #797 absorbed 17 unrelated commits from issue #730 because the new branch was created from an in-progress branch tip instead of `main-dev`). Multi-terminal AI agentic sessions are particularly prone to this when juggling concurrent branches.

**ALWAYS run this pre-creation safety check before `git checkout -b feature/...`:**

```bash
# 1. Verify HEAD is on the intended parent (main-dev / main / main-staging),
#    NOT on another in-progress feature/* branch
git branch --show-current   # MUST print main-dev (or your intended parent)

# 2. Working tree must be clean (no in-flight changes from another branch)
git status                  # MUST show "nothing to commit, working tree clean"

# 3. Parent branch must be up-to-date with origin (no divergence)
git pull --ff-only          # MUST succeed without conflict

# 4. NOW create the feature branch
git checkout -b feature/issue-<n>-<desc>

# 5. Optionally track parent for explicit lineage on later PRs
git config branch.feature/issue-<n>-<desc>.parent main-dev
```

If `git branch --show-current` prints `feature/...`, **STOP**. Switch back first:
```bash
git checkout main-dev && git pull
```

#### ❌ What NOT to do

```bash
# DON'T: create new branch while still on another feature branch
git checkout -b feature/issue-200-new   # while HEAD is on feature/issue-100-old
# → All in-progress commits from #100 get absorbed into #200's ancestry
# → PR #200 will show #100's commits under #200's title (issue #806)
```

#### PR opening checklist

When opening a PR, verify:

- [ ] **PR title scope matches commit list** — `feat(scope)` should not include commits from unrelated `feat(other-scope)` work
- [ ] **PR file count + LOC are proportional** to the title (e.g., `feat(infra)` should not have 30+ files unless explicit doc reorg)
- [ ] **Commit subjects reference the same issue number** as the branch name (heuristic: `feat(*): #<branch-issue> ...` for each commit, not `feat(*): #<other-issue> ...`)

If any of these red-flag, the branch was likely created on top of an in-progress sibling — fix via `git rebase --onto main-dev <wrong-base> HEAD` before opening the PR.

---

## References

- [`SECURITY.md`](./SECURITY.md) — security policy + responsible disclosure
- [`CLAUDE.md`](./CLAUDE.md) — full developer guide (architecture, code standards, troubleshooting)
- [`docs/security/`](./docs/security/) — security documentation tree
- [`docs/development/`](./docs/development/) — development guides

Questions? Open an issue or check existing discussions.

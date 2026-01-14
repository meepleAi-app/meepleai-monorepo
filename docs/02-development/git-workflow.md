# Git Workflow - MeepleAI

## Branch Strategy

| Branch | Directory | Scopo | CI |
|--------|-----------|-------|:--:|
| `main` | `D:\Repositories\meepleai-monorepo` | Production | ✅ |
| `main-dev` | `D:\Repositories\meepleai-monorepo-dev` | Development | ✅ |
| `frontend-dev` | `D:\Repositories\meepleai-monorepo-frontend` | Frontend Dev | ❌ |

## Merge Flow

```
feature/frontend-dev-{issue}-{desc}
        ↓ PR
    frontend-dev
        ↓ PR
      main-dev
        ↓ PR
        main
```

## Pre-commit Hooks

| Branch | Lint | Typecheck | BE Format | Secrets | Semgrep |
|--------|:----:|:---------:|:---------:|:-------:|:-------:|
| `frontend-dev` | ✅ | ✅ | ❌ | ❌ | ❌ |
| `main-dev` | ✅ | ✅ | ✅ | ✅ | ❌ |
| `main` | ✅ | ✅ | ✅ | ✅ | ✅ |

## Pre-push Hooks

Tutti i branch: **Solo build verification** (no tests)
- Frontend: `pnpm build`
- Backend: `dotnet build`

## Commit Messages

Conventional Commits obbligatorio:
```
<type>(<scope>): <subject>

feat, fix, docs, test, refactor, perf, ci, chore, style, build, revert
```

## Branch Naming

| Parent | Pattern | Esempio |
|--------|---------|---------|
| `frontend-dev` | `feature/frontend-dev-{issue}-{desc}` | `feature/frontend-dev-123-add-button` |
| `main-dev` | `feature/{issue}-{desc}` | `feature/456-api-endpoint` |

## Workflow Tipico

### 1. Sviluppo Frontend
```bash
# Da frontend-dev
cd D:\Repositories\meepleai-monorepo-frontend
git checkout frontend-dev
git pull
git checkout -b feature/frontend-dev-123-new-component

# Lavora, commit
git add .
git commit -m "feat(frontend): add new component"
git push -u origin feature/frontend-dev-123-new-component

# PR: feature/frontend-dev-123-new-component → frontend-dev
```

### 2. Promozione a main-dev
```bash
# Da main-dev
cd D:\Repositories\meepleai-monorepo-dev
git checkout main-dev
git pull
git merge frontend-dev

# O via PR: frontend-dev → main-dev
```

### 3. Release a Production
```bash
# Da main
cd D:\Repositories\meepleai-monorepo
git checkout main
git pull

# PR: main-dev → main (CI full + security)
```

## Branch Protection Rules (GitHub)

### main
- ✅ Require PR from `main-dev` only
- ✅ Require status checks (CI)
- ❌ No direct push

### main-dev
- ✅ Require PR from allowed patterns
- ✅ Require status checks (CI)
- ⚠️ Direct push allowed (emergenze)

### frontend-dev
- ❌ No restrictions
- ❌ No CI trigger
- ✅ Local hooks only

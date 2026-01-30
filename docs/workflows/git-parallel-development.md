# 🔀 Git Workflow Strategy - Parallel Development

**Scopo**: Guideline per sviluppo parallelo Frontend/Backend con sincronizzazione controllata

**Audience**: Team sviluppo MeepleAI
**Contesto**: Epic multi-settimana con dipendenze cross-team

---

## 📋 Indice

1. [Branch Structure](#branch-structure)
2. [Merge Points](#merge-points-sincronizzazione)
3. [Parallelization Rules](#parallelization-rules)
4. [Daily Sync Protocol](#daily-sync-protocol)
5. [Conflict Resolution](#conflict-resolution-strategy)
6. [Example Workflow](#example-workflow-week-3-4)
7. [CI/CD Integration](#cicd-integration)

---

## Branch Structure

```
main-dev (development trunk)
├── frontend-dev (UI/UX development)
│   ├── feature/agt-011-agent-button
│   ├── feature/agt-012-config-modal
│   ├── feature/agt-013-chat-sidebar
│   └── feature/component-library
│
└── backend-dev (API/Services development)
    ├── feature/agt-001-typology-model
    ├── feature/agt-009-agent-session
    └── feature/gst-001-session-schema
```

### Branch Naming Convention

**Feature Branches**:
```
feature/{issue-id}-{short-desc}
feature/agt-013-chat-sidebar
feature/gst-004-toolkit-routes
```

**Long-lived Branches**:
- `main-dev`: Development trunk (pre-production)
- `frontend-dev`: Frontend development stream
- `backend-dev`: Backend development stream
- `main`: Production release branch

---

## Merge Points (Sincronizzazione)

### Merge Point 1: Week 2 End (Agent Typology Backend Ready)

```bash
# Backend completa AGT-001 to AGT-004
backend-dev → main-dev (PR + merge)

# Frontend può iniziare AGT-005 to AGT-008
main-dev → frontend-dev (sync)
```

**Deliverable**: Backend Typology API completo + OpenAPI spec aggiornato
**Validation**: Backend tests pass + API docs generati

### Merge Point 2: Week 3 End (Agent Typology Frontend + GST Backend Ready)

```bash
# Frontend completa AGT-005 to AGT-008
frontend-dev → main-dev (PR + merge)

# Backend completa GST-001 to GST-003, AGT-009, AGT-010
backend-dev → main-dev (PR + merge)

# Sincronizzazione per EPIC 2 development
main-dev → frontend-dev (sync)
main-dev → backend-dev (sync)
```

**Deliverable**: Typology UI completo + GST Backend + Session Backend
**Validation**: E2E smoke tests pass + Integration tests green

### Merge Point 3: Week 4 End (Chat UI Ready)

```bash
# Frontend completa AGT-011 to AGT-014 (Chat Sidebar!)
frontend-dev → main-dev (PR + merge)

# Backend completa AGT-015 (GST Integration)
backend-dev → main-dev (PR + merge)

# Sincronizzazione per E2E testing
main-dev → frontend-dev (sync)
main-dev → backend-dev (sync)
```

**Deliverable**: Chat UI completo con SSE streaming + Agent-GST sync
**Validation**: Full E2E chat flow pass

### Merge Point 4: Week 5 End (Testing Complete - MVP LAUNCH)

```bash
# Frontend tests AGT-016, E2E AGT-017
frontend-dev → main-dev (PR + merge)

# Backend validation AGT-018
backend-dev → main-dev (PR + merge)

# Final merge to production
main-dev → main (Release PR)
```

**Deliverable**: Agent System MVP ready for production
**Validation**: All tests pass + QA approval + Coverage targets met

---

## Parallelization Rules

### ✅ Può Procedere in Parallelo

**Week 2**:
- Backend (AGT-001 to AGT-004)
- Frontend (Component Library #2924-#2926)

**Week 3**:
- Frontend (AGT-005 to AGT-008)
- Backend (GST-001 to GST-003)

**Week 4**:
- Frontend (AGT-011 to AGT-014)
- Backend (AGT-015 GST Integration)

**Week 6-8**:
- Frontend (Library #2866, #2867)
- Backend (Feature Flags #3073)
- Infra (Oracle Cloud #2968-#2970)

### ⛔ Richiede Sincronizzazione (Sequential)

**Week 1**:
- RAG Validation (#3172, #3173) → **BLOCKER** per tutto

**Week 2 → Week 3**:
- Backend Typology deve completare prima che Frontend Typology inizi AGT-005

**Week 3 → Week 4**:
- GST Backend deve completare prima che Agent Session Frontend inizi AGT-011

**Week 5**:
- E2E tests richiedono sia Frontend che Backend completati

---

## Daily Sync Protocol

### Morning Standup (async Slack)

**Format**:
```
**Backend Team**: AGT-001 progress 70%, AGT-002 blocked by migration issue
**Frontend Team**: Component Library 80%, Storybook setup complete
**Conflicts**: None detected
```

**Channels**:
- `#dev-backend`: Backend daily updates
- `#dev-frontend`: Frontend daily updates
- `#dev-sync`: Cross-team coordination

### Evening Sync (se necessario)

```bash
# Frontend synca da main-dev ogni sera per includere backend changes
cd apps/web
git checkout frontend-dev
git fetch origin
git merge origin/main-dev

# Risolvi conflicts se presenti
# Pusha changes
git push origin frontend-dev
```

**When to Sync**:
- After merge point completions
- Before starting work on dependent features
- When OpenAPI spec is updated
- When database migrations are added

---

## Conflict Resolution Strategy

### 1. API Contract Changes

**Process**:
1. Backend notifica Frontend team in `#dev-sync`
2. Backend aggiorna OpenAPI spec
3. Frontend rigenera client con `pnpm generate:api`
4. Frontend aggiorna type imports

**Example**:
```typescript
// Prima
import { AgentTypologyDto } from '@/lib/api/generated'

// Dopo (contract changed)
import { AgentTypologyResponse } from '@/lib/api/generated'
```

### 2. Database Schema Changes

**Process**:
1. Backend crea migration in feature branch
2. Backend comunica schema change in PR description
3. Frontend aggiorna types dopo merge point
4. Integration tests validano compatibilità

**Example**:
```sql
-- Migration: Add agent_sessions table
CREATE TABLE agent_sessions (
    id UUID PRIMARY KEY,
    game_session_id UUID REFERENCES game_sessions(id),
    -- ...
);
```

### 3. Environment Variables

**Process**:
1. Backend aggiunge entry in `infra/secrets/{service}.secret`
2. Backend documenta in PR + notifica in `#dev-sync`
3. Frontend aggiorna `.env.local` se necessario
4. Team runs `setup-secrets.ps1` per sync

**Example**:
```bash
# Backend adds to infra/secrets/api.secret
AGENT_MAX_SESSIONS=10

# Frontend updates .env.local (if needed)
NEXT_PUBLIC_AGENT_MAX_SESSIONS=10
```

### 4. Shared Components/Utilities

**Process**:
1. Coordinate in `#dev-sync` prima di modificare shared code
2. Preferire creazione nuove versioni vs breaking changes
3. Deprecation period (2 weeks) prima di rimuovere old code

**Example**:
```typescript
// Invece di breaking change su GameCard
// Crea GameCardV2 e migra gradualmente
export { GameCard as GameCardLegacy } from './GameCard'
export { GameCardV2 as GameCard } from './GameCardV2'
```

---

## Example Workflow (Week 3-4)

### Backend Team (Week 3)

```bash
# Setup
git checkout main-dev && git pull
git checkout -b backend-dev
git checkout -b feature/agt-009-agent-session

# Sviluppo AGT-009: AgentSession entity + migration
# Commit incrementali
git add . && git commit -m "feat(agent): add AgentSession entity"
git add . && git commit -m "feat(agent): add session migration"
git add . && git commit -m "test(agent): add session repository tests"

# Push e PR
git push -u origin feature/agt-009-agent-session
# Create PR to backend-dev
# Code review + tests pass
# Merge to backend-dev

git checkout backend-dev
git merge feature/agt-009-agent-session
git push origin backend-dev
```

### Frontend Team (Week 3 - parallel)

```bash
# Setup
git checkout main-dev && git pull
git checkout -b frontend-dev
git checkout -b feature/agt-005-admin-list

# Sviluppo AGT-005: Admin typologies list page
git add . && git commit -m "feat(admin): add typologies list page"
git add . && git commit -m "feat(admin): add pagination and filters"
git add . && git commit -m "test(admin): add list page tests"

# Push e PR
git push -u origin feature/agt-005-admin-list
# Create PR to frontend-dev
# Code review + tests pass
# Merge to frontend-dev

git checkout frontend-dev
git merge feature/agt-005-admin-list
git push origin frontend-dev
```

### Merge Point (End of Week 3)

```bash
# Backend merge to main-dev
git checkout main-dev
git merge backend-dev
git push origin main-dev

# Frontend merge to main-dev
git checkout main-dev
git merge frontend-dev
git push origin main-dev

# Sync per Week 4
git checkout backend-dev && git merge main-dev && git push
git checkout frontend-dev && git merge main-dev && git push
```

### Week 4 Development

```bash
# Frontend ora può usare AgentSession types da backend
# Backend può procedere con AGT-015 (GST Integration)

# Frontend
cd apps/web
pnpm generate:api  # Rigenera client con nuovi types
git checkout -b feature/agt-011-agent-button
# Usa AgentSessionDto da generated types
```

---

## CI/CD Integration

### Branch Protection Rules

```yaml
main-dev:
  required_reviews: 1
  required_checks:
    - backend-tests
    - frontend-tests
    - e2e-smoke-tests
  required_status_checks:
    - build-backend
    - build-frontend
  dismiss_stale_reviews: true
  require_code_owner_reviews: false

frontend-dev:
  required_reviews: 1
  required_checks:
    - frontend-tests
    - frontend-lint
    - frontend-typecheck
  required_status_checks:
    - build-frontend

backend-dev:
  required_reviews: 1
  required_checks:
    - backend-tests
    - backend-coverage-90
  required_status_checks:
    - build-backend
```

### Automated Merge Point Validation

```yaml
# .github/workflows/merge-point-validation.yml
name: Merge Point Validation

on:
  pull_request:
    branches: [main-dev]
    types: [opened, synchronize, reopened]

jobs:
  validate-merge-point:
    runs-on: ubuntu-latest
    steps:
      - name: Check Dependencies Merged
        run: |
          # Verifica che tutte le dipendenze siano già in main-dev
          # Esempio: AGT-005 richiede AGT-001-004 merged

      - name: Run Integration Tests
        run: |
          docker compose up -d
          cd apps/api/src/Api && dotnet test --filter "Category=Integration"
          cd ../../../web && pnpm test:integration

      - name: Validate API Contracts
        run: |
          # Verifica che OpenAPI spec sia sincronizzato
          cd apps/api/src/Api && dotnet run --generate-openapi
          cd ../../../web && pnpm generate:api
          git diff --exit-code apps/web/src/lib/api/generated

      - name: Check Migration Compatibility
        run: |
          # Verifica che migrations siano applicabili senza conflitti
          cd apps/api/src/Api
          dotnet ef database update --dry-run
```

### Pre-Merge Checklist

**Backend PR to main-dev**:
- [ ] All unit tests pass (>90% coverage)
- [ ] Integration tests pass
- [ ] Migrations tested on clean DB
- [ ] OpenAPI spec updated (if API changes)
- [ ] API docs reviewed
- [ ] Breaking changes documented

**Frontend PR to main-dev**:
- [ ] All tests pass (>85% coverage)
- [ ] Type checks pass
- [ ] Lint pass
- [ ] E2E smoke tests pass
- [ ] Accessibility checks pass
- [ ] API client regenerated (if backend changed)

**Merge Point to Production (main-dev → main)**:
- [ ] Full E2E test suite pass
- [ ] Performance tests pass (Lighthouse >90)
- [ ] Security scan pass (Semgrep)
- [ ] QA approval obtained
- [ ] Release notes prepared
- [ ] Rollback plan documented

---

## Best Practices

### ✅ DO

- **Communicate Early**: Notify `#dev-sync` before making changes affecting other team
- **Small PRs**: Keep PRs <400 LOC quando possibile, easier to review
- **Incremental Commits**: Commit logico per feature, non "WIP" commits
- **Test First**: Write tests before merging to long-lived branches
- **Sync Daily**: Pull from main-dev daily, avoid long-lived divergent branches

### ❌ DON'T

- **Direct Push to main-dev**: Always use PR workflow
- **Force Push to Shared Branches**: Never force push backend-dev/frontend-dev
- **Merge Without Tests**: All checks must pass before merge
- **Ignore Conflicts**: Resolve conflicts immediately, don't accumulate debt
- **Skip Code Review**: Every PR needs review, even "trivial" changes

---

## Troubleshooting

### Issue: Merge Conflict on main-dev

**Symptom**: Git merge conflict quando synchi da main-dev

**Solution**:
```bash
# Update local main-dev
git checkout main-dev && git pull

# Try merge into your branch
git checkout feature/your-branch
git merge main-dev

# Resolve conflicts
# Edit conflicted files
git add .
git commit -m "chore: resolve merge conflicts with main-dev"
```

### Issue: API Client Out of Sync

**Symptom**: TypeScript errors su API types dopo backend merge

**Solution**:
```bash
cd apps/web
pnpm generate:api
git add src/lib/api/generated
git commit -m "chore: regenerate API client"
```

### Issue: Migration Conflict

**Symptom**: EF migration conflict dopo backend merge

**Solution**:
```bash
cd apps/api/src/Api

# Remove your migration
dotnet ef migrations remove

# Pull latest
git pull origin main-dev

# Re-create your migration
dotnet ef migrations add YourMigrationName
```

### Issue: Divergent Long-lived Branches

**Symptom**: frontend-dev/backend-dev troppo divergenti da main-dev

**Solution**:
```bash
# Forced sync (use with caution!)
git checkout backend-dev
git fetch origin
git reset --hard origin/main-dev
git push --force-with-lease origin backend-dev

# Coordinate with team before doing this!
```

---

## References

- **ROADMAP**: `docs/ROADMAP.md`
- **Git Conventions**: `docs/02-development/git-conventions.md`
- **PR Template**: `.github/pull_request_template.md`
- **CI/CD Workflows**: `.github/workflows/`

---

**Last Updated**: 2026-01-30
**Maintained By**: Development Team
**Review Cycle**: Quarterly or after major epic completions

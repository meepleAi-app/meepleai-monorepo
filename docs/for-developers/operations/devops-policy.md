# DevOps Policy — 3-Branch Strategy

> **Status**: Draft 2026-05-08 · **Owner**: badsworm@gmail.com · **Last review**: 2026-05-08

Documento di policy che fissa target numerici, gating, e responsabilità per i 3 branch long-lived.

## 1. Branch model

| Branch | Ruolo | Deploy target | CI gate sync | CI deep |
|---|---|---|---|---|
| `feature/issue-N` | Working branch | none | trigger su PR open | inherits da target branch |
| `main-dev` | Local-first dev | none (locale solo) | lint + typecheck + unit-smoke ≤3 min | async post-merge (E2E/integration) |
| `main-staging` | Staging online | https://meepleai.app | full CI (incl. E2E/perf/security) ≤20 min | inclusi in CI standard |
| `main` | Production | (deploy on-demand manuale) | full CI deep + manual approval | + load test + pentest |

**Promotion flow**:
```
feature/issue-N → PR → main-dev (CI fast async) → cherry-pick → main-staging (CI full + auto-deploy) → manual gate → main (CI deep + manual deploy)
```

**🔴 Rule**: feature branch PR target = **parent branch**, NOT main. Vedi CLAUDE.md "CRITICAL PR Rule".

## 2. CI Gating Policy

### 2.1 Pull Request → main-dev

**Sync gate (BLOCCA merge)** — target ≤3 min P95:
- `lint` (ESLint frontend + dotnet format backend)
- `typecheck` (TypeScript + .NET compile)
- `unit-smoke` (subset rapido: unit test domain logic, no DB, no testcontainers)

**Async checks (NON bloccano merge)** — eseguiti post-merge su `main-dev`:
- Full unit test suite
- Integration test (testcontainers)
- E2E Playwright
- Visual regression
- Bundle size check
- A11y test

**Failure handling**:
- Singolo async fail → auto-issue assegnata all'autore
- 3 async fail consecutive su `main-dev` → branch protection blocca merge nuovi PR fino a verde
- Auto-revert: out of scope wave 1 (manuale)

### 2.2 Pull Request → main-staging

**Sync gate (BLOCCA merge)** — target ≤20 min:
- Tutto il sync gate di main-dev
- Full unit + integration suite
- E2E Playwright completo
- Visual regression
- Security scan (Trivy, CodeQL)

### 2.3 Pull Request → main

**Sync gate (BLOCCA merge)** — target ≤30 min:
- Tutto il main-staging gate
- Load test (k6)
- Penetration test scan
- **Manual approval** richiesta da superadmin (Aaron)

**Deploy a prod**: NON automatico. Eseguito manualmente da Aaron via `make prod` su VPS dopo merge.

## 3. Deploy responsibilities

| Env | Trigger | Responsabile failure | SLA recovery |
|---|---|---|---|
| Local dev | `make dev-core` | Developer | self |
| Staging | Auto su push `main-staging` | Aaron via GitHub Actions | ≤30 min rollback |
| Prod | Manuale via `make prod` | Aaron sul VPS | ≤2h rollback |

**Staging rollback**: auto via deploy-staging.yml se smoke fallisce. Manual via `git revert` + redeploy se discovered post-deploy.

**Prod rollback**: manuale solo. `git revert` HEAD su `main` + `make prod`.

## 4. Staging access (wave 1)

Staging richiede email allowlist. Solo email in `STAGING_ALLOWED_EMAILS` env var possono autenticarsi.

Implementazione: middleware ASP.NET `StagingAccessMiddleware` in `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Middleware/`. Attivato solo quando `ASPNETCORE_ENVIRONMENT=Staging`. Risposta 403 con JSON `{code:"STAGING_ACCESS_DENIED", message, contactEmail}` se email non in lista.

**Default safe**: allowlist vuota = pass-through (dev/prod non sono impattati). Su Staging l'app logga warning all'avvio se la lista è vuota (misconfig window).

**Wave 1 list iniziale** (decisione 2026-05-08):
- badsworm@gmail.com (Aaron, project owner)
- _(da popolare manualmente da Aaron, max 5 in wave 1)_

**Add/remove email**: edit `infra/secrets/admin.secret` → `STAGING_ALLOWED_EMAILS=email1,email2,...` → restart staging API container (`make staging-down && make staging-minimal`).

**Wave 2** (futuro): integrare con esistente `AccessRequest` BC (`apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/`) + admin UI per invite system.

## 5. Decisions log

| Date | Decision | Rationale |
|---|---|---|
| 2026-05-08 | Target PR-to-merge `main-dev` = **5 min P95** | Compromise tra 2 min (troppo aggressivo) e status quo |
| 2026-05-08 | Sync gate `main-dev` = lint+typecheck+unit-smoke (opzione c) | Bilanciamento velocità vs detection di issue ovvi |
| 2026-05-08 | Wave 1 = email allowlist (env var) NOT AccessRequest | Velocità impl: 1 giorno vs 3-4 per UI integration |
| 2026-05-08 | Branch naming invariato (`main-dev`, `main-staging`, `main`) | Refactor cost (36 workflow + Makefile + docs) > convention benefit |
| 2026-05-08 | Prod deploy resta manuale | Safety: zero auto-deploy a prod nel primo trimestre |
| 2026-05-08 | Mobile testing = Playwright mobile emulation per CI, real device per Aaron | Real device costoso; emulation copre 95% regressions |
| 2026-05-08 | Middleware wire in `WebApplicationExtensions.ConfigureAuthMiddleware`, NOT Program.cs | Preserva pipeline layering esistente; review-feedback B3 |
| 2026-05-08 | Env check via `app.Environment.IsEnvironment("Staging")` | Match `ASPNETCORE_ENVIRONMENT: Staging` di compose.staging.yml; review-feedback B1 |
| 2026-05-08 | Secrets in `admin.secret.example` (NOT nuovo `api.secret.example`) | File esistente già contiene `STAGING_DEMO_EMAIL`; review-feedback B2 |

## 6. Open questions (carry-forward)

- **D2**: dev-core RAM peak attuale (data collection necessaria prima di target ≤8GB). Action: misurare con `docker stats` su `make dev-core` cold start. Comando baseline: `docker stats --no-stream --format "table {{.Name}}\t{{.MemUsage}}"`
- **D4**: auto-rollback staging — gating su quale smoke metric? (200 OK su /healthz oppure broader auth+chat smoke?)
- **D1**: split CI fast/deep — quanti test ridurre dal sync gate? Data baseline necessaria. Comando collection: `gh run list --workflow=ci.yml --branch=main-dev --limit=50 --json conclusion,startedAt,updatedAt`
- **Mobile staging visibility**: banner "STAGING" cosa contiene oltre il testo? Versione SHA? Nome ambiente?
- **OAuth claim mapping**: verificare che provider OAuth (Google/GitHub) popolino `ClaimTypes.Email` o se serve fallback a `"email"` claim. Action: revisione `OAuthConfiguration.cs` + smoke staging con Google login.

## 7. Riferimenti

- Spec-panel analysis: 2026-05-08 (D1-D5 SMART + Socratic)
- Plan: `docs/superpowers/plans/2026-05-08-devops-3branch-optimization.md`
- Existing infra: `infra/Makefile` (35+ targets), `.github/workflows/` (36 workflow)
- Branch protection: GitHub Settings (NOT in repo)
- Esistente: `AccessRequest` BC in `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/`

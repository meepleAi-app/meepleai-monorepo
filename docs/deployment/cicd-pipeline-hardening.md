# CI/CD Pipeline Hardening — Implementation Guide

> Epic #171 | Expert panel analysis by Nygard, Fowler, Hightower, Newman, Crispin

## Overview

This document describes the comprehensive CI/CD pipeline improvements identified through expert spec-panel review. It covers 17 issues organized by priority, with specific implementation guidance for each.

---

## Architecture: Current vs Target

### Current Pipeline

```
feature/* ──PR──► main-dev ──merge──► main-staging ──merge──► main
                    │                     │                    │
                    ▼                     ▼                    ▼
                  CI Tests           Deploy Staging       Deploy Prod
                (PR to main/        (auto on push)      (approval gate)
                 main-staging       meepleai.app         meepleai.com
                 ONLY)
```

**Problems identified**:

| Category | Issue | Severity |
|----------|-------|----------|
| Pipeline Gap | CI doesn't run on PRs to `main-dev` | 🔴 Critical |
| Test Quality | Test failures don't block deploys | 🔴 Critical |
| Domain | Workflows reference wrong staging URL | 🔴 Critical |
| Validation | Smoke tests are empty placeholders | 🔴 Critical |
| GitOps | Server config not in git | 🔴 Critical |
| Deploy | No selective frontend/backend deploy | 🟡 Important |
| Deploy | Separate images for staging vs prod | 🟡 Important |
| DB | Migrations coupled with deploy | 🟡 Important |
| Health | Only basic HTTP 200 check | 🟡 Important |
| Recovery | No automated rollback | 🟡 Important |
| Promotion | No PR gate for branch promotion | 🟡 Important |

### Target Pipeline

```
feature/* ──PR──► main-dev ──PR──► main-staging ──PR──► main
    │                │                  │                 │
    ▼                ▼                  ▼                 ▼
  CI (full)       CI (full)          CI (full)         CI (full)
  ✅ gate          ✅ gate            ✅ gate            ✅ gate
                                       │                 │
                                       ▼                 ▼
                              ┌──── Detect ────┐   ┌── Promote ──┐
                              │  Changes       │   │  Images     │
                              ▼         ▼      │   │  (staging   │
                          Build API  Build Web │   │   SHA)      │
                              │         │      │   │             │
                              ▼         ▼      │   ▼             │
                          Migrate DB (if any)  │   Approval Gate │
                              │                │       │         │
                              ▼                │       ▼         │
                          Deploy Changed       │   Migrate DB    │
                              │                │       │         │
                              ▼                │       ▼         │
                          Deep Health Checks   │   Deploy        │
                              │                │       │         │
                              ▼                │       ▼         │
                          Smoke Tests (real)   │   Health+Smoke  │
                              │                │       │         │
                              ▼                │       ▼         │
                          Notify               │   Release+Notify│
```

---

## Implementation Phases

### Phase 1: Critical Fixes (Week 1)

These issues directly impact deployment correctness and should be implemented first.

#### C1: Fix Staging Domain (#172)

**Files**: `deploy-staging.yml`, `deploy-production.yml`

Replace all instances of `staging.meepleai.com` with `meepleai.app`:
- Health check URLs in deploy steps
- Environment URLs in job definitions
- Smoke test endpoints
- Documentation references

#### C2: Add main-dev to CI (#173)

**File**: `ci.yml` line 5

```yaml
# Change
branches: [main, main-staging]
# To
branches: [main, main-dev, main-staging]
```

Also configure GitHub branch protection on `main-dev` to require CI to pass.

#### C3: Remove Test Bypass (#174)

**File**: `ci.yml` lines 259, 282, 297

1. Remove `continue-on-error: true` from test step
2. Remove `|| true` from `dotnet test` commands
3. Split unit and integration tests into separate steps
4. Keep generous `timeout-minutes` for ARM64

#### C4: Real Smoke Tests (#175)

**Files**: `deploy-staging.yml`, `deploy-production.yml`

Replace placeholder echo statements with actual endpoint verification:
- `/health` — API deep health
- `/scalar/v1` — API docs accessible
- `/api/v1/shared-game-catalog/games?limit=1` — Game catalog responding
- `/` — Frontend rendering (check for `__next`)
- `/api/v1/auth/me` — Auth endpoint reachable (expect 401, not 500)

#### C5: GitOps Config (#176)

SSH to server, copy config files, sanitize, commit:
- `compose.staging-traefik.yml` → add to repo
- `env/*.env` → create `.example` templates

---

### Phase 2: Reliability (Weeks 2-3)

#### I1: Separate DB Migrations (#177)

Create a dedicated `migrate-db` job that:
1. Checks for pending migrations
2. Creates a database backup
3. Applies migrations
4. Runs BEFORE deploy job (not inside it)

#### I2: Selective Deploy (#178)

Add `dorny/paths-filter` to deploy workflows:
- API changes → build + deploy API only
- Web changes → build + deploy Web only
- Both → full deploy
- `workflow_dispatch` → option to force full deploy

#### I3: Image Promotion (#179)

1. Remove `ASPNETCORE_ENVIRONMENT` from build args (make it runtime env var)
2. Staging: build + tag `staging-<sha>`
3. Production: pull staging tag → retag as `v1.2.3` + `latest`
4. Note: Next.js frontend requires separate builds (NEXT_PUBLIC_* baked at build time)

#### I4: Deep Health Checks (#180)

Enhance `/health` endpoint to return structured JSON with individual service status:
- Database: connection pool, query latency
- Redis: ping, memory usage
- Qdrant: readiness, collection count
- Version: deployed image tag

#### I5: Automated Rollback (#181)

1. Auto-rollback on deploy failure (previous known-good image)
2. Manual rollback workflow (`rollback.yml`)
3. Rollback notification via Slack

#### I6: PR-Based Promotion (#182)

Configure branch protection:
- `main-staging`: require PR + CI pass
- `main`: require PR + CI pass + 1 approval
- Block direct pushes to both branches

---

### Phase 3: Maturity (Weeks 4+)

#### R1: Post-Deploy E2E (#183)

Run Playwright smoke suite against live staging after deploy.

#### R2: Performance Baseline (#184)

Measure and record endpoint response times per deployment.

#### R3: Version File (#185)

Write `DEPLOYMENT.json` on server with version, image tags, timestamp, actor.

#### R4: Runner Alerting (#186)

Notify when self-hosted runner is unavailable and GitHub-hosted fallback is used.

#### R5: Changelog (#187)

Configure `.github/release.yml` for categorized auto-generated release notes.

---

## Infrastructure Reference

| Component | Current | Target |
|-----------|---------|--------|
| Staging Server | Hetzner CAX21 (4 vCPU, 8GB RAM) | Same |
| Staging Domain | meepleai.app (Cloudflare) | Same |
| Production Domain | meepleai.com | Same |
| Container Registry | GHCR (ghcr.io) | Same |
| CI Runner | Oracle ARM64 self-hosted | Same + alerting |
| Deploy Method | SSH (appleboy/ssh-action) | Same + rollback |
| SSL | Cloudflare Origin Certificate | Same |
| Monitoring | Prometheus + Grafana + AlertManager | Same + pipeline metrics |

## Dependencies Between Issues

```
C1 (domain fix) ← no deps, do first
C2 (CI triggers) ← no deps, do first
C3 (test gating) ← no deps, do first
C4 (smoke tests) ← depends on C1 (correct domain)
C5 (GitOps) ← no deps, do first

I1 (migrations) ← no deps
I2 (selective deploy) ← no deps
I3 (image promotion) ← no deps
I4 (deep health) ← backend code change + C1
I5 (rollback) ← I1 (separate migration helps)
I6 (PR promotion) ← C2 (CI must run on main-dev first)

R1 (E2E staging) ← C4 (smoke tests first), I2 (selective deploy)
R2 (perf baseline) ← C4 (smoke tests infra)
R3 (version file) ← no deps
R4 (runner alert) ← no deps
R5 (changelog) ← I6 (PR-based promotion needed for PR titles)
```

## Effort Estimates

| Priority | Issues | Total Effort |
|----------|--------|-------------|
| 🔴 Critical | C1-C5 | ~3 hours |
| 🟡 Important | I1-I6 | ~16 hours |
| 🟢 Recommended | R1-R5 | ~3 days |
| **Total** | **17 issues** | **~5 days** |

---

*Created: 2026-03-11 | Epic: #171*
*Experts: Nygard (Release It!), Fowler (CI/CD), Hightower (Cloud-native), Newman (Microservices), Crispin (Agile Testing)*

# ADR-054 — DevOps Multi-Branch Strategy

**Status**: Accepted
**Date**: 2026-05-08
**Deciders**: @badsworm
**Tracking**: Epic [#842](https://github.com/meepleAi-app/meepleai-monorepo/issues/842) and sub-issues [#843](https://github.com/meepleAi-app/meepleai-monorepo/issues/843)–[#850](https://github.com/meepleAi-app/meepleai-monorepo/issues/850).

## Context

MeepleAI is moving from a single-branch development model into a small beta with external users. We need a branching and CI/CD layout that:

- Keeps **`main-dev`** iteration velocity high (this is where almost all feature work lands daily).
- Provides a **realistic staging surface** for invited users without spinning up dedicated infrastructure that the project cannot yet afford.
- Protects **production** with stricter gates, rollback procedures, and manual approval.

Hard constraints driving the decision:

- **Single host**: developer/operator runs all environments on one 32 GB workstation (Hetzner CAX21 ARM64 is the staging VPS, dev + ops also share local hardware for the heavy lifting). No additional servers are budgeted for staging.
- **Beta scale**: a handful of invited users (`badsworm@gmail.com` is the bootstrap allowlist, see [#845](https://github.com/meepleAi-app/meepleai-monorepo/issues/845)). Traffic is sub-100 req/min, so co-tenancy patterns that would not survive higher load are acceptable here.
- **CI minutes budget**: ~3,100 GitHub Actions minutes/month is the soft target; the workflow split must respect that envelope (tracked in [#850](https://github.com/meepleAi-app/meepleai-monorepo/issues/850)).
- **One-person ops**: any rollback / incident response must be doable by a single operator without team coordination.

## Problem

A trunk-based model has been the historical default but it does not separate the kinds of regressions we expect on different branches:

- Frequent flakes on `main-dev` (smoke streaks, build drift) bleed into staging confidence.
- Direct deploys from `main-dev` to a public surface expose half-finished work to invited users.
- Without an intermediate gate, production hotfixes have no rehearsal environment.

At the same time, a full trunk-based deployment matrix (per-PR preview environments, ephemeral staging) is disproportionate to the current scale and the single-host constraint.

## Decision

We adopt **three long-lived branches** with differentiated SLOs:

| Branch | Environment | Host | Blocking CI on PR | Async CI post-merge | Deploy trigger |
|--------|-------------|------|-------------------|----------------------|----------------|
| `main-dev` | Local | Dev machine 32 GB | lint + typecheck + unit (< 3 min) | integration + E2E + smoke (≤ 30 min, auto-revert bot if red > 30 min — see [#843](https://github.com/meepleAi-app/meepleai-monorepo/issues/843)) | — |
| `main-staging` | Staging | Hetzner CAX21 + co-tenant on dev machine where applicable | full suite (< 10 min) | — | Auto on merge to `main-staging` |
| `main` | Production | TBD (see [#848](https://github.com/meepleAi-app/meepleai-monorepo/issues/848)) | exhaustive (< 30 min) | — | Manual + explicit approval, rolling with healthcheck gates |

Concrete operational rules:

1. **`main-dev` is the working trunk.** Feature branches `feature/issue-N-*` MUST target `main-dev`. Auto-revert if the branch stays red for more than 30 minutes ([#843](https://github.com/meepleAi-app/meepleai-monorepo/issues/843)).
2. **`main-staging` is promoted from `main-dev`** via a single fast-forward PR per release window. CI on this PR runs the full suite. Merge triggers `docker compose -f compose.staging.yml up -d` against the staging host (see [#844](https://github.com/meepleAi-app/meepleai-monorepo/issues/844) for the co-tenant compose layout with resource limits + Traefik routing).
3. **`main` is promoted from `main-staging`** via a manual PR. Deploy is rolling with healthcheck gates and requires explicit approval. Rollback path is the manual image-tag revert documented in the prod runbook ([#848](https://github.com/meepleAi-app/meepleai-monorepo/issues/848)).
4. **Access control on staging**: Wave 1 uses a `staging_allowlist` table seeded with `badsworm@gmail.com` and an admin endpoint to manage it ([#845](https://github.com/meepleAi-app/meepleai-monorepo/issues/845)). Wave 2 adds a full `Invitation` aggregate in the Authentication BC ([#847](https://github.com/meepleAi-app/meepleai-monorepo/issues/847)) with token + expiry + invited-by + redeemed-at semantics.
5. **Workflow split per branch**: GitHub Actions workflows are split into `dev-fast`, `dev-async`, `staging`, `prod` files to keep blocking CI lean on the hot path ([#846](https://github.com/meepleAi-app/meepleai-monorepo/issues/846)).
6. **CI cost discipline**: workflow caching, concurrency cancellation on stale runs, and `paths-ignore` filters keep the GitHub minutes budget on target ([#850](https://github.com/meepleAi-app/meepleai-monorepo/issues/850)).
7. **Mobile testing remains manual** for the beta window. Cypress/Playwright runs on desktop viewports only.

## Consequences

### Positive

- **Hot-path velocity preserved**: `main-dev` blocking CI is intentionally narrow (< 3 min); the expensive checks are async and feedback the contributor without blocking merges.
- **Realistic staging surface**: invited users see a build that has passed the full suite, served from a host configuration that mirrors production except for resource limits.
- **Rollback is rehearsed**: every production deploy follows a staging deploy with the same image, so the rollback path has been exercised under realistic conditions.
- **One-operator safe**: auto-revert on `main-dev` and manual approval on `main` mean that any single CI break has a bounded blast radius even when the operator is offline.

### Negative

- **Co-tenancy fragility**: the dev workstation hosting both `make dev` and `make staging` can race for ports / RAM if pruning discipline lapses (see [Docker Maintenance](../../../../docs/for-developers/operations/operations-manual.md#docker-maintenance) and the related ops notes about cleaning images before rebuilds). Accepted for beta scale.
- **CI complexity**: four workflow files instead of one is more to maintain. Mitigation: each file has a clear scope (fast PR, async post-merge, staging deploy, prod deploy) and shared steps live in reusable workflows.
- **Three branches to keep in sync**: drift between `main-staging` and `main` is possible if hotfixes land directly on `main`. Mitigation: hotfixes also fast-forward back into `main-staging` and `main-dev` in the same PR series.

### Risks

- **Dev machine outage = staging outage**: accepted for the beta window. When the user base outgrows the single-host model, staging should move to a dedicated VPS (see *Alternatives*).
- **GitHub Actions minute exhaustion**: if the async post-merge suite expands faster than the budget, paid minutes or self-hosted runner addition is the escape hatch (we already have a self-hosted ARM64 runner per [ADR-044](./adr-044-self-hosted-arm64-runner.md)).

## Alternatives Considered

1. **Trunk-based development with PR preview environments**.
   *Rejected*: per-PR preview environments require either container orchestration infrastructure we don't run yet, or budget for a Fly.io / Render-style provider. The staging surface is also not isolated from in-flight feature work, which defeats the "invited users see a stable build" goal.

2. **Staging on a dedicated VPS, separate from the dev machine**.
   *Deferred*: this is the right end state. It is gated on the user base outgrowing the co-tenant resource budget, or on the ops cost becoming small relative to the value delivered. Tracked implicitly under the [#842](https://github.com/meepleAi-app/meepleai-monorepo/issues/842) epic close-out criteria.

3. **Blue/green production deployments**.
   *Deferred*: overkill for current scale. A rolling-with-healthcheck deploy + image-tag rollback gives us the recoverability we need at a fraction of the operational complexity. Procedure documented in [`rollback-runbook.md`](../../../for-developers/operations/rollback-runbook.md); upgrade triggers in [§15 future upgrade path](../../../for-developers/operations/rollback-runbook.md#15-future-upgrade-path).

4. **Auto-deploy from `main-dev` straight to staging** (skip `main-staging`).
   *Rejected*: removes the gate where the full suite runs blocking, which is exactly the friction that protects invited users from `main-dev` flakes.

## References

- Epic: [#842 — DevOps Multi-Branch Strategy](https://github.com/meepleAi-app/meepleai-monorepo/issues/842)
- Auto-revert bot: [#843](https://github.com/meepleAi-app/meepleai-monorepo/issues/843)
- Staging compose layout: [#844](https://github.com/meepleAi-app/meepleai-monorepo/issues/844)
- Access control Wave 1: [#845](https://github.com/meepleAi-app/meepleai-monorepo/issues/845)
- Workflow split: [#846](https://github.com/meepleAi-app/meepleai-monorepo/issues/846)
- Invitation aggregate Wave 2: [#847](https://github.com/meepleAi-app/meepleai-monorepo/issues/847)
- Prod rollback runbook: [#848](https://github.com/meepleAi-app/meepleai-monorepo/issues/848) → [`rollback-runbook.md`](../../../for-developers/operations/rollback-runbook.md)
- CI cost optimisation: [#850](https://github.com/meepleAi-app/meepleai-monorepo/issues/850)
- Self-hosted ARM64 runner: [ADR-044](./adr-044-self-hosted-arm64-runner.md)

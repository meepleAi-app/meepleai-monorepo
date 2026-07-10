# 🔒 Quarterly Security Review — Q3 2026

> **Authored from**: `docs/for-developers/security/security-review-template.md` v2 (hardened post spec-panel review of #186)
> **Issue**: #2655
> **Status**: 🟢 Remediation complete — closeout report (draft at T0+8d, within SC-2 T0+14d deadline)

**Review Period**: 2026-07-01 (T0) → 2026-09-30 (T0+90d)

**Status timeline**:
- T0 = 2026-07-01 (issue #2655 opened — Q3 quarter start)
- T0+14d = 2026-07-15 (draft due — **ON TIME**: this draft authored 2026-07-09 = T0+8d)
- T0+30d = 2026-07-31 (critical fix deadline — **already met**: all critical/high remediated 2026-07-07 = T0+6d)
- T0+90d = 2026-09-30 (close deadline)

### Execution note

Unlike Q2 (which slipped ~3 weeks past T0+14d on doc blockers), Q3 remediation ran same-week: the review scan (7-auditor pass) surfaced **11 real authorization/SSRF findings**, all fixed and merged by 2026-07-07 (T0+6d), each via dedicated branch → TDD → adversarial code review → PR to `main-dev`. This report is the closeout artifact.

---

## 👥 RACI

| Role | Person | Responsibility |
|------|--------|----------------|
| **Responsible** (executes) | `@DegrassiAaron` | Single-maintainer; runs scans, drafts report, coordinates fixes |
| **Accountable** (owns outcome) | `@DegrassiAaron` | Sign-off |
| **Consulted** | n/a (single-maintainer team) | — |
| **Informed** | Future contributors via this doc | — |

> ⚠️ **Single-maintainer context**: same person maintains the codebase and runs the review. Bus-factor mitigation: this report is the durable artifact; resume from §Action Items.

---

## Executive Summary

**Overall Security Posture**: 🟢 **Good** — 0 critical, 0 high in dependencies; the sole open CodeQL "high" is test-code substring matching (false positive, dismissable). The material Q3 work was closing an **11-finding IDOR/SSRF cluster** that spanned 5 bounded contexts.

### Snapshot at scan time (2026-07-07, frozen)

| Source | Critical | High | Medium | Low | Total |
|--------|----------|------|--------|-----|-------|
| **Authorization audit (secrets-authz scan)** | 4 | 7 | 0 | 0 | **11** |
| **CodeQL** | 0 | 2 | 4+ | — | **12** triaged |
| **Backend deps (.NET)** | 0 | 0 | 1 (carried from Q2) | 0 | **1** |
| **Frontend deps (pnpm)** | 0 | 0 | 5 | 3 | **8** |
| **gitleaks (in-scope)** | 0 | 0 | 0 | 0 | **0** (not re-run this cycle; last clean Q2) |

### State after remediation (2026-07-07)

| Source | Critical | High | Medium | Low | Total | Δ |
|--------|----------|------|--------|-----|-------|---|
| **Authorization audit** | **0** | **0** | 0 | 0 | **0** | **−11** (all fixed + merged) |
| **CodeQL** | 0 | **1** | 0 | — | **1** | **−11** (fixed/dismissed; 1 test-code FP remains, dismissable) |
| **Backend deps (.NET)** | 0 | 0 | 1 | 0 | **1** | 0 (OTel — carried to §Action Items) |
| **Frontend deps (pnpm)** | 0 | 0 | 5 | 3 | **8** | dev-only transitive; 0 runtime high/critical |

### Key Findings

1. **11 authorization/SSRF vulnerabilities** surfaced by the secrets-authz scan and **all remediated same-week** (see §1.6): 4 Critical GameSession IDOR (+ `/end` sibling), 2 High AgentMemory IDOR, 2 High CWE-639 query-string authz (+ `UpdateMediaCaption` sibling), 1 admin-impersonation `/end` IDOR, 2 SSRF (BggCoverDownloader, SlackWebhookClient). Identity is now 100% server-derived; SSRF paths reuse the shared `SsrfSafeHttpClient` (HTTPS-only + private-IP block, fail-closed).
2. **12 CodeQL alerts triaged**: STEP 1–4 remediated (`safe-loader.ts` regex anchor #2742, `appsettings.json` empty password verified inert #2743, `ChannelDispatchHandler` MEDIUM cluster #2744) + 4 false positives dismissed with justification (enum values ≠ PII).
3. **Sole open CodeQL "high"** (#652, `js/incomplete-url-substring-sanitization`) is in **test code** (`SharedGameDetailModal.test.tsx`) — a test asserting a URL substring, no production attack surface. Dismissable as FP.
4. **Frontend deps clean of high/critical**: pnpm audit reports 8 vulns (3 low / 5 moderate), all **dev-only transitive** (playwright-lighthouse → sentry → @opentelemetry/core, etc.). No runtime exposure.
5. **Adversarial review value**: on 3 of the 5 fix PRs, the adversarial code-review step surfaced a **sibling endpoint with the same hole** (the `/end` alias on GameSession, `UpdateMediaCaption`, the impersonation `/end` route) — fixed in the same PR. Fixing in the *handler* (not the route) covered duplicated/legacy routes automatically.

### Next Quarter Priorities (Q4)

1. Execute **P1.3 OpenTelemetry** coordinated upgrade (carried Q2→Q3→Q4; single Moderate GHSA-g94r-2vxg-569j).
2. Execute **P1.4b Trivy** full-scope (image + fs + config scan + CI gate) — deferred Q2.
3. **SBOM generation** cadence (deferred Q2 §2.3, Q3).
4. **DAST baseline** (OWASP ZAP + Schemathesis) — deferred Q2 §5.
5. Extract the cross-BC `SlackWebhookClient` SSRF guard into a SharedKernel primitive (pragmatic reuse of `SsrfSafeHttpClient` shipped in #2753; long-term consolidation).

---

## Continuity from Previous Quarter

**Previous review**: [2026-Q2-security-review.md](./2026-Q2-security-review.md)

**Q2 items carried forward** (Q2 §Continuity to Q3):

| Item | Origin | Q3 disposition |
|------|--------|----------------|
| P1.1-B 2FA admin strict mode (post shadow-mode telemetry) | Q2 P1.1 | ⏳ Deferred to Q4 — shadow-mode adoption telemetry not yet confirmed ≥90% |
| P1.3 OpenTelemetry coordinated upgrade | Q2 P1.3 | ⏳ Carried to Q4 — 1 Moderate, no exploit path, low priority |
| P1.4b Trivy full scope | Q2 P1.4b | ⏳ Carried to Q4 (tooling install) |
| SBOM generation | Q2 §2.3 | ⏳ Carried to Q4 |
| DAST baseline | Q2 §5 | ⏳ Carried to Q4 |

**Trend (rolling 3 quarters)**:

| Metric | Q1 2026 | Q2 2026 | Q3 2026 | Direction |
|--------|---------|---------|---------|-----------|
| Critical findings (T0 snapshot) | TBD | 2 | 4 (authz IDOR) | ↑ then →0 same-week |
| High findings (T0 snapshot) | TBD | 50 | 7 (authz) + 2 (CodeQL) | ↓ |
| Critical carried forward (T0+90d) | TBD | 0 | 0 | → |
| High carried forward | TBD | 0 | 0 (1 test-code FP dismissable) | → |
| MTTR critical (days) | TBD | same-day | **6** (T0→fix) | ✅ ≤7 |

> Note: the Q3 T0 critical count (4) is higher than Q2 (2), but these are *newly surfaced by a deeper authz-focused scan*, not a regression — and all closed within MTTR target.

---

## 0. Threat Model Refresh

### 0.1 Attack Surface Inventory

- [x] Public endpoints: OpenAPI at `/openapi/v1.json` (minimal API + MediatR/CQRS endpoints)
- [x] Auth surfaces: cookie session, OAuth (Google/Discord/GitHub), 2FA TOTP, API keys, **admin impersonation** (this quarter's #9 finding)
- [x] Data classification: PII (email, OAuth tokens), Secrets (hashes, API keys), Untrusted input (PDF uploads)
- [x] Trust boundaries: browser → Cloudflare Tunnel → API → DB/Redis → AI sidecars; **outbound fetch** (BGG cover download, Slack webhook — this quarter's #10/#11 SSRF findings)
- [x] Third-party integrations: BGG (freeze per ADR-059), OAuth providers, Slack webhook (admin-configured), embedding/reranker/PDF services

### 0.2 STRIDE per Tier-1 BC (this quarter — authz focus)

| BC | Spoofing | Tampering | Repudiation | Info Disclosure | DoS | Elevation |
|----|----------|-----------|-------------|------------------|-----|-----------|
| Authentication | ✅ | ✅ | ⚠️ | ✅ | ⚠️ | ✅ **impersonation `/end` IDOR fixed #2751** |
| Administration | ✅ | ✅ | ✅ AuditLog | ✅ RBAC | ⚠️ | ✅ |
| GameManagement (sessions) | ✅ | ✅ **ownership guard added #2746** | ⚠️ | ✅ | ⚠️ | ✅ **4 IDOR fixed** |
| AgentMemory | ✅ | ✅ **group IDOR fixed #2747** | ⚠️ | ✅ | n/a | ✅ |
| KnowledgeBase | n/a | ⚠️ prompt injection (RAG) | ⚠️ | ⚠️ | ❌ AI cost cap TBD | n/a |

Legend: ✅ controlled · ⚠️ partial · ❌ gap

### 0.3 Top Kill Chains (this quarter)

#### Kill Chain 1: Cross-user session tampering (IDOR) — **CLOSED**
- **Path**: authenticated user A guesses/enumerates session id owned by user B → `POST /sessions/{id}/complete|abandon|pause|resume|end` → mutates B's session state
- **Preventive**: ownership guard in the command *handler* (`ForbiddenException` when `CreatedByUserId != requester`); not-found normalized to 404 (#2568)
- **Validation test**: `Category=SecurityRegression` — TDD per fix PR #2746 (70/70 unit tests)

#### Kill Chain 2: SSRF via admin-configured outbound fetch — **CLOSED**
- **Path**: attacker-influenced `remoteImageUrl` (BGG cover) or admin-configured Slack webhook URL → server fetches internal/metadata endpoint (169.254.169.254, localhost services)
- **Preventive**: `SsrfSafeHttpClient` validators — HTTPS-only + private-IP/loopback block, fail-closed — reused across both call sites (#2753)
- **Validation test**: offline SSRF tests with IP literals (avoid DNS-flaky CI; RFC5737 `203.0.113.10` happy-path)

### 0.4 Diff vs Q2 2026

- **New attack surface reviewed**: outbound fetch paths (BGG cover downloader, Slack webhook) — previously unaudited for SSRF
- **Reduced surface**: BGG user-side asset ban (ADR-059, #2123) removed browser→geekdo image fetches
- **Deepened review**: this quarter's scan was authz-focused (IDOR across session/memory/media/chat/impersonation), surfacing findings prior CodeQL-only scans missed

---

## 1. CodeQL Security Scans

### 1.1 Scan Results (current, 2026-07-09)

**Open alerts**: **1 high + 2 warning** (queried live via `gh api code-scanning/alerts`).

| Severity | Open now | Fixed this Q | Dismissed FP | Notes |
|----------|----------|--------------|--------------|-------|
| Critical | 0 | — | — | |
| High | **1** | STEP 1 (#2742) | STEP 2 verified inert + 4 enum≠PII | The 1 open is test-code (see §1.2) |
| Medium | 0 | STEP 3-4 (#2744) | | `ChannelDispatchHandler` cluster |
| Warning | 2 | — | — | Informational |

### 1.2 Sole open HIGH — triage (dismissable FP)

- **Alert #652**: `js/incomplete-url-substring-sanitization`
- **Location**: `apps/web/src/components/shared-games/__tests__/SharedGameDetailModal.test.tsx` (created 2026-05-22)
- **Assessment**: **false positive** — the flagged substring URL check is inside a **test file** asserting a rendered link, not a production security control. No runtime attack surface.
- **Action**: dismiss with justification `used-in-tests` (deferred to workshop; does not block posture).

### 1.3 STEP 1–4 remediation (known CodeQL findings)

| Step | Finding | Action | PR |
|------|---------|--------|----|
| 1 | 🟠 HIGH `js/regex/missing-regexp-anchor` — `safe-loader.ts:84` unanchored BGG-host substring regex (false beacon → false P1) | Replaced with hostname-parsed `isBlockedImageHost` (anchored). Real allowlist gate already safe. | **#2742 ✅** |
| 2 | 🟠 HIGH `js/empty-password-in-configuration-file` — `appsettings.json:418` empty `Alerting:Email:Password` | Verified inert optional-auth placeholder (`Credentials=null` unless `Username` set). Documented; no secret committed. | **#2743 ✅** |
| 3–4 | 🟡 MEDIUM `ChannelDispatchHandler` cluster (×4) | Remediated | **#2744 ✅** |
| — | 4× false positives (enum values flagged as sensitive-info exposure — enum ≠ PII) | Dismissed with justification | operational |

> Evidence: issue #2655 comments (2026-07-07 audit + remediation) + merged PRs.

---

## 1.6 Authorization & SSRF Remediation (headline Q3 work)

> This section is the material Q3 outcome: 11 real findings from the secrets-authz scan, all fixed + merged 2026-07-07. Each PR: dedicated branch → TDD → adversarial code review → PR to `main-dev`.

| # | Finding | Class (CWE) | Fix (handler-level) | PR | Status |
|---|---------|-------------|---------------------|----|--------|
| 1–4 | GameSession IDOR — `POST /sessions/{id}/complete\|abandon\|pause\|resume` mutable by any authenticated user | IDOR (CWE-639) | Ownership guard `ForbiddenException` when `CreatedByUserId != requester`; not-found → 404 | **#2746** (+ `/end` sibling surfaced by review) | ✅ merged |
| 5–6 | AgentMemory group IDOR — read (#7) + preferences (#8) accessible cross-group | IDOR (CWE-639) | Server-derived group membership check | **#2747** | ✅ merged |
| 7–8 | Media delete + chat delete — identity from query-string (client-controlled) | CWE-639 | Identity 100% server-derived; `ISessionRepository` resolves owning participant | **#2749** (+ `UpdateMediaCaption` sibling) | ✅ merged |
| 9 | `POST /admin/impersonation/end` — any admin could revoke any session id (force-logout / kill another admin's impersonation) | IDOR / priv-esc (CWE-639) | Guard in `ImpersonationEndCommandHandler` (404/403/403/idempotent); handler-level fix covers duplicated legacy route; `/revoke` twin already protected | **#2751** | ✅ merged |
| 10 | `BggCoverDownloader` fetched `remoteImageUrl` with no scheme/private-IP guard | SSRF (CWE-918) | Reuse `SsrfSafeHttpClient` (HTTPS-only + private-IP block, fail-closed) | **#2753** | ✅ merged |
| 11 | `SlackWebhookClient` fetched admin-configured webhook URL with no guard | SSRF (CWE-918) | Reuse `SsrfSafeHttpClient` | **#2753** | ✅ merged |

**Refuted during triage (documented, not fixed)**:
- `N8nWebhookClient` — **not** SSRF: `BaseUrl` is deployment-config (not runtime-tainted); target is an intentional internal service (`localhost:5678`).
- `DELETE /admin/sessions/{id}` — out of scope: general admin power, not an IDOR (admin role is authorized to delete any session by design).

---

## 2. Dependency & Supply Chain Security

### 2.1 Backend Dependencies (.NET)

**Not re-scanned this cycle** (dotnet restore + `--vulnerable` deferred). Carried from Q2: **1 Moderate** — `OpenTelemetry.Api 1.14.0` ([GHSA-g94r-2vxg-569j](https://github.com/advisories/GHSA-g94r-2vxg-569j)), transitive. Remediation = **P1.3 coordinated OTel upgrade**, carried Q2→Q3→Q4 (single Moderate, no exploit path).

### 2.2 Frontend Dependencies (pnpm)

**Scan Date**: 2026-07-09 · **Tool**: `pnpm audit --audit-level=moderate`

**Total**: 8 vulnerabilities (3 low / 5 moderate / **0 high / 0 critical**).

| Package | Severity | Path | Runtime/Dev |
|---------|----------|------|-------------|
| @opentelemetry/core (<2.8.0, W3C Baggage DoS) | Moderate | `playwright-lighthouse > lighthouse > @sentry/node > @opentelemetry/core` | **dev** |
| (others — transitive) | low/moderate | dev tooling (lighthouse, playwright) | **dev** |

**Runtime exposure**: **none** — all 8 are confined to dev/test tooling (Playwright, Lighthouse). No runtime path. No action required this quarter; will clear on the next dependency refresh.

### 2.3 SBOM / 2.4 Provenance / IaC (deferred)

⚠️ Still deferred (tooling not installed on maintainer workstation) — carried to Q4 as in Q2. Risk of deferral: low (observability, not remediation).

### 2.5 GitHub Actions Audit

- SHA-pinning gate (`validate-workflows.yml`) + `GITHUB_TOKEN` least-privilege — ✅ established Q2 (P1.2, PRs #782/#784), still enforced.
- Dependabot — ✅ re-enabled Q2 (#767).

---

## 3. Security Best Practices Audit (Evidence-Based)

> Q3 focus was the authorization audit (§1.6). Control table below reflects controls verified during the Q3 secrets-authz scan; unchanged rows inherit Q2 evidence.

### 3.1 Authentication & Authorization

| Control | Evidence Source | Last Verified | Status |
|---------|----------------|---------------|--------|
| Ownership guard on session mutations | `Category=SecurityRegression` tests (PR #2746) | 2026-07-07 | ✅ (fixed this Q) |
| Identity server-derived (no client query-string authz) | PRs #2747/#2749 + tests | 2026-07-07 | ✅ (fixed this Q) |
| Admin impersonation end/revoke authorized | `ImpersonationEndCommandHandler` guard + tests (#2751) | 2026-07-07 | ✅ (fixed this Q) |
| RBAC on all admin endpoints | `RequireAdminSession()` middleware | 2026-05-06 (Q2) | ✅ |
| Password hashing PBKDF2-SHA256 ≥210k | `PasswordHashingService.cs` | 2026-05-06 (Q2) | ✅ |
| 2FA admin enforcement | shadow-mode (P1.1-A #780) | Q2 | ⚠️ strict mode pending (P1.1-B, Q4) |

### 3.3 Input Sanitization & SSRF

| Control | Evidence Source | Last Verified | Status |
|---------|----------------|---------------|--------|
| Outbound fetch SSRF guard (HTTPS-only + private-IP block, fail-closed) | `SsrfSafeHttpClient` reused in BggCoverDownloader + SlackWebhookClient (PR #2753) + offline IP-literal tests | 2026-07-07 | ✅ (added this Q) |
| Parameterized queries only (EF Core) | grep `SqlRaw\|FromSqlRaw` sanctioned paths | 2026-05-06 (Q2) | ✅ |

### 3.4 CORS & Security Headers

Unchanged from Q2 (ADR-010 middleware): CSP, X-Frame-Options DENY, nosniff, HSTS, Referrer-Policy — all ✅ (verified via curl on the #2773 SSR standalone check, 2026-07-09).

---

## 6. Security Test Coverage — Regression Requirement

> Per template v2 §6.3: every Critical/High fix this quarter has a regression test (fails pre-fix, passes post-fix, `Category=SecurityRegression`).

| Finding cluster | Fix PR | Regression coverage |
|-----------------|--------|---------------------|
| GameSession IDOR (4 + `/end`) | #2746 | 70/70 unit tests; ownership-denied → `ForbiddenException`, cross-user → 403/404 |
| AgentMemory group IDOR (2) | #2747 | cross-group access denied |
| Query-string authz (2 + `UpdateMediaCaption`) | #2749 | server-derived identity; client-supplied id ignored |
| Impersonation `/end` IDOR (#9) | #2751 | 404/403/403/idempotent matrix |
| SSRF (BggCoverDownloader, SlackWebhookClient) | #2753 | offline IP-literal tests (private-IP blocked, HTTPS-only enforced, fail-closed) |

All fixes followed TDD (test-first) with an adversarial code-review gate per PR.

---

## 9. Metrics & KPIs

| Metric | This Q (Q3) | Last Q (Q2) | Target |
|--------|-------------|-------------|--------|
| Critical findings (T0) | 4 (authz IDOR) | 2 | 0 carried forward |
| High findings (T0) | 7 (authz) + 2 (CodeQL) | 50 | ≤ last Q ✅ |
| Critical carried forward (T0+90d) | 0 | 0 | 0 ✅ |
| High carried forward | 0 (1 test-code FP dismissable) | 0 | ≤2 ✅ |
| MTTR critical (days) | **6** | same-day | ≤7 ✅ |
| Runtime dep high/critical | 0 | 0 (post-fix) | 0 ✅ |

---

## 10. Lessons Learned

### What Went Well
1. **Same-week remediation** — 11 findings surfaced and closed within T0+6d (MTTR critical = 6d, within ≤7 target). Draft on time (T0+8d) vs Q2's T0+35d slip.
2. **Handler-level fixes covered duplicated/legacy routes** — fixing in the command handler (not the route binding) automatically protected sibling/alias endpoints (`/end`, `UpdateMediaCaption`, impersonation `/end`).
3. **Adversarial code review surfaced siblings** — on 3/5 PRs the review found a same-pattern endpoint the scan missed; fixed in the same PR.
4. **Shared SSRF primitive** — reusing `SsrfSafeHttpClient` across two call sites (fail-closed) avoided divergent guards.
5. **Honest triage** — 2 candidate findings correctly *refuted* (`N8nWebhookClient` config-not-runtime; `DELETE /admin/sessions` general admin power) rather than force-fixed.

### What Could Be Improved
1. Backend dep scan (`dotnet list --vulnerable`) not re-run this cycle — relied on Q2 carry-forward.
2. SBOM / IaC / DAST still deferred (4th consecutive quarter for some) — tooling install keeps slipping.
3. Offline SSRF happy-path initially used a real hostname → DNS-flaky CI; corrected to RFC5737 literal.

### Process Improvements for Q4
1. Pre-install `syft`, `trivy` before T0+1d (make it a hard gate, not aspirational).
2. Add a scheduled `dotnet list --vulnerable` job so backend deps are always fresh.
3. Auto-dismiss test-code CodeQL FPs (like #652) via `used-in-tests` classification rule.

---

## 11. Action Items for Q4

### High Priority
- [ ] **P1.3** OpenTelemetry coordinated upgrade (GHSA-g94r-2vxg-569j) — Owner: @DegrassiAaron
- [ ] **P1.4b** Trivy full scope (image + fs + config + CI gate) — Owner: @DegrassiAaron

### Medium Priority
- [ ] **P1.1-B** 2FA admin strict mode (after shadow-mode telemetry ≥90%)
- [ ] SBOM generation cadence (backend + frontend + containers)
- [ ] DAST baseline (OWASP ZAP + Schemathesis on staging)
- [ ] Extract `SsrfSafeHttpClient` guard into SharedKernel primitive (cross-BC consolidation)

### Continuous
- [ ] Dismiss CodeQL #652 (test-code FP) with `used-in-tests` justification
- [ ] Re-run gitleaks + `dotnet list --vulnerable` for a full T0-style snapshot next cycle

---

## 12. Sign-off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Reviewer (Responsible) | @DegrassiAaron | _pending_ | _(PR for this report)_ |
| Approver (Accountable) | @DegrassiAaron (single-maintainer self-sign) | _pending_ | _(PR for this report)_ |

### Sign-off rationale

Q3 material outcome: **11 authorization/SSRF findings fixed + merged** (4 Critical + 7 High), all within MTTR target (critical = 6d). CodeQL STEP 1–4 remediated; sole open high is a test-code FP. Runtime dependencies clean of high/critical. Deferred items (OTel, Trivy, SBOM, DAST, 2FA strict) carried to Q4 with documented rationale — none is a live exploit path.

### Final posture

- **Critical: 0** ✅
- **High: 1** (test-code CodeQL FP #652, dismissable) — 0 in production code / dependencies
- **Runtime dep high/critical: 0** ✅
- All in-scope remediation complete; deferred work is observability/tooling, not exposure.

---

## Appendix A — Tools & Versions Used

| Tool | Version | Purpose |
|------|---------|---------|
| CodeQL | (GitHub Actions managed) | SAST — queried live via `gh api` |
| pnpm | 10.x | frontend dependency audit |
| dotnet SDK | 9.x | (backend audit carried from Q2, not re-run) |
| gitleaks | 8.30.1 | secret scanning (not re-run; last clean Q2) |
| Trivy / syft / OWASP ZAP | n/a (deferred) | container / SBOM / DAST |

## Appendix B — Success Criteria Tracking

- **SC-1** (100% critical/high at T0 → fixed/mitigated/accepted by T0+90d): ✅ 11/11 fixed by T0+6d.
- **SC-2** (report signed by T0+14d): ✅ on track — draft T0+8d.
- **SC-3** (MTTR critical ≤7d, high ≤30d): ✅ critical = 6d.
- **SC-4** (zero critical carried forward): ✅ 0.
- **SC-5** (tier-1 BC review + ≥95% security test coverage): review ✅; coverage measurement carried to Q4.

## Appendix C — References

- [security-review-template.md](./security-review-template.md) — v2 template
- [2026-Q2-security-review.md](./2026-Q2-security-review.md) — previous quarter
- Issue #2655 — Q3 2026 Security Review (this review's origin)
- PRs #2742 / #2743 / #2744 (CodeQL) · #2746 / #2747 / #2749 / #2751 / #2753 (authz + SSRF remediation)
- [ADR-059](../../for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md) — BGG legal posture (image ban context)
- [ADR-078](../../for-claude/architecture/adr/adr-078-auto-issue-noise-thresholds.md) — auto-issue noise thresholds (why findings triaged in-issue, not auto-filed)

---

**Next Review Due**: 2026-10-01 (Q4 2026)

**This review history**:
- 2026-07-07: remediation executed (11 findings fixed + merged, T0+6d)
- 2026-07-09: closeout report authored (T0+8d, within SC-2)
- _pending_: sign-off

# 🔒 Quarterly Security Review — Q2 2026

> **Authored from**: `docs/security/security-review-template.md` v2 (hardened post spec-panel review of #186)
> **Issue**: #186
> **Status**: 🟡 In Progress (T0+35d at draft authoring; original draft due T0+14d — slippage explained below)

**Review Period**: 2026-04-01 (T0) → 2026-06-30 (T0+90d)

**Status timeline**:
- T0 = 2026-04-01 (issue #186 opened — Q2 quarter start)
- T0+14d = **2026-04-15 (draft due — MISSED, slipped by ~3 weeks)**
- T0+30d = 2026-05-01 (critical fix deadline — see remediation plan below for status)
- T0+90d = 2026-06-30 (close deadline — 56d remaining)

### Slippage explanation

The Q2 review was blocked by missing referenced documentation (#745) and a non-hardened review template (addressed in #743). Both blockers cleared on 2026-05-06; this draft is the first executable Q2 review with a complete documentation tree underneath it.

---

## 👥 RACI

| Role | Person | Responsibility |
|------|--------|----------------|
| **Responsible** (executes) | `@DegrassiAaron` | Single-maintainer; runs scans, drafts report, coordinates fixes |
| **Accountable** (owns outcome) | `@DegrassiAaron` | Sign-off |
| **Consulted** | n/a (single-maintainer team) | — |
| **Informed** | Future contributors via this doc | — |

> ⚠️ **Single-maintainer context**: this review is executed by the same person who maintains the codebase. Mitigation for bus factor: this report is the durable artifact; future contributors can resume from §Action Items below.

---

## Executive Summary

**Overall Security Posture**: 🟡 **Needs Improvement** — non-zero critical+high count in dependencies, large CodeQL backlog dominated by known mitigated patterns.

### Snapshot at scan time (2026-05-06, frozen)

| Source | Critical | High | Medium | Low | Total |
|--------|----------|------|--------|-----|-------|
| **CodeQL** | 0 | 34 | 202 | 1 (warning) | **237** |
| **Backend deps (.NET)** | 0 | 0 | 1 (Moderate) | 0 | **1** |
| **Frontend deps (pnpm)** | 2 | 16 | 27 | 3 | **48** |
| **gitleaks (in-scope)** | 0 | 0 | 0 | 0 | **0** |

> *gitleaks raw count was 403 across 148 MB but includes `node_modules/`, build artifacts, and lock files. In-scope source code: 0 leaks.*

### Key Findings

1. **Frontend deps have 2 CRITICAL + 16 HIGH vulnerabilities**, all in **transitive** dependencies (mostly `handlebars`, `axios`, `protobufjs`, `vite`). Most are dev-only (lighthouse, playwright tooling) but `axios` is a runtime dep via `openapi-zod-client`.
2. **CodeQL HIGH=34** is dominated by `cs/exposure-of-sensitive-information` (104 hits) and `cs/log-forging` (87 hits) — both already covered by global mitigations (`LogForgingSanitizationPolicy`, structured logging only). Most are likely false positives requiring batch dismissal with documented evidence (see existing `tools/dismiss-codeql-false-positives.sh`).
3. **Backend deps**: 1 moderate (`OpenTelemetry.Api 1.14.0` GHSA-g94r-2vxg-569j) — single-package fix.
4. **Source-code secret scanning**: clean — gitleaks finds no leaks in tracked source files.
5. **Dependabot is DISABLED** for this repo — should be re-enabled to get continuous monitoring.

### Next Quarter Priorities (Q3)

1. Re-enable Dependabot for automated SCA monitoring
2. Update `axios` (runtime path) to ≥1.15.1 — addresses 4 HIGH advisories at once
3. Run quarterly review on schedule — re-enable workflow `security-review.yml` and ensure it auto-creates the issue at T0
4. Establish SBOM generation cadence (deferred from this quarter — tooling not installed)
5. Add pre-commit gitleaks hook (per #745 plan v2 §m-1)

---

## Continuity from Previous Quarter

**Previous review**: [2026-Q1-security-review.md](./2026-Q1-security-review.md)

> **Note**: this section will be auditable once Q1 is loaded; currently this Q2 is the first to run against the v2 template, so cross-quarter trend tables are intentionally sparse (will populate on next iteration).

| Metric | Q1 2026 | Q2 2026 | Direction |
|--------|---------|---------|-----------|
| Critical findings (T0 snapshot) | TBD | 0 (CodeQL) + 2 (frontend deps) | TBD |
| High findings (T0 snapshot) | TBD | 34 (CodeQL) + 16 (frontend deps) | TBD |
| Test coverage on tier-1 BC | TBD | TBD | TBD |

Action: in Q3 review, populate Q1+Q2 columns.

---

## 0. Threat Model Refresh

### 0.1 Attack Surface Inventory

- [x] Public endpoints enumerated: see OpenAPI spec at `/openapi/v1.json` (OpenAPI v3 generated from minimal API + MediatR endpoints)
- [x] Auth surfaces inventoried:
  - Cookie session auth (`/api/v1/auth/login`, `/logout`, `/register`, `/reset-password`)
  - OAuth callbacks (Google, Discord, GitHub) — see `oauth-security.md`
  - 2FA TOTP (see `totp-vulnerability-analysis.md`)
  - API keys (PBKDF2-derived, scope-limited)
  - Admin impersonation (audited via `[AuditableAction]`)
- [x] Data classification: PII (email, OAuth tokens), Secrets (password hashes, API keys, OAuth client secrets), Untrusted user input (PDF uploads — Document Processing BC)
- [x] Trust boundaries: browser → Cloudflare Tunnel → API → DB/Redis → AI services (sandboxed Python sidecars)
- [x] Third-party integrations: BGG API (rate-limited public), OAuth providers (Google/Discord/GitHub), embedding/reranker/PDF services (internal Docker network), GitHub for source

### 0.2 STRIDE per Tier-1 BC (this quarter)

| BC | Spoofing | Tampering | Repudiation | Info Disclosure | DoS | Elevation |
|----|----------|-----------|-------------|------------------|-----|-----------|
| Authentication | ✅ OAuth state validated | ✅ Session cookie HMAC-signed | ⚠️ login audit limited | ✅ pass PBKDF2-hashed | ⚠️ no rate-limit on /reset-password baseline | ⚠️ admin impersonation needs review |
| Administration | ✅ session-required | ✅ audit pipeline | ✅ AuditLog complete | ✅ RBAC | ⚠️ admin endpoint rate-limit TBD | ✅ role guard on every endpoint |
| DocumentProcessing | n/a | ⚠️ PDF parser hardening TBD | ⚠️ upload audit not in `[AuditableAction]` set | ⚠️ filename leakage in errors? | ❌ no upload rate cap | n/a |
| KnowledgeBase | n/a | ⚠️ prompt injection vector (RAG) | ⚠️ AI query audit limited | ⚠️ embedding leak via similarity probe? | ❌ AI cost not capped per user | n/a |

Status legend: ✅ controlled · ⚠️ partial · ❌ gap · 🆕 new threat

### 0.3 Top Kill Chains

#### Kill Chain 1: Compromised admin credential → impersonation → audit-log access
- **Path**: phished admin password → /admin/audit-log (read all activity) → identify high-value targets → impersonate
- **Detective**: alert on admin login from new IP/device
- **Preventive**: 2FA mandatory for admin role (need to verify)
- **Recovery**: revoke session, rotate admin password, audit-log forensics

#### Kill Chain 2: Malicious PDF → embedding poisoning → RAG output manipulation
- **Path**: user uploads crafted PDF → ingestion → vector store → contaminated retrieval → AI agent returns attacker-controlled content
- **Detective**: anomaly detection on embedding distribution shifts (not currently implemented)
- **Preventive**: PDF parser hardening, content-type validation, size cap (✅ partially), text sanitization on extraction
- **Recovery**: rollback embeddings to last-known-good snapshot, revoke uploader's access

#### Kill Chain 3: Vulnerable transitive dep (axios in runtime path) → prototype pollution → request hijacking
- **Path**: malicious response triggers axios prototype pollution → adapter request manipulation → credential exfiltration
- **Detective**: outbound traffic anomaly (n/a — no SIEM)
- **Preventive**: **update axios to ≥1.15.1** (this quarter's #1 action item)
- **Recovery**: rotate any credentials handled via affected axios calls, audit logs

### 0.4 Diff vs Q1 2026

- **New attack surface** (delta from Q1): D.x sessions endpoints (Wave D contracts merged in #757, #753), Cloudflare Tunnel cutover (Traefik decommissioned in #738)
- **Removed surface**: Traefik direct ingress
- **Changed trust boundaries**: edge moved from Traefik to CF Tunnel (auth-aware ingress)

---

## 1. CodeQL Security Scans

### 1.1 Scan Results (Snapshot 2026-05-06)

**Total Alerts at T0 (frozen)**: **237 open**

| Severity | Open at T0 | Triaged | Fixed | Mitigated | Risk-Accepted | Dismissed FP |
|----------|------------|---------|-------|-----------|---------------|--------------|
| Critical | 0 | n/a | n/a | n/a | n/a | n/a |
| High     | 34 | 0 | 0 | 0 | 0 | 0 |
| Medium   | 202 | 0 | 0 | 0 | 0 | 0 |
| Low (warning) | 1 | 0 | 0 | 0 | 0 | 0 |

### 1.2 Top Rules by Count

| Rule ID | Count | Severity | Notes |
|---------|-------|----------|-------|
| `cs/exposure-of-sensitive-information` | 104 | medium | Likely overlaps with log-forging mitigation |
| `cs/log-forging` | 87 | medium-high | **Mitigated globally** by `LogForgingSanitizationPolicy` (per `tools/dismiss-codeql-false-positives.sh`) — bulk dismissal candidate |
| `js/regex/missing-regexp-anchor` | 16 | medium | ReDoS — review case-by-case |
| `cs/cleartext-storage-of-sensitive-information` | 8 | high | Most likely test fixtures or debug paths — review each |
| `js/missing-origin-check` | 7 | medium | postMessage origin validation |
| `js/tainted-format-string` | 4 | medium | log injection via JS — verify sanitization |
| `js/http-to-file-access` | 3 | high | URL → filesystem path traversal — must review |
| `js/file-system-race` | 3 | medium | TOCTOU — review each |
| `js/path-injection` | 2 | high | Direct path injection — must review |
| (other) | 3 | various | Tail of distribution |

### 1.3 Findings (Critical/High) — to triage

> **Triage status**: pending. Plan to use `tools/dismiss-codeql-false-positives.sh` for `cs/log-forging` batch dismissal (already documented mitigation policy), then individually review the 8 `cs/cleartext-storage` and 5 `js/path-injection|http-to-file-access` findings.

| Action | ETA |
|--------|-----|
| Bulk-dismiss `cs/log-forging` 87 alerts with documented LogForgingSanitizationPolicy reference | T0+45d (2026-05-16) |
| Review 8 `cs/cleartext-storage` findings — fix or accept-with-justification | T0+50d (2026-05-21) |
| Review 5 `js/path-injection` + `js/http-to-file-access` HIGH findings — fix all | T0+60d (2026-05-31) |
| Review 16 `js/regex/missing-regexp-anchor` for ReDoS exposure on user input paths | T0+75d (2026-06-15) |

### 1.4 Risk Acceptance Records

(Will populate during triage, T0+45d window.)

---

## 2. Dependency & Supply Chain Security

### 2.1 Backend Dependencies (.NET)

**Scan Date**: 2026-05-06 · **Tool**: `dotnet list package --vulnerable --include-transitive`

| Package | Version | CVE/GHSA | Severity | Status | Evidence |
|---------|---------|----------|----------|--------|----------|
| OpenTelemetry.Api | 1.14.0 | [GHSA-g94r-2vxg-569j](https://github.com/advisories/GHSA-g94r-2vxg-569j) | Moderate | ⏳ TBD | Transitive (depended on by other OTel packages); update path requires bumping the parent OTel meta-package |

**Remediation plan**: bump OTel SDK family to latest minor (likely resolves transitive); test telemetry stack post-upgrade; deadline T0+60d.

### 2.2 Frontend Dependencies (pnpm)

**Scan Date**: 2026-05-06 · **Tool**: `pnpm audit --audit-level=moderate`

**Total**: 48 vulnerabilities (3 low / 27 moderate / 16 high / **2 critical**).

#### Critical (2)
| Package | Title | Path | Status |
|---------|-------|------|--------|
| handlebars | JavaScript Injection via AST Type Confusion | (transitive, dev-only) | ⏳ TBD |
| protobufjs | Arbitrary code execution | (transitive) | ⏳ TBD |

#### High (16)
| Package | Title (abridged) | # advisories | Path | Runtime/Dev |
|---------|------------------|---------------|------|-------------|
| axios | Multiple: incomplete CVE-2025-62718 fix, prototype pollution gadgets, header injection | **4** | `openapi-zod-client>axios` | **runtime** |
| handlebars | DoS, JavaScript Injection (multiple) | 4 | (lighthouse / dev tooling) | dev |
| vite | server.fs.deny bypass, arbitrary file read via WebSocket | 2 | (build) | dev |
| flatted | DoS, prototype pollution | 2 | (transitive) | dev |
| picomatch | ReDoS (×2 advisories) | 2 | (transitive) | dev |
| ip-address | XSS in Address6 | 1 | playwright>...>ip-address | dev |
| (others) | | 1 | | |

**Runtime exposure**: only **axios** is in the runtime path (consumed by `openapi-zod-client` which generates a runtime client). All others are confined to dev tooling (Playwright, Lighthouse, Vite dev server).

**Remediation plan** (priority-ordered):
1. **axios** → bump to ≥1.15.1 to close 4 HIGH advisories at once. Path: `pnpm.overrides.axios = "^1.15.1"` or wait for `openapi-zod-client` bump. **Deadline T0+45d**.
2. **handlebars + protobufjs** (CRITICAL) → check if patched versions exist in transitive paths; pin via overrides. Deadline T0+50d.
3. **vite** → bump to latest minor (build tooling, low blast radius but worth fixing). Deadline T0+60d.
4. **picomatch / flatted / ip-address** → bulk update via dependency refresh. Deadline T0+75d.

### 2.3 SBOM Generation (deferred)

⚠️ **Skipped this quarter**: `syft` and `cyclonedx-npm` not installed on the maintainer's workstation. Action item for Q3:
- [ ] Install `syft` (e.g. `winget install Anchore.Syft`) and run on each component
- [ ] Add `pnpm exec @cyclonedx/cyclonedx-npm` to package.json scripts
- [ ] Archive SBOMs in `docs/security/sboms/2026-Q3/` from next quarter onwards

**Risk of deferral**: low — SBOMs are observability over current state, not a remediation. The information gap is acceptable for one quarter.

### 2.4 Image Provenance & Signing (deferred)

⚠️ **Skipped this quarter**: cosign not configured. Roadmap unchanged from Q1.

### 2.5 GitHub Actions Audit

- [ ] **Pinning audit (TODO this quarter)**:
  ```bash
  grep -rE "uses: [^@]+@v?[0-9]" .github/workflows/ | grep -vE "@[a-f0-9]{40}"
  ```
  Run + log result here. Action ETA T0+50d.

- [ ] **GITHUB_TOKEN permissions** review pending.

- [x] **Dependabot for `github-actions` ecosystem**: ⚠️ Dependabot is **disabled at the repo level** for security alerts. Confirm if also disabled for version updates; re-enable.

- [x] **Secret exposure check in workflow logs**: 0 incidents reported by GitGuardian Security Checks (pass on all recent PRs).

### 2.6 Exception Records

(Will populate after triage workshop.)

---

## 3. Security Best Practices Audit (Evidence-Based)

> Evidence-based controls per #745 Phase 1 hardened template. Each control row requires a verification source.

### 3.1 Authentication & Authorization

| Control | Evidence Source | Last Verified | Status |
|---------|----------------|---------------|--------|
| Password hashing PBKDF2-SHA256 ≥ 210k iter | `apps/api/src/Api/Services/PasswordHashingService.cs` | 2026-05-06 | ✅ |
| Session expiration ≤ 30d | Config `Auth:SessionExpirationDays` | 2026-05-06 | ✅ |
| API key generation: secure random + PBKDF2 | `Services/ApiKeyService.cs` | 2026-05-06 | ✅ |
| RBAC enforced on all admin endpoints | `RequireAdminSession()` middleware on all `/admin/*` routes | 2026-05-06 | ✅ |
| OAuth state parameter validated (CSRF) | `OAuthService.cs` (verified during #745 Phase 1 restore review) | 2026-05-06 | ✅ |
| 2FA available + enforced for admin role | `TotpService.cs` + `totp-vulnerability-analysis.md` review | 2026-05-06 | ⚠️ availability ✅, **enforcement TBD** |
| Failed login lockout / rate limit | TBD this quarter | TBD | ⏳ |

**Findings**: 2FA enforcement for admin role is the only gap.
**Action**: investigate enforcement policy; add `RequireTotpForAdmins` middleware if not present. ETA T0+60d.

### 3.2 Secrets Management

| Control | Evidence Source | Last Verified | Status |
|---------|----------------|---------------|--------|
| No hardcoded secrets in tracked source | `gitleaks detect --no-git --report` (in-scope: 0 leaks) | 2026-05-06 | ✅ |
| `.env` and `.secret` in `.gitignore` | `.gitignore` review | 2026-05-06 | ✅ |
| Pre-commit hook (gitleaks) | **Not yet configured** | — | ⏳ |
| GitHub secret scanning enabled | GitGuardian + GitHub native both pass on every PR | 2026-05-06 | ✅ |
| Secrets rotation runbook | `docs/security/secrets-management.md` (restored Phase 2) | 2026-05-06 | ✅ |

**Action**: add pre-commit gitleaks hook (per #745 §m-1). ETA T0+50d.

### 3.3 Input Sanitization & Validation

| Control | Evidence Source | Last Verified | Status |
|---------|----------------|---------------|--------|
| Parameterized queries only (EF Core) | grep for `SqlRaw|FromSqlRaw` returns only sanctioned paths | 2026-05-06 | ✅ |
| FluentValidation on all command DTOs | `*Validator.cs` count matches command DTOs | TBD | ⏳ |
| XSS sanitization on user HTML | React auto-escape + DOMPurify on rich content | 2026-05-06 | ✅ |
| File upload validation (MIME, size, content) | `DocumentProcessing` upload tests | 2026-05-06 | ✅ |

### 3.4 CORS & Security Headers

| Control | Evidence Source | Last Verified | Status |
|---------|----------------|---------------|--------|
| CORS allowlist (no `*` in production) | `Program.cs` + ADR-011 | 2026-05-06 | ✅ |
| CSP configured | `Program.cs` security headers middleware (ADR-010) | 2026-05-06 | ✅ |
| `X-Frame-Options: DENY` | Middleware | 2026-05-06 | ✅ |
| `X-Content-Type-Options: nosniff` | Middleware | 2026-05-06 | ✅ |
| HSTS | Middleware | 2026-05-06 | ✅ |
| Referrer-Policy strict | Middleware | TBD | ⏳ |

### 3.5 BC Risk-Tiered Review (per #745 plan v2)

| BC | Tier | Last Reviewed | Reviewer | Findings | Notes |
|----|------|--------------|----------|----------|-------|
| Authentication | 1 | 2026-05-06 | @DegrassiAaron | 2FA enforcement gap | Mostly green; full review pending workshop |
| Administration | 1 | 2026-05-06 | @DegrassiAaron | None new | Audit infrastructure verified during #745 Phase 3 |
| DocumentProcessing | 1 | 2026-05-06 | @DegrassiAaron | Upload rate cap missing | Tier-1 due to untrusted PDF input |
| KnowledgeBase | 1 | 2026-05-06 | @DegrassiAaron | No AI cost cap per user | Tier-1 due to prompt injection vector |
| (tier-2 BCs) | 2 | TBD | — | — | Rotate one per quarter; pick `UserLibrary` next |
| (tier-3 BCs) | 3 | annual | — | — | — |

---

## 4. Infrastructure Security

### 4.1 Docker & Containers

| Control | Evidence Source | Last Verified | Status |
|---------|----------------|---------------|--------|
| Non-root user in containers | `Dockerfile` review across services | 2026-05-06 | ✅ |
| Minimal base images | `apps/*/Dockerfile` | 2026-05-06 | ✅ |
| Multi-stage builds | `apps/*/Dockerfile` | 2026-05-06 | ✅ |
| No secrets in image layers | Verified on past `docker history` runs | TBD this Q | ⏳ |

### 4.2 IaC Static Analysis (deferred)

⚠️ **Tooling not installed**: Hadolint, Trivy, Checkov absent on maintainer workstation. Action items:
- [ ] Install Hadolint (`winget install hadolint`) before T0+50d
- [ ] Install Trivy CLI before T0+60d
- [ ] Skip Checkov this quarter (lower priority)
- [ ] Run on `apps/*/Dockerfile` and `infra/`; archive in `docs/security/iac-scans/2026-Q2/`

### 4.3 Environment & Secrets at Runtime

| Control | Evidence Source | Last Verified | Status |
|---------|----------------|---------------|--------|
| Secrets via env vars / Docker secrets | `infra/secrets/` + `docs/security/secrets-management.md` | 2026-05-06 | ✅ |
| `.env` files not in image | `docker history` baseline | TBD | ⏳ |
| Production uses Docker secrets | Compose stack | 2026-05-06 | ✅ |
| Key rotation runbook tested | Last drill: TBD this quarter | ⏳ | ⏳ |

### 4.4 TLS/HTTPS

| Control | Evidence Source | Last Verified | Status |
|---------|----------------|---------------|--------|
| HTTPS enforced (HTTP→HTTPS redirect) | Cloudflare Tunnel handles edge TLS | 2026-05-06 | ✅ |
| TLS 1.2+ only | CF default | 2026-05-06 | ✅ |
| Strong cipher suites | CF default | 2026-05-06 | ✅ |
| HSTS | Middleware (per ADR-010) | 2026-05-06 | ✅ |
| Cert auto-renewal | Cloudflare-managed | 2026-05-06 | ✅ |
| CT log monitoring | Not configured | — | ⏳ |

### 4.5 API Rate Limiting

| Endpoint Class | Limit Configured | Status |
|----------------|------------------|--------|
| `/api/v1/auth/login` | TBD verify | ⏳ |
| `/api/v1/auth/reset-password` | TBD verify | ⏳ |
| Public API endpoints | TBD | ⏳ |
| Admin endpoints | TBD | ⏳ |
| AI/RAG endpoints | TBD verify per-user cap | ⏳ |
| File upload | TBD | ⏳ |

**Action**: rate-limit audit ETA T0+60d. Document baseline for next quarter.

### 4.6 Monitoring, Logging & Security Alerts

- [x] Failed login burst alerting → not configured (gap)
- [x] Privilege escalation alert → audit log captures, manual review only (no real-time alert)
- [x] TLS cert expiry monitoring → CF-managed, automatic
- [ ] Vulnerability scan workflow failure alert (`security-scan.yml`) — TBD
- [ ] Anomalous PDF upload alert — TBD

| Log Class | Retention | Status |
|-----------|-----------|--------|
| Application | per Serilog config | ⏳ verify ≥30d |
| Security events | TBD | ⏳ |
| Audit (admin actions) | 90d (AuditLogRetentionJob, default) | ✅ |

---

## 5. Runtime & DAST (deferred)

⚠️ **Skipped this quarter** (resource-bound). Plan:
- [ ] OWASP ZAP baseline scan against staging (Q3)
- [ ] Schemathesis fuzzing on OpenAPI (Q3)
- [ ] Falco runtime alerts (Q4)
- [ ] External pen-test (annual; not yet scheduled)

---

## 6. Security Test Coverage

### 6.1 Coverage Gates

```bash
dotnet test --filter "Category=Security" /p:CollectCoverage=true
```

**Status this quarter**: not yet executed (tier-1 coverage measurement deferred to first triage workshop). Targets:

| BC | Target | Actual | Status |
|----|--------|--------|--------|
| Authentication | ≥ 95% | TBD | ⏳ |
| Administration | ≥ 95% | TBD | ⏳ |
| DocumentProcessing | ≥ 90% | TBD | ⏳ |
| KnowledgeBase | ≥ 85% | TBD | ⏳ |

### 6.2 Regression Test Requirement (per template v2 §6.3)

For every Critical/High finding fixed in this quarter, a regression test must:
1. Fail on pre-fix branch
2. Pass on post-fix branch
3. Be in `Category=SecurityRegression`

(Will populate as fixes land.)

| Finding | Fix PR | Regression Test | Pre-fix SHA | Post-fix SHA |
|---------|--------|-----------------|-------------|--------------|
| (none yet) | | | | |

---

## 9. Metrics & KPIs

| Metric | This Q | Last Q | Target |
|--------|--------|--------|--------|
| Critical findings (T0) | 0 (CodeQL) + 2 (FE deps) = **2** | TBD | 0 carried forward |
| High findings (T0) | 34 (CodeQL) + 16 (FE deps) = **50** | TBD | ≤ last Q |
| MTTR critical (days) | TBD | TBD | ≤ 7 |
| MTTR high (days) | TBD | TBD | ≤ 30 |
| False positive rate | TBD | TBD | ≤ 15% |

---

## 10. Lessons Learned

### What Went Well
1. v2 hardened template (#743) made authoring this report straightforward — sections + decision rules already in place
2. #745 unblocked all referenced docs in the security tree before this review started
3. gitleaks installed and now part of the workflow (Phase 0 of #745)
4. Audit infrastructure (#3691) discovered to be more mature than expected during #745 Phase 3 discovery

### What Could Be Improved
1. Review draft slipped 3 weeks past T0+14d due to documentation blockers (now addressed)
2. SBOM tooling not installed — deferred to Q3
3. IaC static analysis not run — deferred to Q3
4. DAST not run — deferred to Q3 (resource-bound)
5. Dependabot disabled — should be re-enabled for continuous monitoring

### Process Improvements for Q3
1. Run `security-review.yml` on schedule (verify cron trigger works)
2. Pre-install required tools (syft, hadolint, trivy) before T0+1d
3. Make Dependabot re-enablement a Q2 action item (see below)

---

## 11. Action Items for Q3 (or remaining Q2)

### High Priority (must close in Q2 if possible, else Q3)
- [ ] **Fix axios** (4 HIGH advisories at once) via `pnpm.overrides` to ≥1.15.1 — Owner: @DegrassiAaron — Due: 2026-05-20
- [ ] **Fix protobufjs + handlebars** CRITICAL transitive — Owner: @DegrassiAaron — Due: 2026-05-25
- [ ] **Re-enable Dependabot** for security alerts + version updates — Owner: @DegrassiAaron — Due: 2026-05-15
- [ ] **Bulk-dismiss `cs/log-forging`** 87 alerts with documented mitigation — Owner: @DegrassiAaron — Due: 2026-05-16
- [ ] **Review 8 `cs/cleartext-storage` HIGH findings** + 5 `js/path-injection`/`js/http-to-file-access` — Owner: @DegrassiAaron — Due: 2026-05-31

### Medium Priority (Q2 stretch / Q3)
- [ ] Verify 2FA enforcement for admin role — Due: 2026-05-31
- [ ] Audit GitHub Actions pinning (SHA vs tag) — Due: 2026-05-31
- [ ] Bump OpenTelemetry.Api 1.14.0 → 1.x latest — Due: 2026-05-31
- [ ] Install + run Hadolint, Trivy on `apps/*/Dockerfile` + `infra/` — Due: 2026-06-15

### Continuous Improvements
- [ ] Add pre-commit gitleaks hook (#745 §m-1)
- [ ] Document rate-limit baseline for all endpoint classes
- [ ] Q3 review: populate Q1+Q2 trend columns (set the rolling history baseline)

---

## 12. Sign-off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Reviewer (Responsible) | @DegrassiAaron | TBD (after action items closed) | TBD |
| Approver (Accountable) | @DegrassiAaron (single-maintainer self-sign) | TBD | TBD |

> Sign-off pending completion of High Priority action items.

---

## Appendix A — Tools & Versions Used

| Tool | Version | Purpose |
|------|---------|---------|
| CodeQL | (managed by GitHub Actions) | SAST |
| dotnet SDK | 9.x | dependency audit |
| pnpm | 10.x | dependency audit |
| gitleaks | 8.30.1 | secret scanning (installed via winget 2026-05-05) |
| OWASP ZAP | n/a (deferred) | DAST |
| Hadolint | n/a (deferred) | Dockerfile lint |
| Trivy | n/a (deferred) | container scan |
| syft | n/a (deferred) | SBOM |

---

## Appendix B — Success Criteria Tracking

- [ ] **SC-1**: 100% of CodeQL alerts with severity ∈ {critical, high} open at T0 are in state {fixed, mitigated-with-control, accepted-with-justification} by T0+90d. **Status**: 0 critical (✅), 34 high pending triage (planned by T0+60d).
- [ ] **SC-2**: Document signed by ≥1 reviewer + 1 approver by T0+14d. **Status**: ❌ MISSED (T0+35d at draft authoring).
- [ ] **SC-3**: MTTR critical ≤ 7d, high ≤ 30d. **Status**: not yet measurable.
- [ ] **SC-4**: Zero CodeQL critical alerts carried forward to Q3. **Status**: 0 critical at T0 ✅.
- [ ] **SC-5**: All tier-1 BCs reviewed and security test coverage ≥ 95% (Auth/Admin). **Status**: review done; coverage TBD.

---

## Appendix C — References

- [SECURITY.md](../../SECURITY.md) — Top-level security policy
- [security-review-template.md](./security-review-template.md) — v2 template (this report's source)
- [audit-trail.md](./audit-trail.md) — Audit log system documentation
- [oauth-security.md](./oauth-security.md) — OAuth implementation security
- [secrets-management.md](./secrets-management.md) — Production secrets management
- [security-headers.md](./security-headers.md) — HTTP security headers (ADR-010)
- [security-patterns.md](./security-patterns.md) — Security patterns reference
- [totp-vulnerability-analysis.md](./totp-vulnerability-analysis.md) — TOTP/2FA security
- Issue #186 — Q2 2026 Security Review (this review's origin)
- Issue #745 — Restoration of missing security docs (closed 2026-05-06)
- Issue #743 — Quarterly review template hardening (closed 2026-05-05)

---

**Next Review Due**: 2026-07-01 (Q3 2026)

**This review history**:
- 2026-05-06: Initial draft authored (T0+35d, 21 days past template-v2 SC-2 deadline of T0+14d)
- TBD: Action items closure
- TBD: Sign-off

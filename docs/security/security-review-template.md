# 🔒 Quarterly Security Review — [QUARTER YEAR]

> **Template version**: v2 (hardened post spec-panel review of #186, 2026-05-05)
> **Spec-panel findings addressed**: C-1 (SMART criteria), C-2 (G/W/T scenarios),
> C-3 (threat model), C-4 (supply chain), C-5 (security test coverage gates)

**Review Period**: [Start Date T0] → [End Date T0+90d]
**Status timeline**:
- T0 = issue opened (snapshot of findings)
- T0+14d = draft report due
- T0+90d = remediation deadline for critical/high

---

## 👥 RACI

| Role | Person | Responsibility |
|------|--------|----------------|
| **Responsible** (executes) | [@security-champion] | Runs scans, drafts report, coordinates fixes |
| **Accountable** (owns outcome) | [@tech-lead / @cto] | Sign-off, escalation decisions, risk acceptance |
| **Consulted** (input required) | [@backend-lead, @frontend-lead, @devops-lead] | BC-specific reviews, threat model input |
| **Informed** (kept in loop) | All engineers, [@product-owner] | Workshop participation, PR reviews |

> ⚠️ **Hard requirement**: this section MUST be filled before T0+3d. Issue without RACI
> auto-escalates with label `escalation/no-owner`.

---

## Executive Summary

**Overall Security Posture**: ☐ Excellent · ☐ Good · ☐ Needs Improvement · ☐ Critical

**Snapshot at T0** (frozen, do not edit):
- Critical alerts open: [count]
- High alerts open: [count]
- Medium/Low: [count]

**Status at T0+90d**:
- Critical addressed: [count]/[T0 count] (target: 100%)
- High addressed: [count]/[T0 count] (target: 100%)
- False positives dismissed (with justification): [count]
- Risk-accepted with compensating controls: [count]

**Key Findings**: [3-5 bullets summarizing material outcomes]

**Next Quarter Priorities**: [Top 3-5 items]

---

## Continuity from Previous Quarter

> ⚠️ **Required**: must reference previous review and report progress on its action items.

**Previous review**: [link to `2026-QX-security-review.md`]

**Q[X-1] unresolved items carried forward**:
| Item | Origin | Days Open | New SLA |
|------|--------|-----------|---------|
| [...] | Q[X-1] | [n] | [date] |

**Trend (rolling 3 quarters)**:
| Metric | Q[X-2] | Q[X-1] | Q[X] | Direction |
|--------|--------|--------|------|-----------|
| Critical findings | | | | ↑/↓/→ |
| High findings | | | | ↑/↓/→ |
| MTTR critical (days) | | | | ↑/↓/→ |
| MTTR high (days) | | | | ↑/↓/→ |
| Test coverage on tier-1 BC | | | | ↑/↓/→ |

---

## 0. Threat Model Refresh

> 🆕 **C-3 (Nygard)**: Threat model is the foundation. We don't audit controls in isolation —
> we audit them against an explicit attacker model.

### 0.1 Attack Surface Inventory

- [ ] Public endpoints enumerated (link to OpenAPI snapshot at T0): [link]
- [ ] Auth surfaces inventoried: OAuth callbacks, password reset, API keys, sessions, 2FA, admin impersonation
- [ ] Data classification reviewed: PII fields, secrets, tokens, uploaded files (PDFs)
- [ ] Trust boundaries mapped: external (browser → Traefik), edge → API, API ↔ DB/Redis, API ↔ AI services (sandboxed?)
- [ ] Third-party integrations: BGG API, OAuth providers, embedding service, reranker service

### 0.2 STRIDE per Bounded Context (Tier 1)

| BC | Spoofing | Tampering | Repudiation | Info Disclosure | DoS | Elevation |
|----|----------|-----------|-------------|------------------|-----|-----------|
| Authentication | [status] | [status] | [status] | [status] | [status] | [status] |
| Administration | [status] | [status] | [status] | [status] | [status] | [status] |
| DocumentProcessing | [status] | [status] | [status] | [status] | [status] | [status] |
| KnowledgeBase | [status] | [status] | [status] | [status] | [status] | [status] |

Status legend: ✅ controlled · ⚠️ partial · ❌ gap · 🆕 new threat

### 0.3 Top Kill Chains (this quarter)

#### Kill Chain 1: [name]
- **Attack path**: [step 1] → [step 2] → [impact]
- **Detective controls**: [what alerts on this]
- **Preventive controls**: [what blocks this]
- **Recovery procedure**: [how to recover]
- **Validation test**: [link to test or scenario]

(Repeat for top 3-5 chains)

### 0.4 Diff vs Previous Quarter

- **New attack surface**: [endpoints/features added]
- **Removed surface**: [deprecated/removed]
- **Changed trust boundaries**: [e.g., new third-party integration]

---

## 1. CodeQL Security Scans

### 1.1 Scan Results

**Scan Date**: [Date] · **Workflow run**: [link]
**Total Alerts at T0**: [count] · **Languages**: C#, TypeScript, JavaScript

| Severity | Open at T0 | Triaged | Fixed | Mitigated | Risk-Accepted | Dismissed FP |
|----------|------------|---------|-------|-----------|---------------|--------------|
| Critical | | | | | | |
| High     | | | | | | |
| Medium   | | | | | | |
| Low      | | | | | | |

### 1.2 Findings (Critical/High)

> ⚠️ **C-1 (Wiegers)**: every finding requires evidence of remediation, not assertion.

#### Finding 1: [Title]
- **Severity**: Critical | High
- **CWE**: [CWE-ID]
- **Category**: [SQLi, XSS, Path Traversal, etc.]
- **Location**: `path/to/file.cs:line` (commit SHA)
- **Description**: [What the vulnerability is]
- **Impact**: [What an attacker could achieve]
- **Status**: ✅ Fixed | ⚠️ Mitigated | 📋 Risk-Accepted | ⏳ In Progress
- **Remediation evidence**:
  - PR: #[number]
  - Regression test: `path/to/test.cs::TestMethod` (must fail pre-fix, pass post-fix)
  - Verification command: `[command that proves the fix]`
- **Linked issue**: #[number]

### 1.3 Risk Acceptance Records

> 🆕 **C-2 (Adzic)**: see [Scenario: Critical finding non remediable](#scenarios) below.

#### Acceptance 1: [Title]
- **Alert**: CodeQL #[id]
- **Reason for acceptance**: [Technical/business justification]
- **Compensating controls active**: [WAF rule, monitoring, rate limit, etc.]
- **Re-review date**: [T0+30d, max]
- **Approver**: [@cto signature, PR link]

### 1.4 False Positives

#### FP 1: [Title]
- **Alert ID**: [CodeQL alert ID]
- **Reason**: [Technical justification — why CodeQL is wrong here]
- **Reviewed by**: [Name]
- **Suppression rule added**: [yes/no, link if added]

### 1.5 CodeQL Configuration

- **Query suites in use**: security-extended, security-and-quality
- **Custom queries**: [list with links]
- **New suppression rules this quarter**: [list with justifications]

---

## 2. Dependency & Supply Chain Security

> 🆕 **C-4 (Hightower)**: SBOM, IaC scan, and provenance are baseline for 2026.

### 2.1 Backend Dependencies (.NET)

**Scan Date**: [Date] · **Tool**: `dotnet list package --vulnerable --include-transitive`

| Package | Version | CVE | CVSS | Severity | Status | Evidence |
|---------|---------|-----|------|----------|--------|----------|
| | | | | | | |

**Update evidence**: [link to PR + test run]

### 2.2 Frontend Dependencies (pnpm)

**Scan Date**: [Date] · **Tool**: `pnpm audit --audit-level=moderate`

| Package | Version | CVE | Severity | Status | Evidence |
|---------|---------|-----|----------|--------|----------|
| | | | | | |

**Update evidence**: [link to PR + visual regression test run]

### 2.3 SBOM Generation

> 🆕 **Required artifacts** (archived in `docs/security/sboms/[QUARTER]/`):

- [ ] Backend SBOM (CycloneDX): `dotnet sbom-tool generate -b apps/api -bo apps/api/_sbom`
  → archive as `docs/security/sboms/2026-Q[X]/backend-sbom.cdx.json`
- [ ] Frontend SBOM: `pnpm exec @cyclonedx/cyclonedx-npm --output-file fe-sbom.cdx.json`
  → archive as `docs/security/sboms/2026-Q[X]/frontend-sbom.cdx.json`
- [ ] Container SBOM (per image): `syft <image> -o cyclonedx-json`
  → archive per image under `docs/security/sboms/2026-Q[X]/containers/`
- [ ] SBOM diff vs previous quarter generated and reviewed: [link]

### 2.4 Image Provenance & Signing

- [ ] Base image inventory: list of `FROM` directives across Dockerfiles, with origin (Docker Official / MS / etc.)
- [ ] Pinned to digest (not tag) verification: `grep -rE "FROM [^@]+:[^ ]+$" apps/*/Dockerfile`
- [ ] (Roadmap) cosign signing of meepleai-* images: status [planned / in-progress / done]
- [ ] Provenance attestation (SLSA level): current level [N/A | L1 | L2 | L3]

### 2.5 GitHub Actions Audit

- [ ] All third-party actions pinned to commit SHA (not tag):
  ```bash
  grep -rE "uses: [^@]+@v?[0-9]" .github/workflows/ | grep -vE "@[a-f0-9]{40}"
  # Expected output: empty
  ```
- [ ] `GITHUB_TOKEN` permissions reviewed per workflow (least-privilege): [link to audit doc]
- [ ] Dependabot enabled for `package-ecosystem: github-actions`: [yes/no]
- [ ] Secrets exposure check in workflow logs: [audit completed YYYY-MM-DD]

### 2.6 Exception Records (with re-review SLA)

#### Exception 1: [Package Name @ version]
- **Vulnerability**: [CVE-ID, description]
- **Risk Assessment**: Low | Medium | High (CVSS [score])
- **Justification**: [no exploit path / no patch available / breaking change blocked]
- **Compensating controls**: [list]
- **Re-review by**: [date, max T0+30d for High]
- **Owner**: [@user]

---

## 3. Security Best Practices Audit (Evidence-Based)

> ⚠️ **C-1 (Wiegers)**: every control row requires fresh evidence and a verification source.
> Empty `Last Verified` or `Evidence` ⇒ control is **untrusted** until verified.

### 3.1 Authentication & Authorization

| Control | Evidence Source | Last Verified | Status |
|---------|----------------|---------------|--------|
| Password hashing PBKDF2-SHA256 ≥ 210k iter | Test `Authentication.PasswordHasherTests.IterationCount` + grep `PBKDF2_ITERATIONS` | [date] | ✅/❌/⚠️ |
| Session expiration ≤ 30d | Config `Auth:SessionExpirationDays` snapshot | [date] | |
| API key generation: secure random + PBKDF2 | Test `ApiKeyGeneratorTests.EntropySufficient` | [date] | |
| RBAC enforced on all admin endpoints | Integration tests filter `BoundedContext=Administration && Category=Authorization` | [date] | |
| OAuth state parameter validated (CSRF) | Test `OAuthFlowTests.RejectsInvalidState` | [date] | |
| 2FA available + enforced for admin role | Config + test `TwoFactorTests.AdminRoleEnforced` | [date] | |
| Failed login lockout / rate limit | Integration test + alert rule in monitoring | [date] | |

**Findings**: [list]
**Actions**: [list with PR/issue links]

### 3.2 Secrets Management

| Control | Evidence Source | Last Verified | Status |
|---------|----------------|---------------|--------|
| No hardcoded secrets in codebase | `gitleaks detect` output (zero findings) | [date] | |
| `.env` and `.secret` in `.gitignore` | `git check-ignore` results | [date] | |
| Pre-commit hook active (detect-secrets) | `.pre-commit-config.yaml` review + last run | [date] | |
| GitHub secret scanning enabled | Repo settings screenshot or API check | [date] | |
| Secrets rotation procedure documented + practiced | Link to runbook + last rotation log | [date] | |

### 3.3 Input Sanitization & Validation

| Control | Evidence Source | Last Verified | Status |
|---------|----------------|---------------|--------|
| Parameterized queries only (EF Core) | grep for raw SQL: `SqlRaw`, `FromSqlRaw` | [date] | |
| FluentValidation on all command DTOs | Coverage report on `*Validator.cs` ≥ 95% | [date] | |
| XSS: React auto-escape + sanitizer on user HTML | grep for unsafe HTML injection helpers (`dangerously*` family) without sanitizer wrap | [date] | |
| File upload validation (MIME, size, content) | Test `DocumentProcessing.UploadTests.RejectsMaliciousPdf` | [date] | |

### 3.4 CORS & Security Headers

| Control | Evidence Source | Last Verified | Status |
|---------|----------------|---------------|--------|
| CORS allowlist (no `*` in production) | `Program.cs` snapshot + staging response headers | [date] | |
| CSP header configured + reportOnly mode validated | Header check via curl on staging | [date] | |
| `X-Frame-Options: DENY` | Header check | [date] | |
| `X-Content-Type-Options: nosniff` | Header check | [date] | |
| `Strict-Transport-Security` (HSTS) | Header check + preload list status | [date] | |
| `Referrer-Policy: strict-origin-when-cross-origin` (or stricter) | Header check | [date] | |

### 3.5 BC Risk-Tiered Review

> 🆕 **M-1 (Newman)**: 18 BCs, not equal risk. Tiered cadence.

| BC | Tier | Last Reviewed | Reviewer | Findings | Notes |
|----|------|--------------|----------|----------|-------|
| Authentication | 1 | [date] | | | every quarter |
| Administration | 1 | [date] | | | every quarter |
| DocumentProcessing | 1 | [date] | | | every quarter (untrusted PDF input) |
| KnowledgeBase | 1 | [date] | | | every quarter (prompt injection vector) |
| AgentMemory | 2 | [date] | | | rotated, sample 1/quarter |
| GameManagement | 2 | [date] | | | rotated |
| UserLibrary | 2 | [date] | | | rotated |
| (other tier-2) | 2 | | | | |
| Gamification | 3 | [date] | | | annual |
| (other tier-3) | 3 | | | | |

---

## 4. Infrastructure Security

### 4.1 Docker & Containers

| Control | Evidence Source | Last Verified | Status |
|---------|----------------|---------------|--------|
| Non-root user in containers | grep `USER` in Dockerfiles + runtime check | [date] | |
| Minimal base images (alpine/distroless) | Dockerfile review | [date] | |
| Multi-stage builds | Dockerfile review | [date] | |
| No secrets in image layers | `docker history` + Trivy secret scan | [date] | |

### 4.2 IaC Static Analysis

> 🆕 **C-4 (Hightower)**: required this quarter onwards.

- [ ] **Hadolint**: `hadolint apps/*/Dockerfile infra/*Dockerfile`
  → archive output in `docs/security/iac-scans/2026-Q[X]/hadolint.txt`
- [ ] **Trivy config-scan**: `trivy config infra/`
  → archive in `docs/security/iac-scans/2026-Q[X]/trivy-config.json`
- [ ] **Checkov**: `checkov -d infra/`
  → archive in `docs/security/iac-scans/2026-Q[X]/checkov.json`
- [ ] All HIGH/CRITICAL IaC findings triaged: [link to triage table]

### 4.3 Environment & Secrets at Runtime

| Control | Evidence Source | Last Verified | Status |
|---------|----------------|---------------|--------|
| Secrets only via env vars / secret store | `docker-compose.yml` review | [date] | |
| `.env` files not in image | `docker history` review | [date] | |
| Production uses Docker secrets / vault | Compose stack inspection | [date] | |
| Key rotation runbook tested | Last rotation drill: [date] | [date] | |

### 4.4 TLS/HTTPS

| Control | Evidence Source | Last Verified | Status |
|---------|----------------|---------------|--------|
| HTTPS enforced (HTTP→HTTPS redirect) | Traefik config + curl test | [date] | |
| TLS 1.2+ only | `nmap --script ssl-enum-ciphers` output | [date] | |
| Strong cipher suites (no RC4/3DES/CBC weak) | nmap output | [date] | |
| HSTS enabled with preload | Header + preload-list check | [date] | |
| Certificate auto-renewal verified | Last renewal log + expiry check | [date] | |
| Certificate transparency monitored | crt.sh feed subscription / CT log alert | [date] | |

### 4.5 API Rate Limiting

| Endpoint Class | Limit Configured | Test Verified | Status |
|----------------|------------------|---------------|--------|
| `/api/v1/auth/login` | [X req/min/IP] | Test `RateLimitTests.LoginThrottled` | |
| `/api/v1/auth/reset-password` | [X req/min/IP] | Test name | |
| Public API endpoints | [X req/min/key] | Test name | |
| Admin endpoints | [X req/min/user] | Test name | |
| AI/RAG endpoints (cost-sensitive) | [X req/min/user] | Test name | |
| File upload | [X req/h/user] + [Y MB total/day] | Test name | |

### 4.6 Monitoring, Logging & Security Alerts

> 🆕 **Required alert rules** (configured in Prometheus/Grafana/Serilog):

- [ ] Failed login burst (>10/min/IP) → `#security` Slack
- [ ] Privilege escalation event (role change in audit log) → `#security` Slack + email
- [ ] Secret detected in commit (pre-commit + GitHub secret scanning) → `#security`
- [ ] TLS cert expiry < 30d → `#ops`
- [ ] Vulnerability scan workflow failure 2 consecutive runs → `#security`
- [ ] Anomalous PDF upload (size, rate, source) → `#security`
- [ ] Unauthorized admin endpoint access (403/401 burst) → `#security`
- [ ] Database query >5s on auth tables → `#ops` (potential timing attack)

| Log Class | Retention | Storage | Last Verified |
|-----------|-----------|---------|---------------|
| Application | [X days] | | [date] |
| Security events | [X days, ≥365 recommended] | | [date] |
| Audit (admin actions) | [X days, ≥365 recommended] | | [date] |
| HTTP access | [X days] | | [date] |

---

## 5. Runtime & DAST

> 🆕 **M-2 (Hightower)**: SAST + SCA cover code; DAST covers running system.

### 5.1 OWASP ZAP Baseline (staging)

- [ ] ZAP baseline scan run against staging (unauth):
  → archive in `docs/security/dast/2026-Q[X]/zap-baseline-unauth.html`
- [ ] ZAP authenticated scan (with valid session):
  → archive as `zap-baseline-auth.html`
- [ ] All HIGH/MEDIUM ZAP findings triaged: [link to triage]

### 5.2 API Fuzzing

- [ ] Schemathesis or RESTler against OpenAPI on staging:
  ```bash
  schemathesis run --base-url https://staging.meepleai.com http://localhost:8080/openapi/v1.json --checks all
  ```
  → archive output in `docs/security/dast/2026-Q[X]/schemathesis.log`

### 5.3 Container Runtime

- [ ] Falco or equivalent rules active on staging: [yes/no]
- [ ] Anomalous syscall events reviewed: [link to review]

### 5.4 (Annual, recorded if performed in this quarter)

- [ ] External pen-test scheduled / performed: [link to report]

---

## 6. Security Test Coverage

> 🆕 **C-5 (Crispin)**: review must verify regression coverage of past findings.

### 6.1 Coverage Gates

```bash
dotnet test --filter "Category=Security" /p:CollectCoverage=true /p:CoverletOutput=./coverage/security/
```

**Required gates** (this quarter):

| BC | Required line coverage | Actual | Status |
|----|------------------------|--------|--------|
| Authentication | ≥ 95% | [X%] | ✅/❌ |
| Administration | ≥ 95% | [X%] | |
| DocumentProcessing | ≥ 90% | [X%] | |
| KnowledgeBase | ≥ 85% | [X%] | |

### 6.2 Test Categories Required

- [ ] Authentication flow tests (positive + negative): [count] tests
- [ ] Authorization tests (RBAC matrix): role × endpoint coverage
- [ ] Input validation fuzz tests: [count] FluentValidation rules covered
- [ ] SQL injection regression tests: [count]
- [ ] XSS regression tests: [count]
- [ ] CSRF regression tests: [count]
- [ ] OAuth flow tests (state, PKCE, redirect_uri validation): [count]
- [ ] Rate limit regression tests: [count]

### 6.3 Regression Test Requirement

> ⚠️ **Hard rule**: every Critical/High finding fixed this quarter MUST have a test that:
> 1. Fails on pre-fix branch (commit SHA recorded)
> 2. Passes on post-fix branch (commit SHA recorded)
> 3. Is in `Category=SecurityRegression`

| Finding | Fix PR | Regression Test | Pre-fix SHA | Post-fix SHA |
|---------|--------|-----------------|-------------|--------------|
| | | | | |

---

## 7. Process Workflow

### Week 1 — Preparation
- [ ] All scans triggered (CodeQL, dotnet, pnpm, SBOM, IaC, ZAP)
- [ ] T0 snapshot frozen (counts recorded above)
- [ ] Threat model section 0 drafted
- [ ] RACI confirmed

### Week 2 — Triage & Workshop
- [ ] **Three Amigos workshop**: Security Champion + 1 dev/tier-1-BC + 1 QA
  - Output: per-finding decision (fix / mitigate / accept)
  - Notes archived in `docs/security/2026-Q[X]-workshop-notes.md`
- [ ] Issues created for fixes with owners + SLA
- [ ] Risk acceptance records drafted

### Weeks 3–8 — Remediation
- [ ] Critical fixes deployed (target: T0+30d)
- [ ] High fixes deployed (target: T0+60d)
- [ ] Regression tests added (verified per §6.3)
- [ ] Dependencies updated and verified

### Weeks 9–13 — Validation & Sign-off
- [ ] Re-scan + verification: post-fix scan compared to T0 snapshot
- [ ] Report finalized
- [ ] CTO sign-off on risk-accepted items
- [ ] Action items for Q[X+1] filed as issues

---

## 8. Scenarios (Given / When / Then)

> 🆕 **C-2 (Adzic)**: explicit scenarios for non-conformance — what happens when things go wrong.

### Scenario 1: Critical CodeQL finding non remediable within quarter
```gherkin
Given a CodeQL alert with severity=critical open at T0
When the fix requires a breaking change to a public API
Then a risk-acceptance-record.md is created including:
  - threat model of the exploitation path
  - compensating controls active (e.g. WAF rule)
  - re-review date (≤ T0+30d)
  - approval signature from CTO
And the alert remains open on GitHub with label `accepted-risk`
And the record is linked from this report §1.3
```

### Scenario 2: Vulnerable dependency with no upstream patch
```gherkin
Given pnpm audit reports a high CVE on package X@Y
And no patched version exists at T0+7d
When the package is a transitive dependency
Then we choose one of:
  - pnpm.overrides with a patched fork, OR
  - removal of the consumer feature, OR
  - runtime mitigation (extra input validation, network policy)
And document the decision with re-review date T0+30d
```

### Scenario 3: Review not completed within 14 days
```gherkin
Given T0+14d and the security-review issue is still open
When no PR labeled `security-review-q[X]` is in review
Then the issue is escalated with label `escalation/sla-breach`
And an automated notification fires in #security
And the Accountable role (CTO) is paged
```

### Scenario 4: Required scan tooling fails
```gherkin
Given the CodeQL workflow fails 2 consecutive runs
When the cause cannot be resolved within 24h
Then we fall back to:
  - Semgrep with security ruleset, AND
  - manual review of `BoundedContexts/{Authentication,Administration}/`
And document the fallback in this report §1
```

### Scenario 5: New BC introduced during the quarter
```gherkin
Given a new bounded context added between T0 and T0+90d
When the BC handles auth, secrets, or untrusted input
Then it must be added to tier-1 and reviewed before quarter close
Else it can defer to next quarter at tier-2
```

---

## 9. Metrics & KPIs

### Vulnerability Metrics

| Metric | This Q | Last Q | Target | Trend |
|--------|--------|--------|--------|-------|
| Critical findings (T0) | | | 0 carried forward | |
| High findings (T0) | | | ≤ last Q | |
| MTTR critical (days) | | | ≤ 7 | |
| MTTR high (days) | | | ≤ 30 | |
| Vulnerability backlog (T0+90d) | | | 0 critical, ≤ 2 high | |
| False positive rate | | | ≤ 15% | |
| Risk-accepted count (active) | | | ≤ 5 | |

### Process Metrics

| Metric | This Q | Target |
|--------|--------|--------|
| Review completion time (T0 to sign-off, days) | | ≤ 90 |
| Draft due delivered by T0+14d | | ✅ |
| Workshop held (Week 2) | | ✅ |
| Regression tests added per fix | | 100% |
| BC tier-1 coverage on security tests | | ≥ 95% |

### Supply Chain Metrics

| Metric | This Q | Target |
|--------|--------|--------|
| SBOMs generated (backend, frontend, containers) | | 3/3 |
| GitHub Actions pinned to SHA | | 100% |
| Dockerfile base images pinned to digest | | 100% |
| IaC scans run with no HIGH/CRITICAL untriaged | | ✅ |

---

## 10. Lessons Learned

### What Went Well
1.
2.
3.

### What Could Be Improved
1.
2.
3.

### Process Improvements for Next Quarter
1.
2.
3.

---

## 11. Action Items for Next Quarter

### High Priority (must close in Q[X+1])
- [ ] [Item] — Owner: [@user] — Due: [date]

### Medium Priority
- [ ] [Item] — Owner: [@user] — Due: [date]

### Continuous Improvements
- [ ] [Item] — Owner: [@user]

---

## 12. Sign-off

| Role | Name | Date | Signature (PR comment link) |
|------|------|------|-----------------------------|
| Reviewer (Responsible) | | | |
| Approver (Accountable) | | | |

---

## Appendix A — Tools & Versions

| Tool | Version | Purpose |
|------|---------|---------|
| CodeQL | | SAST |
| dotnet SDK | | dependency audit |
| pnpm | | dependency audit |
| Hadolint | | Dockerfile lint |
| Trivy | | container & config scan |
| Checkov | | IaC scan |
| syft | | SBOM generation |
| OWASP ZAP | | DAST |
| Schemathesis | | API fuzzing |
| gitleaks | | secret scanning |

## Appendix B — Success Criteria (SMART)

> 🆕 **C-1 (Wiegers)**: replaces vague "100% addressed" wording.

- **SC-1**: 100% of CodeQL alerts with severity ∈ {critical, high} open at T0 are in state {fixed, mitigated-with-control, accepted-with-justification} by T0+90d.
  *Verification*: GraphQL `code-scanning/alerts?state=fixed,dismissed` filtered by `created_at ≤ T0`.
- **SC-2**: Document `2026-Q[X]-security-review.md` has §0–6 filled and signed by ≥1 reviewer + 1 approver by T0+14d.
  *Verification*: PR merged with label `security-review-q[X]`.
- **SC-3**: MTTR for critical ≤ 7d, high ≤ 30d.
  *Verification*: SQL/script over closed issues with label `security` in the quarter.
- **SC-4**: Zero CodeQL critical alerts carried forward to Q[X+1].
  *Verification*: GraphQL alert count at T0+90d.
- **SC-5**: All tier-1 BCs reviewed and security test coverage ≥ 95%.
  *Verification*: `dotnet test --filter Category=Security /p:CollectCoverage=true` output.

## Appendix C — References

- [SECURITY.md](../../SECURITY.md)
- [OWASP Top 10 2021](https://owasp.org/Top10/)
- [OWASP ASVS 4.0](https://owasp.org/www-project-application-security-verification-standard/)
- [CWE Top 25](https://cwe.mitre.org/top25/)
- [NIST SSDF (SP 800-218)](https://csrc.nist.gov/Projects/ssdf)
- [SLSA Framework](https://slsa.dev/)
- [GitHub Security Advisories](https://github.com/advisories)

---

**Next Review Due**: [Next Quarter Start Date]
**Spec-Panel revision history**:
- v1: original (pre-2026-05-05) — archived at [archive/security-review-template-v1.md](./archive/security-review-template-v1.md)
- v2 (2026-05-05): post-spec-panel hardening — addresses Critical findings C-1…C-5 from #186 review

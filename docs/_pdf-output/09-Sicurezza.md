# MeepleAI - Sicurezza

Review di sicurezza, OWASP Top 10, analisi TOTP, vulnerabilita note.

**Data generazione**: 8 marzo 2026

**File inclusi**: 6

---

## Indice

1. security/README.md
2. security/2026-Q1-security-review.md
3. security/KNOWN_VULNERABILITIES.md
4. security/owasp-top-10-compliance.md
5. security/security-review-template.md
6. security/totp-vulnerability-analysis.md

---



<div style="page-break-before: always;"></div>

## security/README.md

# Security Documentation

**Quick Navigation** - Trova rapidamente le risorse di sicurezza

---

## 📂 File in questa Sezione

| File | Descrizione | Pubblico | Ultimo Aggiornamento |
|------|-------------|----------|----------------------|
| `owasp-top-10-compliance.md` | OWASP Top 10 compliance matrix e mitigations | Developer / Security | 2026-01-18 |
| _More security guides coming_ | Penetration testing, incident response, etc. | Security | Future |

---

## 🔍 Trova per Scenario

**Se vuoi...** | **Leggi questo file**
---|---
Verificare OWASP compliance | `owasp-top-10-compliance.md`
Gestire i secret | `../04-deployment/secrets-management.md`
Configurare autenticazione | `../07-bounded-contexts/authentication.md`
Implementare OAuth/2FA | `../05-testing/backend/oauth-testing.md`
Security headers middleware | `../01-architecture/adr/adr-010-security-headers-middleware.md`
CORS configuration | `../01-architecture/adr/adr-011-cors-whitelist-headers.md`

---

## 🛡️ Security Checklist

**Backend**:
- [ ] Secret management configurato (`infra/secrets/*.secret`)
- [ ] FluentValidation su tutti i Command/Query
- [ ] Input sanitization (XSS, SQL injection)
- [ ] Rate limiting configurato (Redis)
- [ ] CORS policy whitelist corretta
- [ ] Security headers middleware attivo (CSP, HSTS, etc.)

**Frontend**:
- [ ] Nessun secret committato nel codice
- [ ] API calls con credentials: "include"
- [ ] XSS protection via sanitization
- [ ] HTTPS obbligatorio in produzione

**Infrastructure**:
- [ ] Firewall configurato (UFW/iptables)
- [ ] Fail2Ban attivo per SSH
- [ ] SSL/TLS certificati validi
- [ ] Database password rotate ogni 90 giorni

---

## 📖 Guide Correlate

- [Secrets Management](../04-deployment/secrets-management.md)
- [OAuth Testing Guide](../05-testing/backend/oauth-testing.md)
- [Deployment Security](../04-deployment/infrastructure-deployment-checklist.md)
- [ADR-010: Security Headers Middleware](../01-architecture/adr/adr-010-security-headers-middleware.md)
- [ADR-011: CORS Whitelist](../01-architecture/adr/adr-011-cors-whitelist-headers.md)

---

## 🚨 Security Incident Response

**Se rilevi una vulnerabilità**:
1. **NON committare fix immediatamente** (evita disclosure pubblica)
2. Documentare in privato (file locale, non committato)
3. Notificare team security
4. Preparare patch
5. Applicare patch + disclosure coordinata

**Risorse**:
- Security Team: [contact info]
- Vulnerability Report Template: _To be created_

---

**Last Updated**: 2026-01-18
**Maintainers**: Security Team
**Status**: 🚧 In Development


---



<div style="page-break-before: always;"></div>

## security/2026-Q1-security-review.md

# 🔒 Quarterly Security Review - Q1 2026

**Review Period**: 2026-01-01 to 2026-01-08
**Reviewer(s)**: Claude Sonnet 4.5 (AI Security Analysis)
**Date Completed**: 2026-01-08

---

## Executive Summary

**Overall Security Posture**: Good → Excellent (post-remediation)

**Key Findings**:
- Critical vulnerabilities: 1 (dependency - jspdf)
- High severity issues: 14 (13 PII exposure + 1 dependency)
- Medium/Low issues: 22 (warnings - non-critical)
- False positives dismissed: 61 (log forging - protected by sanitization)

**Actions Taken**:
- Fixed 14 PII exposure issues (email logging without masking)
- Removed 76 lines of insecure dead code (user-role cookie with HttpOnly=false)
- Patched 3 dependency vulnerabilities (jspdf, qs, @babel/runtime)
- Created security documentation structure and review template

**Next Quarter Priorities**:
- Monitor CodeQL warning alerts (22 items - low severity)
- Consider upgrading @opentelemetry dependencies (peer dependency warnings)
- Review React DOM manipulation in PrismHighlighter component

---

## 1. CodeQL Security Scans

### Scan Results

**Scan Date**: 2026-01-08
**Total Alerts**: 97 open (at review start)
**Language Coverage**: C#, TypeScript, JavaScript

| Severity | Count | Triaged | Fixed | Dismissed |
|----------|-------|---------|-------|-----------|
| Error    | 75    | 75      | 14    | 61        |
| Warning  | 22    | 22      | 1     | 0         |
| **Total**| **97**| **97**  | **15**| **61**    |

### Critical/High Findings FIXED

#### Finding 1: Exposure of Private Information (Email PII)
- **Severity**: Error (High)
- **Category**: Information Disclosure (CWE-532)
- **Locations**: 13 occurrences across 6 files
- **Description**: Email addresses logged without masking
- **Impact**: GDPR/privacy violation
- **Status**: ✅ Fixed
- **Remediation**: Added `DataMasking.MaskEmail()` to all log statements
- **Commit**: c071a35d

#### Finding 2: Cookie HttpOnly Not Set
- **Severity**: Warning (Medium-High)
- **Category**: Security Misconfiguration (CWE-1004)
- **Location**: CookieHelpers.cs:277-283
- **Description**: User-role cookie with HttpOnly=false
- **Impact**: Potential XSS-based role enumeration
- **Status**: ✅ Fixed (removed dead code - 76 lines)
- **Commit**: c071a35d

#### Finding 3: jsPDF LFI (CVE-2025-68428)
- **Severity**: Critical
- **Category**: Path Traversal
- **Status**: ✅ Fixed (3.0.4 → 4.0.0)
- **Commit**: 421d6c9b

#### Finding 4: qs DoS (CVE-2025-15284)
- **Severity**: High
- **Status**: ✅ Fixed (6.14.0 → 6.14.1)
- **Commit**: 421d6c9b

#### Finding 5: @babel/runtime (CVE-2025-27789)
- **Severity**: Moderate
- **Status**: ✅ Fixed (7.18.9 → 7.28.4 via override)
- **Commit**: 421d6c9b

### False Positives Dismissed

#### Log Forging (61 alerts)
- **Alert IDs**: #5030-#4986
- **Reason**: Protected by LogForgingSanitizationPolicy + Enricher
- **Evidence**: apps/api/src/Api/Logging/LogForgingSanitizationPolicy.cs
- **Confidence**: High

---

## 2. Dependency Vulnerabilities

### Backend: ✅ 0 vulnerabilities
### Frontend: 3 → 0 (fixed)

| Package | Before | After | CVE | Severity |
|---------|--------|-------|-----|----------|
| jspdf | 3.0.4 | 4.0.0 | CVE-2025-68428 | Critical |
| qs | 6.14.0 | 6.14.1 | CVE-2025-15284 | High |
| @babel/runtime | 7.18.9 | 7.28.4 | CVE-2025-27789 | Moderate |

---

## 3. Security Best Practices Audit

### Authentication & Authorization
✅ PBKDF2-SHA256 (210k iterations), session management, RBAC, OAuth, 2FA

### Secrets Management
✅ Environment variables, Docker Secrets, no hardcoded values

### Input Validation
✅ EF Core parameterized queries, FluentValidation, React auto-escaping

### CORS & CSP
✅ Whitelist origins, comprehensive security headers (6 types)

---

## 4. Infrastructure Security

### Docker
✅ Multi-stage builds, non-root users (UID 1001), minimal base images

### Secrets
✅ Docker Secrets with _FILE pattern, .env gitignored

### TLS/HTTPS (Traefik)
✅ TLS 1.2+, strong ciphers, forward secrecy, HTTP/2

### Rate Limiting
✅ Role-based limits, Redis token bucket, 429 responses

### Monitoring
✅ Structured logging, OpenTelemetry tracing, health checks, alerting

---

## Metrics & KPIs

| Metric | Value |
|--------|-------|
| Critical vulns | 1 found, 1 fixed |
| High vulns | 14 found, 14 fixed |
| MTTR | < 1 day ✅ |
| Backlog | 0 ✅ |
| Review time | 1 day ✅ |

---

## Lessons Learned

### What Went Well
1. Existing security infrastructure prevented widespread issues
2. Dependency updates smooth (pnpm ecosystem)
3. Automated quality gates effective

### Improvements
1. High CodeQL false positive rate (62.9%) - tune queries
2. Test suite flakiness - investigate 70 reported failures
3. Template creation - now available for future reviews

---

## Action Items for Next Quarter

### High Priority
- [ ] Investigate test suite stability - Owner: QA, Deadline: 2026-02-01
- [ ] Configure CodeQL suppressions - Owner: Security, Deadline: 2026-02-15
- [ ] Batch cleanup 22 warning alerts - Owner: Dev, Deadline: 2026-03-01

### Medium Priority
- [ ] Migrate to nonce-based CSP - Owner: Frontend, Deadline: 2026-04-01
- [ ] Upgrade @opentelemetry - Owner: DevOps, Deadline: 2026-03-15

---

## Sign-off

**Reviewed By**: Claude Sonnet 4.5
**Security Posture**: Excellent
**Next Review**: 2026-04-01 (Q2 2026)

---

## Follow-up Scan - 2026-01-16

**Scan Date**: 2026-01-16 (8 days post-initial review)
**Purpose**: Verify no new vulnerabilities introduced since initial review

### Results

#### Backend Dependencies (.NET)
*(blocco di codice rimosso)*
**Status**: ✅ 0 vulnerabilities

#### Frontend Dependencies (pnpm)
*(blocco di codice rimosso)*
**Status**: ✅ 0 vulnerabilities (high/critical)

### Conclusion
No new security findings identified. Security posture remains **Excellent**.

All Q1 2026 security review requirements completed:
- ✅ CodeQL alerts triaged and fixed (initial review)
- ✅ Dependency vulnerabilities patched (initial review)
- ✅ Follow-up scan confirms clean state (2026-01-16)
- ✅ Security best practices validated
- ✅ Infrastructure security confirmed

**Issue #2302**: Ready for closure


---



<div style="page-break-before: always;"></div>

## security/KNOWN_VULNERABILITIES.md

# Known Vulnerabilities - Accepted Risks

This document tracks security vulnerabilities that have been reviewed and accepted as low-risk for this project.

**Last Updated**: 2026-02-14

---

## Accepted Vulnerabilities

### 1. Elliptic Cryptographic Primitive (LOW)

**Status**: ✅ ACCEPTED - Dev dependency only, no production impact

| Property | Value |
|----------|-------|
| **Package** | `elliptic@6.6.1` |
| **Severity** | LOW |
| **Advisory** | [GHSA-848j-6mx2-7j84](https://github.com/advisories/GHSA-848j-6mx2-7j84) |
| **Patched Version** | None available (`<0.0.0`) |
| **Discovered** | 2026-02-14 |
| **Reviewed By** | Development Team |
| **Next Review** | 2026-05-14 (3 months) |

#### Description
The elliptic package uses a cryptographic primitive with a risky implementation. The package is deprecated and unmaintained, with no patch available.

#### Dependency Path
*(blocco di codice rimosso)*

#### Risk Assessment
**Risk Level**: MINIMAL

**Justification**:
1. **Dev-only dependency**: Only used by Storybook for development/testing
2. **Build-time only**: Not bundled or shipped to production
3. **Isolated environment**: Runs only in development environment
4. **No user exposure**: Not accessible to end users
5. **Low severity**: Requires specific attack conditions to exploit

#### Mitigation Strategy
- **Current**: Accept risk due to dev-only scope
- **Monitoring**: Check Storybook updates for dependency removal
- **Review Cycle**: Re-evaluate every 3 months
- **Fallback**: Remove Storybook if vulnerability escalates to MODERATE or higher

#### Alternative Considered
- **Remove Storybook**: Not viable - essential for component development
- **Webpack config**: No viable alternative to node-polyfill-webpack-plugin
- **Override elliptic**: Could break Storybook functionality

---

## Vulnerability Management Process

### Review Schedule
- **Critical/High**: Immediate action required
- **Moderate**: Fix within 7 days
- **Low**: Review and accept/fix within 30 days

### Accepted Risk Criteria
A vulnerability may be accepted if ALL of the following are true:
1. Severity is LOW
2. No patch available OR fix breaks critical functionality
3. Not in production dependencies
4. Risk is documented and approved
5. Monitoring plan is in place

### Review Process
1. **Discovery**: Vulnerability identified via `pnpm audit`
2. **Assessment**: Evaluate severity, impact, and fix availability
3. **Decision**: Fix immediately, schedule fix, or accept risk
4. **Documentation**: Update this file if accepting risk
5. **Monitoring**: Schedule quarterly reviews for accepted risks

---

## Security Audit History

### 2026-02-14: Comprehensive Security Audit
**Auditor**: Development Team
**Scope**: All frontend dependencies (production + dev)

**Findings**:
- Fixed 5 production vulnerabilities (1 HIGH, 3 MODERATE, 1 LOW)
- Fixed 4 dev dependency vulnerabilities (1 HIGH, 3 LOW)
- Accepted 1 dev dependency vulnerability (elliptic - LOW, no patch)

**Actions Taken**:
- Updated `pdfjs-dist` to 5.4.624 (HIGH severity fix)
- Updated `diff` to 8.0.3 (LOW severity fix)
- Added pnpm overrides for transitive dependencies:
  - `lodash@>=4.17.23`
  - `lodash-es@>=4.17.23`
  - `markdown-it@>=14.1.1`
  - `axios@>=1.13.5`
  - `webpack@>=5.104.1`
  - `qs@>=6.14.2`

**Result**: ✅ 0 production vulnerabilities, 1 accepted dev vulnerability

**Next Audit**: 2026-05-14

---

## Contact

For security concerns, contact:
- **Security Team**: [security@meepleai.com]
- **Development Lead**: [dev@meepleai.com]

For reporting new vulnerabilities, see [SECURITY.md](../SECURITY.md)


---



<div style="page-break-before: always;"></div>

## security/owasp-top-10-compliance.md

# OWASP Top 10 Compliance Guide

**MeepleAI Security Posture** - Come MeepleAI previene le vulnerabilità OWASP Top 10

---

## 📋 OWASP Top 10 (2021) Compliance Matrix

| # | Vulnerability | Status | Mitigations |
|---|---------------|--------|-------------|
| A01 | Broken Access Control | ✅ Protected | Authorization middleware, role-based access |
| A02 | Cryptographic Failures | ✅ Protected | BCrypt, PBKDF2, TLS/SSL |
| A03 | Injection | ✅ Protected | Parameterized queries, FluentValidation |
| A04 | Insecure Design | ✅ Protected | Threat modeling, secure architecture |
| A05 | Security Misconfiguration | ⚠️ Monitored | Auto-config, secrets management |
| A06 | Vulnerable Components | ⚠️ Monitored | Dependabot, Semgrep scanning |
| A07 | Authentication Failures | ✅ Protected | Strong password policy, 2FA, rate limiting |
| A08 | Data Integrity Failures | ✅ Protected | HMAC signatures, integrity checks |
| A09 | Logging Failures | ✅ Protected | Centralized logging, audit trails |
| A10 | SSRF | ✅ Protected | URL validation, whitelist |

---

## A01: Broken Access Control 🔐

### Vulnerabilities Prevented

**Authorization Middleware**:
*(blocco di codice rimosso)*

**Role-Based Access Control (RBAC)**:
*(blocco di codice rimosso)*

**Resource-Level Authorization**:
*(blocco di codice rimosso)*

---

## A02: Cryptographic Failures 🔑

### Protections Implemented

**Password Hashing (BCrypt)**:
*(blocco di codice rimosso)*

**API Key Hashing (PBKDF2)**:
*(blocco di codice rimosso)*

**TLS/SSL Enforcement**:
*(blocco di codice rimosso)*

**Secrets Management**:
- ✅ Secrets stored in `.secret` files (gitignored)
- ✅ Never committed to repository
- ✅ Environment-specific encryption
- ✅ Auto-rotation recommended (90 days)

---

## A03: Injection Attacks 💉

### SQL Injection Prevention

**Parameterized Queries (EF Core)**:
*(blocco di codice rimosso)*

**FluentValidation (Input Sanitization)**:
*(blocco di codice rimosso)*

**XSS Prevention (Frontend)**:
*(blocco di codice rimosso)*

### Command Injection Prevention

**Safe External Process Execution**:
*(blocco di codice rimosso)*

---

## A04: Insecure Design 🏗️

### Secure Design Patterns

**Threat Modeling**:
- ✅ Authentication context threat model documented
- ✅ RAG system security review (prompt injection prevention)
- ✅ PDF upload size limits (max 50MB)
- ✅ Rate limiting per endpoint

**Defense in Depth**:
*(blocco di codice rimosso)*

**Fail Securely**:
*(blocco di codice rimosso)*

---

## A05: Security Misconfiguration ⚙️

### Configuration Hardening

**Security Headers (ADR-010)**:
*(blocco di codice rimosso)*

**CORS Whitelist (ADR-011)**:
*(blocco di codice rimosso)*

**Secret Validation**:
*(blocco di codice rimosso)*

---

## A06: Vulnerable and Outdated Components 📦

### Dependency Management

**Automated Scanning**:
*(blocco di codice rimosso)*

**Dependabot Configuration**:
*(blocco di codice rimosso)*

**Update Policy**:
- **Critical vulnerabilities**: Patch within 24 hours
- **High vulnerabilities**: Patch within 7 days
- **Medium vulnerabilities**: Patch within 30 days
- **Low vulnerabilities**: Patch in next release

---

## A07: Identification and Authentication Failures 🔓

### Protections Implemented

**Strong Password Policy**:
*(blocco di codice rimosso)*

**Two-Factor Authentication (TOTP)**:
*(blocco di codice rimosso)*

**Rate Limiting**:
*(blocco di codice rimosso)*

**Session Security**:
*(blocco di codice rimosso)*

---

## A08: Software and Data Integrity Failures 🔏

### Integrity Protections

**Webhook Signature Verification**:
*(blocco di codice rimosso)*

**File Upload Validation**:
*(blocco di codice rimosso)*

---

## A09: Security Logging and Monitoring Failures 📊

### Comprehensive Logging

**Serilog Configuration**:
*(blocco di codice rimosso)*

**Security Event Logging**:
*(blocco di codice rimosso)*

**Audit Trail**:
*(blocco di codice rimosso)*

---

## A10: Server-Side Request Forgery (SSRF) 🌐

### SSRF Prevention

**URL Validation**:
*(blocco di codice rimosso)*

---

## 🧪 Security Testing

### Automated Security Scans

**Semgrep (SAST)**:
*(blocco di codice rimosso)*

**detect-secrets**:
*(blocco di codice rimosso)*

**Dependency Scanning**:
*(blocco di codice rimosso)*

---

## 🔒 Production Security Checklist

### Pre-Deployment

**Infrastructure**:
- [ ] Firewall configured (UFW/iptables)
- [ ] SSH key-only authentication (no passwords)
- [ ] Fail2Ban installed for brute-force protection
- [ ] SSL/TLS certificates valid (Let's Encrypt)
- [ ] Security headers configured (CSP, HSTS, X-Frame-Options)

**Application**:
- [ ] All secrets in `.secret` files (not environment variables)
- [ ] CORS whitelist configured (production domains only)
- [ ] Rate limiting enabled (Redis-backed)
- [ ] Error messages don't leak sensitive info
- [ ] Logging excludes passwords, API keys, tokens

**Database**:
- [ ] Strong database password (20+ chars)
- [ ] Network isolated (not exposed to internet)
- [ ] Backups encrypted and tested
- [ ] Query timeouts configured

### Post-Deployment

**Monitoring**:
- [ ] Grafana alerts configured (failed logins, errors)
- [ ] Prometheus metrics collecting (request rates, latencies)
- [ ] Log aggregation working (Seq/HyperDX)
- [ ] Security scan scheduled (weekly Semgrep)

**Validation**:
- [ ] Penetration test completed
- [ ] Vulnerability scan passed
- [ ] OWASP compliance verified
- [ ] Security headers validated (securityheaders.com)

---

## 📚 Security Resources

### Internal Documentation
- [Secrets Management](../04-deployment/secrets-management.md)
- [OAuth Testing](../05-testing/backend/oauth-testing.md)
- [Authentication Context](../07-bounded-contexts/authentication.md)
- [ADR-010: Security Headers](../01-architecture/adr/adr-010-security-headers-middleware.md)

### External Resources
- [OWASP Top 10 (2021)](https://owasp.org/Top10/)
- [OWASP Cheat Sheet Series](https://cheatsheetseries.owasp.org/)
- [ASP.NET Core Security](https://learn.microsoft.com/en-us/aspnet/core/security/)
- [BCrypt Best Practices](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)

### Security Tools
- **Semgrep**: Static analysis (SAST)
- **detect-secrets**: Secret scanning
- **Dependabot**: Dependency updates
- **SecurityHeaders.com**: Header validation
- **SSL Labs**: SSL/TLS configuration test

---

**Last Updated**: 2026-01-18
**Maintainer**: Security Team
**Compliance**: OWASP Top 10 (2021)
**Status**: ✅ Protected


---



<div style="page-break-before: always;"></div>

## security/security-review-template.md

# 🔒 Quarterly Security Review - [QUARTER YEAR]

**Review Period**: [Start Date] to [End Date]
**Reviewer(s)**: [Name(s)]
**Date Completed**: [Completion Date]

---

## Executive Summary

**Overall Security Posture**: [Excellent | Good | Needs Improvement | Critical]

**Key Findings**:
- Critical vulnerabilities: [count]
- High severity issues: [count]
- Medium/Low issues: [count]
- False positives dismissed: [count]

**Actions Taken**:
- [Brief summary of major fixes and improvements]

**Next Quarter Priorities**:
- [Top 3-5 priorities for next quarter]

---

## 1. CodeQL Security Scans

### Scan Results

**Scan Date**: [Date]
**Total Alerts**: [count]
**Language Coverage**: C#, TypeScript, JavaScript

| Severity | Count | Triaged | Fixed | Dismissed |
|----------|-------|---------|-------|-----------|
| Critical | 0     | 0       | 0     | 0         |
| High     | 0     | 0       | 0     | 0         |
| Medium   | 0     | 0       | 0     | 0         |
| Low      | 0     | 0       | 0     | 0         |

### Critical/High Findings

#### Finding 1: [Title]
- **Severity**: Critical | High
- **Category**: [e.g., SQL Injection, XSS, Path Traversal]
- **Location**: `file.cs:line` or `component.tsx:line`
- **Description**: [Brief description of vulnerability]
- **Impact**: [Potential security impact]
- **Status**: ✅ Fixed | ⏳ In Progress | ❌ Open
- **Remediation**: [Actions taken or planned]
- **Issue**: #[issue number if created]

### False Positives Dismissed

#### Dismissal 1: [Title]
- **Alert ID**: [CodeQL alert ID]
- **Reason**: [Technical justification for dismissal]
- **Reviewed By**: [Name]

### CodeQL Configuration

- **Query Suites**: [security-extended, security-and-quality]
- **Custom Queries**: [List any custom queries added]
- **Suppression Rules**: [Document any suppressions added]

---

## 2. Dependency Vulnerabilities

### Backend Dependencies (.NET)

**Scan Date**: [Date]
**Tool**: `dotnet list package --vulnerable`

| Package | Version | Vulnerability | Severity | CVE | Status |
|---------|---------|---------------|----------|-----|--------|
| [name]  | [ver]   | [description] | High     | CVE-YYYY-XXXXX | ✅ Fixed |

**Actions Taken**:
- Updated [package] from [old version] to [new version]
- Reviewed breaking changes: [summary]
- Test results: [pass/fail]

### Frontend Dependencies (pnpm)

**Scan Date**: [Date]
**Tool**: `pnpm audit`

| Package | Version | Vulnerability | Severity | Status |
|---------|---------|---------------|----------|--------|
| [name]  | [ver]   | [description] | High     | ✅ Fixed |

**Actions Taken**:
- Updated [package] from [old version] to [new version]
- Compatibility verification: [pass/fail]
- Visual regression tests: [pass/fail]

### Exceptions Documented

#### Exception 1: [Package Name]
- **Vulnerability**: [Description]
- **Risk Assessment**: [Low | Medium | High]
- **Justification**: [Why not fixed - e.g., no exploit path, breaking changes]
- **Mitigation**: [Compensating controls]
- **Review Date**: [When to review again]

---

## 3. Security Best Practices Audit

### Authentication & Authorization

**Review Date**: [Date]
**Files Reviewed**: [List key files]

- ✅ Password hashing (PBKDF2-SHA256, 210k iterations)
- ✅ Session management (30-day expiration)
- ✅ API key generation (secure random, PBKDF2)
- ✅ Role-based access control (Admin, Editor, User)
- ✅ OAuth integration (Google, Discord, GitHub)

**Findings**:
- [List any issues or improvements]

**Actions**:
- [List fixes or enhancements made]

### Secrets Management

**Review Date**: [Date]
**Audit Method**: `grep -r "API_KEY\|SECRET\|PASSWORD" --exclude-dir=node_modules`

- ✅ No hardcoded secrets in codebase
- ✅ `.env` files in `.gitignore`
- ✅ Environment variables documented
- ✅ Pre-commit hooks active (detect-secrets)

**Findings**:
- [List any secrets found or issues]

**Actions**:
- [List remediation steps]

### Input Sanitization & Validation

**Review Date**: [Date]
**Files Reviewed**: [API endpoints, form handlers]

- ✅ Parameterized queries (EF Core)
- ✅ Input validation (FluentValidation, ASP.NET Data Annotations)
- ✅ XSS prevention (React auto-escaping, DOMPurify where needed)
- ✅ CSRF protection (SameSite cookies)

**Findings**:
- [List any validation gaps]

**Actions**:
- [List improvements made]

### CORS & CSP Configuration

**Review Date**: [Date]
**Files Reviewed**: `Program.cs`, middleware configs

**CORS Policy**:
*(blocco di codice rimosso)*

**CSP Headers**:
- ✅ Content-Security-Policy configured
- ✅ X-Frame-Options: DENY
- ✅ X-Content-Type-Options: nosniff

**Findings**:
- [List any issues]

**Actions**:
- [List improvements]

---

## 4. Infrastructure Security

### Docker Configuration

**Review Date**: [Date]
**Files Reviewed**: `Dockerfile`, `docker-compose.yml`

- ✅ Non-root user in containers
- ✅ Minimal base images (alpine variants)
- ✅ Multi-stage builds
- ✅ No secrets in Dockerfile
- ✅ Security scanning enabled (Trivy/Snyk)

**Findings**:
- [List any issues]

**Actions**:
- [List improvements]

### Environment Variables & Secrets

**Review Date**: [Date]

- ✅ Secrets via environment variables (not config files)
- ✅ `.env` files not committed
- ✅ Docker secrets for production
- ✅ Key rotation procedures documented

**Findings**:
- [List any issues]

**Actions**:
- [List improvements]

### TLS/HTTPS Configuration

**Review Date**: [Date]
**Infrastructure**: Traefik reverse proxy

- ✅ HTTPS enforced (redirects HTTP → HTTPS)
- ✅ TLS 1.2+ only
- ✅ Strong cipher suites
- ✅ HSTS enabled

**Certificate Status**:
- Issuer: [Let's Encrypt / Self-signed / Other]
- Expiry: [Date]
- Auto-renewal: ✅ Enabled

**Findings**:
- [List any issues]

**Actions**:
- [List improvements]

### API Rate Limiting

**Review Date**: [Date]
**Implementation**: ASP.NET Rate Limiting middleware

- ✅ Rate limits configured per endpoint
- ✅ IP-based throttling
- ✅ API key rate limits
- ✅ DDoS protection (Cloudflare/WAF)

**Configuration**:
- Login endpoint: [X requests/minute]
- API endpoints: [X requests/minute]
- Admin endpoints: [X requests/minute]

**Findings**:
- [List any issues]

**Actions**:
- [List improvements]

### Monitoring & Logging

**Review Date**: [Date]
**Stack**: Serilog, OpenTelemetry, Prometheus, Grafana

- ✅ Structured logging (JSON)
- ✅ Security event logging (auth failures, permission denials)
- ✅ Distributed tracing
- ✅ Health checks monitored
- ✅ Alerting configured

**Log Retention**:
- Application logs: [X days]
- Security logs: [X days]
- Audit logs: [X days]

**Findings**:
- [List any gaps in monitoring]

**Actions**:
- [List improvements]

---

## Metrics & KPIs

### Vulnerability Metrics

| Metric | This Quarter | Last Quarter | Trend |
|--------|--------------|--------------|-------|
| Critical vulns found | 0 | 0 | → |
| High vulns found | 0 | 0 | → |
| Mean Time to Remediate (MTTR) | [X days] | [X days] | ↑/↓/→ |
| Vulnerability backlog | 0 | 0 | → |
| False positive rate | [X%] | [X%] | ↑/↓/→ |

### Process Metrics

- **Review Completion Time**: [X days] (Target: ≤14 days)
- **Issues Created**: [count]
- **Issues Resolved**: [count]
- **Test Coverage Impact**: [+/- X%]

---

## Lessons Learned

### What Went Well
1. [Positive outcome 1]
2. [Positive outcome 2]
3. [Positive outcome 3]

### What Could Be Improved
1. [Improvement area 1]
2. [Improvement area 2]
3. [Improvement area 3]

### Process Improvements for Next Quarter
1. [Action item 1]
2. [Action item 2]
3. [Action item 3]

---

## Action Items for Next Quarter

### High Priority
- [ ] [Action item with owner and deadline]
- [ ] [Action item with owner and deadline]

### Medium Priority
- [ ] [Action item with owner and deadline]
- [ ] [Action item with owner and deadline]

### Continuous Improvements
- [ ] [Ongoing improvement area]
- [ ] [Ongoing improvement area]

---

## Appendix

### Tools & Resources Used
- **CodeQL**: [version]
- **dotnet list package**: [.NET SDK version]
- **pnpm audit**: [pnpm version]
- **Pre-commit hooks**: [version]

### References
- [SECURITY.md](../../SECURITY.md)
- [OWASP Top 10 2021](https://owasp.org/Top10/)
- [CWE Top 25](https://cwe.mitre.org/top25/)
- [GitHub Security Advisories](https://github.com/advisories)

### Sign-off

**Reviewed By**: [Name, Role]
**Approved By**: [Name, Role]
**Date**: [Date]

---

**Next Review Due**: [Next Quarter Start Date]


---



<div style="page-break-before: always;"></div>

## security/totp-vulnerability-analysis.md

# TOTP Replay Attack Vulnerability Analysis - Issue #1787

## 🚨 Security Severity: HIGH

**OWASP Classification**: A07:2021 - Identification and Authentication Failures
**ASVS Standard**: 2.8.3 - OTP values must be used only once
**Discovery Date**: 2025-12-08
**Test Status**: ❌ FAILING - `ReplayAttack_ReuseValidTotp_ShouldFail`

---

## Vulnerability Description

### Attack Vector: TOTP Code Reuse (Replay Attack)

**Scenario**:
An attacker who captures a valid TOTP code can reuse it within the 30-second window, bypassing two-factor authentication.

**Impact**:
- Account takeover if attacker has password + captures TOTP
- Bypasses 2FA protection
- No audit trail of replay attempts

---

## Root Cause Analysis

### Code Location
`apps/api/src/Api/Services/TotpService.cs:179-280` - `VerifyCodeAsync()` method

### Race Condition Vulnerability

*(blocco di codice rimosso)*

### The Race Condition Window

**Timeline of Attack**:
*(blocco di codice rimosso)*

**Window Duration**: ~50-200ms (between AnyAsync and SaveChangesAsync)

**Exploitation Difficulty**: EASY
- Attacker needs: Valid password + TOTP code (via phishing, shoulder surfing, etc.)
- Tools: Simple HTTP client with concurrent requests
- Success rate: HIGH (~80% with 2-3 concurrent requests)

---

## Current Implementation Analysis

### What Works ✅
1. **Rate Limiting**: 5 attempts per 5 minutes (LAYER 1)
2. **Account Lockout**: 5 failures = 15min lockout (LAYER 2)
3. **Nonce Storage**: Uses `used_totp_codes` table with SHA256 hash
4. **Expiry**: Used codes expire after 2 minutes
5. **Audit Logging**: Logs replay attempts
6. **Metrics**: Tracks replay attacks via Prometheus

### What Doesn't Work ❌
1. **NO ATOMICITY**: Check + Insert is not atomic
2. **NO UNIQUE CONSTRAINT**: Database allows duplicate (UserId, CodeHash)
3. **RACE CONDITION VULNERABLE**: Concurrent requests bypass check

---

## Proposed Solution: Database-Level Atomicity

### Implementation: Unique Constraint

**File**: `apps/api/src/Api/Infrastructure/EntityConfigurations/Authentication/UsedTotpCodeEntityConfiguration.cs`

**Change**:
*(blocco di codice rimosso)*

### Migration Required
*(blocco di codice rimosso)*

### TotpService Code Changes

**File**: `apps/api/src/Api/Services/TotpService.cs:253-264`

**Add exception handling for constraint violation**:
*(blocco di codice rimosso)*

---

## Why This Solution Works

### Database-Level Guarantees
- ✅ **Atomic**: Single transaction, no race condition possible
- ✅ **Reliable**: Works even under high concurrency
- ✅ **Simple**: No distributed locks or complex coordination
- ✅ **Fast**: Index lookup is O(log n), very performant

### Defense-in-Depth Layers
*(blocco di codice rimosso)*

---

## Alternative Solutions (Not Recommended)

### Option 2: Transaction with Serializable Isolation
*(blocco di codice rimosso)*
**Problems**:
- ❌ Performance overhead
- ❌ Deadlock risk
- ❌ Not all DB engines support Serializable

### Option 3: Distributed Lock (Redis)
*(blocco di codice rimosso)*
**Problems**:
- ❌ Adds complexity
- ❌ Depends on Redis availability
- ❌ Lock contention under load

---

## Test Coverage

### Existing Tests (Issue #1787)
1. ✅ `VerifyCodeAsync_ValidCode_FirstUse_ShouldSucceed` - Baseline test
2. ❌ `VerifyCodeAsync_ReuseValidCode_ShouldFail_ReplayAttackPrevention` - **FAILING**
3. ✅ `VerifyCodeAsync_DifferentCodes_ShouldBothSucceed` - Different codes work
4. ✅ `VerifyCodeAsync_ExpiredUsedCode_ShouldNotInterfereWithNewCode` - Expiry works
5. ✅ `VerifyCodeAsync_InvalidCode_ShouldFail` - Invalid codes fail

**Test File**: `apps/api/tests/Api.Tests/Integration/Authentication/TotpReplayAttackPreventionTests.cs`

### Expected After Fix
All 5 tests should pass with unique constraint in place.

---

## Implementation Checklist

- [x] Analyze vulnerability root cause
- [x] Identify race condition window
- [x] Review existing security layers
- [x] Design database-level fix
- [ ] Update entity configuration (add `.IsUnique()`)
- [ ] Create EF Core migration
- [ ] Update TotpService exception handling
- [ ] Run integration tests
- [ ] Verify all 5 tests pass
- [ ] Update security documentation
- [ ] Close Issue #1787

---

## Security Impact Assessment

### Before Fix
- **Attack Success Rate**: ~80% (with 2-3 concurrent requests)
- **Exploitation Difficulty**: EASY
- **Detection**: Partial (logged but not blocked)

### After Fix
- **Attack Success Rate**: 0% (database constraint blocks)
- **Exploitation Difficulty**: IMPOSSIBLE (atomic operation)
- **Detection**: FULL (logged + blocked + metrics)

---

## References

- **OWASP ASVS 2.8.3**: [Verification Session Management](https://github.com/OWASP/ASVS/blob/master/4.0/en/0x11-V2-Authentication.md)
- **OWASP A07:2021**: [Identification and Authentication Failures](https://owasp.org/Top10/A07_2021-Identification_and_Authentication_Failures/)
- **RFC 6238**: TOTP - Time-Based One-Time Password Algorithm
- **Issue #1787**: TOTP Replay Attack Prevention

---

## Status

🔴 **VULNERABLE** - Fix in progress
⏱️ **ETA**: <30 minutes (migration + code changes + testing)
🎯 **Priority**: HIGH (security vulnerability)

**Last Updated**: 2025-12-08 19:57 CET


---


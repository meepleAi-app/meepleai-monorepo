# Security Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix all 9 security issues identified in the expert panel security audit (SEC-C1 through SEC-I6).

**Architecture:** Backend (.NET 9) security hardening + frontend CSP/XSS fixes + infrastructure config validation. Each task is independent and produces a single commit. Tasks ordered by criticality: Critical first, then Important, then Low.

**Tech Stack:** .NET 9, ASP.NET Core, Next.js 16, Docker, Traefik, DOMPurify, FluentValidation

**Parent Branch:** `frontend-dev`

---

## File Structure

| File | Action | Responsibility |
|------|--------|----------------|
| `apps/api/src/Api/appsettings.json` | Modify | CSP policy, session cookie default, Qdrant API key, ForwardedHeaders config |
| `apps/api/src/Api/appsettings.CI.json` | Modify | Document CI test credentials |
| `apps/api/src/Api/Program.cs` | Modify | ForwardedHeaders startup validation |
| `apps/api/src/Api/Middleware/SecurityHeadersMiddleware.cs` | Modify | Remove unsafe directives from CSP |
| `apps/web/next.config.js` | Modify | Add security headers for Next.js responses |
| `apps/web/src/components/shared-games/SharedGameDetailModal.tsx` | Modify | Use centralized sanitize.ts |
| `apps/web/src/app/(authenticated)/editor/editor-client.tsx` | Modify | Fix SSR unsanitized fallback |
| `apps/api/src/Api/Routing/CookieHelpers.cs` | Modify | SameSite=Lax for CSRF protection |
| `apps/api/src/Api/Middleware/RateLimitingMiddleware.cs` | Modify | Escalate fail-open to Error level |
| `tests/Api.Tests/Middleware/SecurityHeadersMiddlewareTests.cs` | Modify | Add CSP validation test |

**No changes needed (already safe):**
- `page.tsx` — JSON.stringify of static data, no user input
- `StructuredData.tsx` — own code with escape, not user input
- `PrismHighlighter.tsx` — Prism syntax highlighting, not user input
- `GameOverviewTab.tsx` — already uses `createSafeMarkup()` from centralized lib

---

## Task 1: SEC-C1 — Harden CSP Policy

**Severity:** CRITICAL

**Files:**
- Modify: `apps/api/src/Api/appsettings.json:14`
- Modify: `apps/api/src/Api/Middleware/SecurityHeadersMiddleware.cs:166-175`
- Test: `tests/Api.Tests/Middleware/SecurityHeadersMiddlewareTests.cs`

**What:** Remove the `'unsafe-eval'` CSP directive that allows dynamic JS code execution (eval, Function constructor, setTimeout with strings). Next.js production builds do NOT need it.

- [ ] Step 1: Add test asserting CSP does not contain `unsafe-eval`
- [ ] Step 2: Run test — expect FAIL
- [ ] Step 3: Remove `'unsafe-eval'` from CSP default in `SecurityHeadersMiddleware.cs:168`
- [ ] Step 4: Remove `'unsafe-eval'` from CSP in `appsettings.json:14`
- [ ] Step 5: Run test — expect PASS
- [ ] Step 6: Run full SecurityHeaders test suite
- [ ] Step 7: Commit: `fix(security): remove unsafe-eval from CSP policy (SEC-C1)`

---

## Task 2: SEC-C3 — Validate ForwardedHeaders Proxy Configuration

**Severity:** CRITICAL

**Files:**
- Modify: `apps/api/src/Api/Program.cs:178-230`
- Modify: `apps/api/src/Api/appsettings.json`

**What:** Add startup warning when ForwardedHeaders has no KnownProxies/KnownNetworks in non-dev. Add ForwardedHeaders config section with ForwardLimit=1.

- [ ] Step 1: Add Log.Warning in Program.cs after ForwardedHeaders config for non-dev without proxies
- [ ] Step 2: Add `ForwardedHeaders` section to appsettings.json with ForwardLimit=1
- [ ] Step 3: Build to verify
- [ ] Step 4: Commit: `fix(security): warn on missing ForwardedHeaders proxy config (SEC-C3)`

---

## Task 3: SEC-I5 + SEC-I2 — Fix CSRF and Cookie Secure Default

**Severity:** IMPORTANT

**Files:**
- Modify: `apps/api/src/Api/Routing/CookieHelpers.cs:198-224`
- Modify: `apps/api/src/Api/appsettings.json` (SessionCookie)

**What:** Change production SameSite from None to Lax (CSRF protection). Change Secure default from false to true.

- [ ] Step 1: Update appsettings.json SessionCookie: Secure=true, SameSite=Lax
- [ ] Step 2: Simplify CookieHelpers production branch: default to SameSite.Lax, remove secureForced logic
- [ ] Step 3: Run cookie tests
- [ ] Step 4: Commit: `fix(security): SameSite=Lax + Secure=true default (SEC-I5, SEC-I2)`

---

## Task 4: SEC-I3 — Centralize XSS Sanitization

**Severity:** IMPORTANT

**Files:**
- Modify: `apps/web/src/components/shared-games/SharedGameDetailModal.tsx`
- Modify: `apps/web/src/app/(authenticated)/editor/editor-client.tsx`

**What:** Replace inline DOMPurify usage with centralized `@/lib/security/sanitize` imports. Fix SSR gap in editor that returns raw HTML.

- [ ] Step 1: SharedGameDetailModal — replace `DOMPurify` import with `createSafeMarkup` from centralized lib
- [ ] Step 2: editor-client — replace `require('dompurify')` with `sanitizeHtml` import; strip HTML in SSR fallback
- [ ] Step 3: Run frontend tests
- [ ] Step 4: Run typecheck
- [ ] Step 5: Commit: `fix(security): centralize XSS sanitization (SEC-I3)`

---

## Task 5: SEC-I6 — Enable Qdrant API Key Config

**Severity:** IMPORTANT

**Files:**
- Modify: `apps/api/src/Api/appsettings.json`

**What:** Add `QdrantApiKey` config placeholder. Verify client passes it.

- [ ] Step 1: Add `"QdrantApiKey": "${QDRANT_API_KEY}"` to appsettings.json
- [ ] Step 2: Verify QdrantClient initialization passes API key
- [ ] Step 3: Commit: `fix(security): add Qdrant API key configuration (SEC-I6)`

---

## Task 6: SEC-I4 — Escalate Rate Limit Fail-Open to Error

**Severity:** IMPORTANT

**Files:**
- Modify: `apps/api/src/Api/Middleware/RateLimitingMiddleware.cs:102-109`

**What:** Change LogWarning to LogError so alerting fires when Redis is down.

- [ ] Step 1: Change `_logger.LogWarning` to `_logger.LogError` with Path/Method context
- [ ] Step 2: Run RateLimit tests
- [ ] Step 3: Commit: `fix(security): escalate rate limit fail-open to Error (SEC-I4)`

---

## Task 7: SEC-I1 — Document CI Test Credentials

**Severity:** LOW

**Files:**
- Modify: `apps/api/src/Api/appsettings.CI.json`

**What:** Add comment clarifying CI credentials are intentional for ephemeral test containers.

- [ ] Step 1: Update Comment field in ConnectionStrings
- [ ] Step 2: Commit: `docs(security): document CI test credentials as intentional (SEC-I1)`

---

## Task 8: Add Next.js Security Headers

**Severity:** RECOMMENDED

**Files:**
- Modify: `apps/web/next.config.js`

**What:** Add `headers()` function with X-Frame-Options, nosniff, CSP, Referrer-Policy, Permissions-Policy for all Next.js responses (static assets bypass API middleware).

- [ ] Step 1: Add `async headers()` to nextConfig with security headers array
- [ ] Step 2: Test build: `pnpm build`
- [ ] Step 3: Commit: `feat(security): add security headers to Next.js responses`

---

## Task 9: SEC-C2 — GitGuardian Review (Manual)

**Severity:** CRITICAL | **Type:** Manual

- [ ] Step 1: Open GitGuardian dashboard
- [ ] Step 2: Review each open incident — rotate exposed secrets
- [ ] Step 3: Document findings

URL: `https://dashboard.gitguardian.com/workspace/855584/incidents/secrets`

---

## Summary

| Task | Issue | Severity | Est. |
|------|-------|----------|------|
| 1 | SEC-C1: Harden CSP | CRITICAL | 15m |
| 2 | SEC-C3: ForwardedHeaders | CRITICAL | 15m |
| 3 | SEC-I5+I2: CSRF + Secure | IMPORTANT | 20m |
| 4 | SEC-I3: XSS centralization | IMPORTANT | 25m |
| 5 | SEC-I6: Qdrant API key | IMPORTANT | 10m |
| 6 | SEC-I4: Rate limit alerting | IMPORTANT | 10m |
| 7 | SEC-I1: CI credentials | LOW | 5m |
| 8 | Next.js headers | RECOMMENDED | 15m |
| 9 | SEC-C2: GitGuardian | CRITICAL | 30m manual |

**Total automated:** ~2h | **Branch:** `feature/security-hardening` from `frontend-dev`

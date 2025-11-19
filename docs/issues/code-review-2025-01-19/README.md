# Code Review Issues - Backend-Frontend Interactions (2025-01-19)

Documentazione completa degli issue identificati durante la code review delle interazioni backend-frontend.

## 📋 Overview

**Data Review**: 2025-01-19
**Reviewer**: Claude Code Assistant
**Contesto**: Code Review - Backend-Frontend Interactions
**Totale Issue**: 8
**Tempo Stimato**: 39-52 ore (5 sprints part-time)

## 🎯 Issue Overview

| # | Titolo | Priorità | Area | Effort | Sprint | Ore |
|---|--------|----------|------|--------|--------|-----|
| 01 | [SecurityHeadersMiddleware](01-security-headers-middleware.md) | 🔴 Critical | Backend | Small | 1 | 4-6h |
| 02 | [CORS Whitelist Headers](02-cors-whitelist-headers.md) | 🔴 Critical | Backend | Small | 1 | 2-3h |
| 03 | [FluentValidation Authentication](03-fluentvalidation-authentication.md) | 🟡 High | Backend | Medium | 2 | 5-6h |
| 04 | [NSwag Code Generation](04-nswag-code-generation.md) | 🟡 High | Both | Large | 3 | 8-10h |
| 05 | [Streaming Hooks Consolidation](05-streaming-hooks-consolidation.md) | 🟢 Medium | Frontend | Medium | 4 | 6-8h |
| 06 | [Rate Limiting UX](06-rate-limiting-ux.md) | 🟢 Medium | Frontend | Small | 4 | 4-6h |
| 07 | [Retry Logic Exponential Backoff](07-retry-logic-exponential-backoff.md) | 🟢 Medium | Frontend | Medium | 5 | 6-8h |
| 08 | [Request Deduplication](08-request-deduplication.md) | 🟢 Medium | Frontend | Medium | 5 | 4-6h |

**Totale Ore Stimate**: 39-52 ore

## 🔴 Sprint 1: Criticità Sicurezza (Settimana 1-2)

### Issue #01: SecurityHeadersMiddleware
**File**: [01-security-headers-middleware.md](01-security-headers-middleware.md)

**Problema**: Nessun HTTP security header configurato (CSP, HSTS, X-Frame-Options)

**Soluzione**:
- Creare `SecurityHeadersMiddleware`
- Implementare 7 security headers
- Protezione da XSS, clickjacking, MIME sniffing

**Impact**: Critical - Protegge da vulnerabilità OWASP Top 10

**Related ADR**: [ADR-010](../../01-architecture/adr/adr-010-security-headers-middleware.md)

---

### Issue #02: CORS Whitelist Headers
**File**: [02-cors-whitelist-headers.md](02-cors-whitelist-headers.md)

**Problema**: `AllowAnyHeader()` permette header arbitrari, vettore d'attacco

**Soluzione**:
- Whitelist esplicita: Content-Type, Authorization, X-Correlation-ID, X-API-Key
- Rimuovere `AllowAnyHeader()`

**Impact**: High - Riduce superficie d'attacco CORS

**Related ADR**: [ADR-011](../../01-architecture/adr/adr-011-cors-whitelist-headers.md)

---

## 🟡 Sprint 2-3: Validazione e Type Safety (Settimana 3-6)

### Issue #03: FluentValidation Authentication
**File**: [03-fluentvalidation-authentication.md](03-fluentvalidation-authentication.md)

**Problema**: Validazione inconsistente, logica sparsa negli endpoint

**Soluzione**:
- MediatR ValidationBehavior
- FluentValidation per tutti i Command/Query
- 5 validators (Login, Register, ChangePassword, Enable2FA, ResetPassword)

**Impact**: High - Validazione centralizzata, messaggi errore migliori

**Related ADR**: [ADR-012](../../01-architecture/adr/adr-012-fluentvalidation-cqrs.md)

---

### Issue #04: NSwag TypeScript Code Generation
**File**: [04-nswag-code-generation.md](04-nswag-code-generation.md)

**Problema**: Tipi TypeScript manuali possono divergere da C# DTOs

**Soluzione**:
- NSwag + @hey-api/openapi-ts per auto-generazione
- TypeScript interfaces + Zod schemas da OpenAPI
- CI/CD integration

**Impact**: High - Elimina type drift, zero manutenzione manuale

**Related ADR**: [ADR-013](../../01-architecture/adr/adr-013-nswag-typescript-generation.md)

---

## 🟢 Sprint 4-5: UX e Performance (Settimana 7-10)

### Issue #05: Streaming Hooks Consolidation
**File**: [05-streaming-hooks-consolidation.md](05-streaming-hooks-consolidation.md)

**Problema**: Due hook separati per streaming (useChatStreaming + useChatStream)

**Soluzione**:
- Hook unificato con flag mock/real
- Interfaccia consistente
- Environment-based switching

**Impact**: Medium - Migliora manutenibilità, riduce duplicazione

---

### Issue #06: Rate Limiting UX
**File**: [06-rate-limiting-ux.md](06-rate-limiting-ux.md)

**Problema**: Errore 429 generico, utente non sa quanto aspettare

**Soluzione**:
- Parse `Retry-After` header
- Countdown timer UI
- Auto-re-enable button

**Impact**: Medium - Migliora UX, riduce frustrazione utente

---

### Issue #07: Retry Logic Exponential Backoff
**File**: [07-retry-logic-exponential-backoff.md](07-retry-logic-exponential-backoff.md)

**Problema**: Nessun retry automatico per errori transienti (500, 502, 503)

**Soluzione**:
- Exponential backoff con jitter
- Max 3 retries
- Prometheus metrics

**Impact**: Medium - Migliora resilienza applicazione

---

### Issue #08: Request Deduplication
**File**: [08-request-deduplication.md](08-request-deduplication.md)

**Problema**: Richieste identiche simultanee causano chiamate duplicate

**Soluzione**:
- RequestCache con TTL
- Cache key: method + URL + body hash
- LRU eviction

**Impact**: Medium - Riduce carico backend 20-30%

---

## 📊 Timeline Implementazione

```
Sprint 1 (Week 1-2):   🔴 Security          Issues #01, #02    (6-9 ore)
Sprint 2 (Week 3-4):   🟡 Validation        Issue #03          (5-6 ore)
Sprint 3 (Week 5-6):   🟡 Code Generation   Issue #04          (8-10 ore)
Sprint 4 (Week 7-8):   🟢 UX                Issues #05, #06    (10-14 ore)
Sprint 5 (Week 9-10):  🟢 Performance       Issues #07, #08    (10-14 ore)

Total: 39-52 ore (10 settimane part-time)
```

## 🔗 Related Documentation

### Architecture Decision Records
- [ADR-010: Security Headers Middleware](../../01-architecture/adr/adr-010-security-headers-middleware.md)
- [ADR-011: CORS Whitelist Headers](../../01-architecture/adr/adr-011-cors-whitelist-headers.md)
- [ADR-012: FluentValidation CQRS](../../01-architecture/adr/adr-012-fluentvalidation-cqrs.md)
- [ADR-013: NSwag TypeScript Generation](../../01-architecture/adr/adr-013-nswag-typescript-generation.md)

### GitHub Templates
- **Issue Templates**: `.github/ISSUES_TEMPLATES_CR/`
- **Project Setup**: `.github/ISSUES_TEMPLATES_CR/create-project.md`
- **Manual Creation**: `.github/ISSUES_TEMPLATES_CR/MANUAL_CREATION.md`

### Code Review Report
- **Detailed Report**: Vedere conversazione precedente con Claude Code
- **Summary**: 8 raccomandazioni (3 critical, 2 high, 3 medium priority)

## 📝 Come Creare gli Issue su GitHub

### Opzione 1: Manuale (Web UI)

Per ogni documento in questa cartella:

1. Vai a: https://github.com/DegrassiAaron/meepleai-monorepo/issues/new
2. **Titolo**: Copia da sezione "Title" in cima al documento
3. **Body**: Copia l'intero contenuto del documento (Ctrl+A → Ctrl+C)
4. **Labels**: Applica le label indicate nel documento
5. **Submit**: Clicca "Submit new issue"

### Opzione 2: GitHub CLI (Locale)

Se hai `gh` installato:

```bash
# Issue #1
gh issue create \
  --title "🔐 [Security] Implement SecurityHeadersMiddleware" \
  --body-file docs/issues/code-review-2025-01-19/01-security-headers-middleware.md \
  --label "priority: critical,type: security,area: backend,effort: small,sprint: 1"

# Ripeti per tutti gli 8 issue...
```

Vedi: `.github/ISSUES_TEMPLATES_CR/create-issues-commands.txt` per tutti i comandi.

## 🏷️ Label Required

Prima di creare gli issue, assicurati che queste label esistano:

**Priority**:
- `priority: critical` 🔴
- `priority: high` 🟡
- `priority: medium` 🟢

**Type**:
- `type: security` 🔒
- `type: enhancement` ✨
- `type: refactor` 🔧

**Area**:
- `area: backend` 🎯
- `area: frontend` 🎨

**Effort**:
- `effort: small` (1-3 giorni)
- `effort: medium` (3-7 giorni)
- `effort: large` (1-2 settimane)

**Sprint**:
- `sprint: 1` ... `sprint: 5`

**Additional**:
- `ux`, `resilience`, `performance`

## 📈 Progress Tracking

Dopo aver creato gli issue:

1. **Crea GitHub Project**: Segui `.github/ISSUES_TEMPLATES_CR/create-project.md`
2. **Aggiungi issue al project**
3. **Track progress**: Sposta issue tra colonne (Backlog → In Progress → In Review → Done)
4. **Weekly review**: Ogni venerdì, aggiorna stato issue

## ✅ Checklist

- [ ] **Review documenti**: Leggi tutti gli 8 issue documents
- [ ] **Verifica label**: Crea label mancanti su GitHub
- [ ] **Crea issue #1-2**: Sprint 1 (Critical)
- [ ] **Crea issue #3-4**: Sprint 2-3 (High)
- [ ] **Crea issue #5-8**: Sprint 4-5 (Medium)
- [ ] **GitHub Project**: Crea project per tracking
- [ ] **ADR Review**: Leggi i 4 ADR correlati
- [ ] **Sprint Planning**: Pianifica Sprint 1 (Issue #1-2)

## 📊 Metriche

**Per Sprint**:
- Sprint 1: 2 issue, 6-9 ore
- Sprint 2: 1 issue, 5-6 ore
- Sprint 3: 1 issue, 8-10 ore
- Sprint 4: 2 issue, 10-14 ore
- Sprint 5: 2 issue, 10-14 ore

**Per Area**:
- Backend: 3 issue (16-21 ore)
- Frontend: 4 issue (20-28 ore)
- Both: 1 issue (8-10 ore)

**Per Priorità**:
- Critical: 2 issue (6-9 ore)
- High: 2 issue (13-16 ore)
- Medium: 4 issue (20-28 ore)

## 🔍 Quick Reference

**File nella cartella**:
```
docs/issues/code-review-2025-01-19/
├── README.md (questo file)
├── 01-security-headers-middleware.md
├── 02-cors-whitelist-headers.md
├── 03-fluentvalidation-authentication.md
├── 04-nswag-code-generation.md
├── 05-streaming-hooks-consolidation.md
├── 06-rate-limiting-ux.md
├── 07-retry-logic-exponential-backoff.md
└── 08-request-deduplication.md
```

**Collegamenti rapidi**:
- GitHub Issues: https://github.com/DegrassiAaron/meepleai-monorepo/issues
- GitHub Projects: https://github.com/DegrassiAaron/meepleai-monorepo/projects
- ADR: `docs/01-architecture/adr/`

---

**Creato**: 2025-01-19
**Ultima Modifica**: 2025-01-19
**Fonte**: Code Review - Backend-Frontend Interactions
**Total Issues**: 8
**Status**: Ready for Implementation

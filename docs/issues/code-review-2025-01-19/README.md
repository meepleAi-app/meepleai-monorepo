# Code Review Issues - Backend (2025-01-19)

Documentazione degli issue backend identificati durante la code review delle interazioni backend-frontend.

**Nota**: Gli issue frontend sono stati rimossi e documentati altrove.

## 📋 Overview

**Data Review**: 2025-01-19
**Reviewer**: Claude Code Assistant
**Contesto**: Code Review - Backend Issues
**Totale Issue**: 3
**Tempo Stimato**: 11-15 ore (2 sprints part-time)

## 🎯 Issue Overview

| # | Titolo | Priorità | Area | Effort | Sprint | Ore |
|---|--------|----------|------|--------|--------|-----|
| 01 | [SecurityHeadersMiddleware](01-security-headers-middleware.md) | 🔴 Critical | Backend | Small | 1 | 4-6h |
| 02 | [CORS Whitelist Headers](02-cors-whitelist-headers.md) | 🔴 Critical | Backend | Small | 1 | 2-3h |
| 03 | [FluentValidation Authentication](03-fluentvalidation-authentication.md) | 🟡 High | Backend | Medium | 2 | 5-6h |

**Totale Ore Stimate**: 11-15 ore

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

## 🟡 Sprint 2: Validazione (Settimana 3-4)

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

## 📊 Timeline Implementazione

```
Sprint 1 (Week 1-2):   🔴 Security       Issues #01, #02    (6-9 ore)
Sprint 2 (Week 3-4):   🟡 Validation     Issue #03          (5-6 ore)

Total: 11-15 ore (4 settimane part-time)
```

## 🔗 Related Documentation

### Architecture Decision Records
- [ADR-010: Security Headers Middleware](../../01-architecture/adr/adr-010-security-headers-middleware.md)
- [ADR-011: CORS Whitelist Headers](../../01-architecture/adr/adr-011-cors-whitelist-headers.md)
- [ADR-012: FluentValidation CQRS](../../01-architecture/adr/adr-012-fluentvalidation-cqrs.md)

### GitHub Templates
- **Issue Templates**: `.github/ISSUES_TEMPLATES_CR/`
- **Project Setup**: `.github/ISSUES_TEMPLATES_CR/create-project.md`
- **Manual Creation**: `.github/ISSUES_TEMPLATES_CR/MANUAL_CREATION.md`

### Code Review Report
- **Detailed Report**: Vedere conversazione precedente con Claude Code
- **Summary**: 3 raccomandazioni backend (2 critical, 1 high priority)

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

# Ripeti per tutti e 3 gli issue...
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

- [ ] **Review documenti**: Leggi tutti e 3 gli issue documents
- [ ] **Verifica label**: Crea label mancanti su GitHub
- [ ] **Crea issue #1-2**: Sprint 1 (Critical)
- [ ] **Crea issue #3**: Sprint 2 (High)
- [ ] **GitHub Project**: Crea project per tracking
- [ ] **ADR Review**: Leggi i 3 ADR correlati
- [ ] **Sprint Planning**: Pianifica Sprint 1 (Issue #1-2)

## 📊 Metriche

**Per Sprint**:
- Sprint 1: 2 issue, 6-9 ore
- Sprint 2: 1 issue, 5-6 ore

**Per Area**:
- Backend: 3 issue (11-15 ore)

**Per Priorità**:
- Critical: 2 issue (6-9 ore)
- High: 1 issue (5-6 ore)

## 🔍 Quick Reference

**File nella cartella**:
```
docs/issues/code-review-2025-01-19/
├── README.md (questo file)
├── 01-security-headers-middleware.md
├── 02-cors-whitelist-headers.md
└── 03-fluentvalidation-authentication.md
```

**Collegamenti rapidi**:
- GitHub Issues: https://github.com/DegrassiAaron/meepleai-monorepo/issues
- GitHub Projects: https://github.com/DegrassiAaron/meepleai-monorepo/projects
- ADR: `docs/01-architecture/adr/`

---

**Creato**: 2025-01-19
**Ultima Modifica**: 2025-11-22
**Fonte**: Code Review - Backend Issues
**Total Issues**: 3
**Status**: Ready for Implementation

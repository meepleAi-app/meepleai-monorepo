# MeepleAI Gap Analysis: Codebase vs Documentazione

**Data Originale**: 2026-01-26
**Ultimo Aggiornamento**: 2026-01-27
**Analisi**: Confronto sistematico tra documentazione e implementazione effettiva

---

## Executive Summary

> **AGGIORNAMENTO 2026-01-27**: L'analisi originale conteneva dati errati sui bounded context CQRS. La verifica ha dimostrato che SharedGameCatalog e Authentication sono **100% implementati**.

| Categoria | Severity | Gap Identificati | Status |
|-----------|----------|------------------|--------|
| ~~**CQRS Pattern Violations**~~ | ~~CRITICO~~ | ~~2 bounded contexts~~ | **RISOLTO** |
| **Backend Missing Features** | ALTO | 5 funzionalita | DA FARE |
| **Frontend Placeholders** | MEDIO | 3 componenti | DA FARE |
| **API Endpoints Missing** | MEDIO | 5 endpoint | DA FARE |
| **User Flows Incomplete** | BASSO | 4 flussi | DA FARE |

---

## 1. ~~GAP CRITICI: Pattern CQRS Violati~~ RISOLTO

### 1.1 SharedGameCatalog - COMPLETAMENTE IMPLEMENTATO

**Severity**: ~~CRITICO~~ **RISOLTO**
**Status**: 69/69 handlers implementati (100%)

```
SharedGameCatalog/
├── Application/
│   ├── Commands/     → 41 commands definiti, 41 handlers
│   ├── Queries/      → 28 queries definite, 28 handlers
│   └── EventHandlers → 4 event handlers aggiuntivi
└── Tests/            → 124 file di test
```

**Verifica (2026-01-27)**:
```bash
find SharedGameCatalog -name "*Command.cs" ! -name "*Handler*" ! -name "*Validator*" | wc -l  # 41
find SharedGameCatalog -name "*CommandHandler.cs" | wc -l  # 41
find SharedGameCatalog -name "*Query.cs" ! -name "*Handler*" | wc -l  # 28
find SharedGameCatalog -name "*QueryHandler.cs" | wc -l  # 28
```

**Note**: L'analisi originale riportava erroneamente "193 operazioni, 0 handlers". Gli handlers sono organizzati in sottocartelle (es. `Commands/StartReview/StartReviewCommandHandler.cs`), non in una cartella `Handlers/` piatta.

**Issues Correlate**: #3067, #3068 (chiuse come già completate)

---

### 1.2 Authentication - COMPLETAMENTE IMPLEMENTATO

**Severity**: ~~ALTO~~ **RISOLTO**
**Status**: 53/53 handlers implementati (100%)

```
Authentication/
├── Application/
│   ├── Commands/     → 34 commands definiti, 34 handlers
│   ├── Queries/      → 19 queries definite, 19 handlers
│   └── EventHandlers → handlers per domain events
```

**Verifica (2026-01-27)**:
```bash
find Authentication -name "*Command.cs" ! -name "*Handler*" ! -name "*Validator*" | wc -l  # 34
find Authentication -name "*CommandHandler.cs" | wc -l  # 34
find Authentication -name "*Query.cs" ! -name "*Handler*" | wc -l  # 19
find Authentication -name "*QueryHandler.cs" | wc -l  # 19
```

**Note**: L'analisi originale riportava erroneamente "104 operazioni, solo 2 handlers".

**Issues Correlate**: #3069 (chiusa come già completata)

---

## 2. GAP Backend: Funzionalita Documentate Non Implementate

### 2.1 Session Limits (Documentato in gap-analysis.md)

**Documentato**: Limiti sessioni per tier (Free: 3, Normal: 10, Premium: unlimited)
**Implementato**: NO - Infrastruttura esiste ma enforcement mancante

**File Mancanti**:
- `SessionQuotaService.cs`
- `CheckSessionQuotaCommand.cs`
- `SessionQuotaDto.cs`

**Endpoint Mancanti**:
- `GET /api/v1/admin/system/session-limits`
- `PUT /api/v1/admin/system/session-limits`

**Issue**: #3070

---

### 2.2 PDF Upload Limits Admin UI

**Documentato**: Configurazione limiti PDF via admin UI
**Implementato**: Solo via database, nessun endpoint admin

**Endpoint Mancanti**:
- `GET /api/v1/admin/system/pdf-upload-limits`
- `PUT /api/v1/admin/system/pdf-upload-limits`

**Handler Mancante**:
- `UpdatePdfUploadLimitsCommandHandler.cs`

**Issue**: #3072

---

### 2.3 Email Verification

**Documentato**: Verifica email obbligatoria post-registrazione
**Implementato**: NO - Utenti possono accedere senza verifica

**Componenti Mancanti**:
- `EmailVerificationService.cs`
- Tabella `email_verifications`
- `VerifyEmailCommand.cs`
- `ResendVerificationCommand.cs`

**Issue**: #3071

---

### 2.4 Feature Flags Tier-Based

**Documentato**: Feature flags per subscription tier
**Implementato**: Solo role-based (User/Editor/Admin)

**Modifica Richiesta**:
```csharp
// Attuale
Features.RAG.Admin = true

// Richiesto
Features.RAG.Free = false
Features.RAG.Normal = true
Features.RAG.Premium = true
```

**Issue**: #3073

---

### 2.5 AI Token Usage Tracking

**Documentato**: Tracking consumo token AI per utente
**Implementato**: NO

**Tabella Mancante**:
```sql
CREATE TABLE ai_usage_logs (
    id UUID PRIMARY KEY,
    user_id UUID REFERENCES users(id),
    model VARCHAR(100),
    input_tokens INT,
    output_tokens INT,
    cost DECIMAL(10,6),
    created_at TIMESTAMP
);
```

**Issue**: #3074

---

## 3. GAP Frontend: Componenti Placeholder/Mancanti

### 3.1 Two-Factor Authentication UI

**Documentato**: Setup e verifica 2FA
**Implementato**: PLACEHOLDER - Solo Storybook stories, componenti non codificati

**File Esistenti** (solo stories):
- `TwoFactorSetup.stories.tsx`
- `TwoFactorVerification.stories.tsx`

**Componenti Mancanti**:
- `TwoFactorSetup.tsx` (implementazione reale)
- `TwoFactorVerification.tsx` (implementazione reale)
- Integrazione con endpoint `/2fa/*`

**Issue**: #3077

---

### 3.2 Session Quota UI

**Documentato**: Visualizzazione quota sessioni
**Implementato**: NO

**Componenti Mancanti**:
- `SessionQuotaBar.tsx`
- Integrazione in `/sessions` page
- Widget in dashboard

**Issue**: #3075

---

### 3.3 PDF Limits Admin Config

**Documentato**: Form configurazione limiti PDF
**Implementato**: NO

**Componenti Mancanti**:
- `PdfLimitsConfig.tsx`
- Integrazione in `/admin/configuration`

**Issue**: #3078

---

### 3.4 Feature Flags Tier UI

**Documentato**: UI per gestione feature flags per tier
**Implementato**: NO

**Issue**: #3079

---

## 4. GAP API Endpoints

### Endpoint Documentati ma Non Implementati

| Endpoint | Metodo | Bounded Context | Status | Issue |
|----------|--------|-----------------|--------|-------|
| `/admin/system/session-limits` | GET/PUT | SystemConfiguration | MANCANTE | #3070 |
| `/admin/system/pdf-upload-limits` | GET/PUT | SystemConfiguration | MANCANTE | #3072 |
| `/admin/users/{id}/usage` | GET | Administration | MANCANTE | #3074 |
| `/auth/email/verify` | POST | Authentication | MANCANTE | #3071 |
| `/auth/email/resend` | POST | Authentication | MANCANTE | #3071 |

---

## 5. GAP User Flows

### Flussi Documentati con Implementazione Incompleta

| User Flow | Completezza | Gap |
|-----------|-------------|-----|
| Authentication | 95% | Account lockout, email verification |
| Game Discovery | 90% | Autocomplete search, similar games |
| AI Chat | 90% | Voice input, message feedback |
| Game Sessions | 85% | Session limits enforcement, invites |

---

## 6. Prioritizzazione Interventi

### ~~CRITICO (Sprint Immediato)~~ COMPLETATO

~~1. **SharedGameCatalog Handlers** - Implementare i 193 handlers mancanti~~
   - **Status**: COMPLETATO (2026-01-27)
   - Issue #3067, #3068 chiuse

~~2. **Authentication Handlers** - Migrare a pattern CQRS~~
   - **Status**: COMPLETATO (2026-01-27)
   - Issue #3069 chiusa

### ALTO (Priorita Attuale)

1. **Session Limits** (#3070, #3075)
   - Backend: SessionQuotaService + endpoints
   - Frontend: SessionQuotaBar
   - Effort: 1 sprint

2. **Email Verification** (#3071, #3076)
   - Backend: Service + endpoints
   - Frontend: Verification flow
   - Effort: 1 sprint

3. **2FA Implementation** (#3077)
   - Convertire placeholder in componenti reali
   - Effort: 0.5 sprint

### MEDIO (Q1-Q2 2026)

4. **PDF Limits Admin UI** (#3072, #3078)
   - Endpoints + frontend form
   - Effort: 0.5 sprint

5. **Feature Flags Tier-Based** (#3073, #3079)
   - Estendere FeatureFlagService
   - Effort: 1 sprint

6. **AI Token Tracking** (#3074, #3080)
   - Database + service + dashboard
   - Effort: 1 sprint

### BASSO (Q3-Q4 2026)

7. Advanced Chat Features (voice, feedback)
8. Collaboration Features (session invites)
9. Admin Enhancements (impersonation, tracing)

---

## 7. Metriche di Completamento

### Stato Attuale (Aggiornato 2026-01-27)

| Area | Documentato | Implementato | Gap |
|------|-------------|--------------|-----|
| Bounded Contexts (CQRS) | 10 | **10 completi** | **0** |
| API Endpoints | ~170 | ~165 | 5 mancanti |
| Frontend Components | ~150 | ~139 | 11 placeholder |
| User Flows | 100% | 85% | 15% incompleti |

### Target Post-Interventi

| Area | Target | Timeline |
|------|--------|----------|
| ~~CQRS Compliance~~ | ~~100%~~ | **COMPLETATO** |
| API Endpoints | 100% | Q1 2026 |
| Frontend Components | 95% | Q2 2026 |
| User Flows | 95% | Q2 2026 |

---

## 8. Raccomandazioni

### Immediate Actions

1. ~~**Creare Epic**: "CQRS Pattern Compliance" per SharedGameCatalog e Authentication~~ FATTO
2. **Prioritizzare**: Session limits e email verification per sicurezza
3. **Convertire**: 2FA placeholder in implementazione reale

### Process Improvements

1. **Pre-commit Check**: Verificare che ogni Command/Query abbia handler
2. **Documentation Sync**: Aggiornare docs quando si implementa
3. **Gap Review**: Eseguire questa analisi trimestralmente
4. **Verifica Dati**: Prima di creare issue, verificare stato reale del codice

### Lessons Learned (2026-01-27)

1. **Struttura Cartelle**: Gli handlers possono essere in sottocartelle, non solo in `Handlers/`
2. **Conteggio Automatico**: Usare script di verifica per contare handlers
3. **Validazione Pre-Issue**: Verificare sempre lo stato reale prima di aprire issue

---

## 9. Issue Tracking (Epic #3066)

### Completate (3/14)
- [x] #3067 - SharedGameCatalog Core CQRS (chiusa 2026-01-27)
- [x] #3068 - SharedGameCatalog Share Flow (chiusa 2026-01-27)
- [x] #3069 - Authentication CQRS Migration (chiusa 2026-01-27)

### Da Fare - Backend (5)
- [ ] #3070 - Session Limits Backend
- [ ] #3071 - Email Verification Backend
- [ ] #3072 - PDF Limits Admin API
- [ ] #3073 - Feature Flags Tier-Based
- [ ] #3074 - AI Token Usage Tracking

### Da Fare - Frontend (6)
- [ ] #3075 - Session Quota UI
- [ ] #3076 - Email Verification Flow
- [ ] #3077 - 2FA UI Components
- [ ] #3078 - PDF Limits Admin UI
- [ ] #3079 - Feature Flags Tier UI
- [ ] #3080 - AI Usage Dashboard

---

## Appendice: File di Riferimento

### Documentazione Analizzata
- `CLAUDE.md` - Development guide
- `docs/11-user-flows/gap-analysis.md` - User flow gaps
- `docs/09-bounded-contexts/*.md` - BC documentation

### Codebase Analizzato
- `apps/api/src/Api/BoundedContexts/` - 10 bounded contexts
- `apps/api/src/Api/Routing/*.cs` - 44 endpoint files
- `apps/web/src/` - 1.391 frontend files

### Verifica CQRS (2026-01-27)
```bash
# SharedGameCatalog
Commands: 41 | CommandHandlers: 41 | Queries: 28 | QueryHandlers: 28
Total: 69 operations, 69 handlers (100%)

# Authentication
Commands: 34 | CommandHandlers: 34 | Queries: 19 | QueryHandlers: 19
Total: 53 operations, 53 handlers (100%)
```

---

**Autore**: Claude Code Analysis
**Versione**: 2.0 (aggiornato 2026-01-27)
**Changelog**:
- v2.0 (2026-01-27): Corretto stato CQRS, chiuse issue #3067/#3068/#3069, aggiornate metriche
- v1.0 (2026-01-26): Analisi iniziale (con dati errati su CQRS)

**Prossima Review**: 2026-04-27

# MeepleAI Gap Analysis: Codebase vs Documentazione

**Data**: 2026-01-26
**Analisi**: Confronto sistematico tra documentazione e implementazione effettiva

---

## Executive Summary

L'analisi ha identificato **gap critici** tra documentazione e implementazione:

| Categoria | Severity | Gap Identificati |
|-----------|----------|------------------|
| **CQRS Pattern Violations** | CRITICO | 2 bounded contexts |
| **Backend Missing Features** | ALTO | 5 funzionalita |
| **Frontend Placeholders** | MEDIO | 3 componenti |
| **API Endpoints Missing** | MEDIO | 5 endpoint |
| **User Flows Incomplete** | BASSO | 4 flussi |

---

## 1. GAP CRITICI: Pattern CQRS Violati

### 1.1 SharedGameCatalog - HANDLERS COMPLETAMENTE VUOTI

**Severity**: CRITICO
**Impatto**: 193 operazioni CQRS definite ma non implementate

```
SharedGameCatalog/
├── Application/
│   ├── Commands/     → 123 commands definiti
│   ├── Queries/      → 70 queries definite
│   └── Handlers/     → 0 HANDLERS (folder vuota!)
```

**Problema**: Il bounded context SharedGameCatalog ha Commands e Queries definiti ma **NESSUN handler implementato**. La logica probabilmente usa servizi diretti, violando il pattern CQRS documentato in CLAUDE.md.

**Azione Richiesta**:
1. Implementare tutti i 193 handlers mancanti
2. Migrare da servizi diretti a MediatR pattern
3. Aggiungere validators FluentValidation

**Stima**: Epic con 10-15 issues

---

### 1.2 Authentication - Handlers Sotto-implementati

**Severity**: ALTO
**Impatto**: 104 operazioni CQRS, solo 2 handlers

```
Authentication/
├── Application/
│   ├── Commands/     → 67 commands definiti
│   ├── Queries/      → 37 queries definite
│   └── Handlers/     → SOLO 2 handlers (BulkExport/BulkImport)
```

**Problema**: La maggior parte della logica di autenticazione bypassa MediatR, violando la regola "ZERO direct service injection" documentata.

**Azione Richiesta**:
1. Verificare come gli endpoint auth eseguono i comandi
2. Migrare logica da servizi diretti a handlers
3. Mantenere consistenza con altri bounded contexts

**Stima**: 8-12 issues

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

---

### 2.2 PDF Upload Limits Admin UI

**Documentato**: Configurazione limiti PDF via admin UI
**Implementato**: Solo via database, nessun endpoint admin

**Endpoint Mancanti**:
- `GET /api/v1/admin/system/pdf-upload-limits`
- `PUT /api/v1/admin/system/pdf-upload-limits`

**Handler Mancante**:
- `UpdatePdfUploadLimitsCommandHandler.cs`

---

### 2.3 Email Verification

**Documentato**: Verifica email obbligatoria post-registrazione
**Implementato**: NO - Utenti possono accedere senza verifica

**Componenti Mancanti**:
- `EmailVerificationService.cs`
- Tabella `email_verifications`
- `VerifyEmailCommand.cs`
- `ResendVerificationCommand.cs`

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

---

### 3.2 Session Quota UI

**Documentato**: Visualizzazione quota sessioni
**Implementato**: NO

**Componenti Mancanti**:
- `SessionQuotaBar.tsx`
- Integrazione in `/sessions` page
- Widget in dashboard

---

### 3.3 PDF Limits Admin Config

**Documentato**: Form configurazione limiti PDF
**Implementato**: NO

**Componenti Mancanti**:
- `PdfLimitsConfig.tsx`
- Integrazione in `/admin/configuration`

---

### 3.4 Streaming Chat Improvements

**Documentato**: Phase 4 chat features
**Implementato**: Parziale

**Funzionalita Mancanti**:
- Stop button per interrompere risposta streaming
- Typing indicator avanzato
- Export thread button funzionante

---

## 4. GAP API Endpoints

### Endpoint Documentati ma Non Implementati

| Endpoint | Metodo | Bounded Context | Status |
|----------|--------|-----------------|--------|
| `/admin/system/session-limits` | GET/PUT | SystemConfiguration | MANCANTE |
| `/admin/system/pdf-upload-limits` | GET/PUT | SystemConfiguration | MANCANTE |
| `/admin/users/{id}/usage` | GET | Administration | MANCANTE |
| `/auth/email/verify` | POST | Authentication | MANCANTE |
| `/auth/email/resend` | POST | Authentication | MANCANTE |

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

### CRITICO (Sprint Immediato)

1. **SharedGameCatalog Handlers** - Implementare i 193 handlers mancanti
   - Severity: CRITICO
   - Effort: 2-3 sprint
   - Issue: Da creare epic

2. **Authentication Handlers** - Migrare a pattern CQRS
   - Severity: ALTO
   - Effort: 1-2 sprint

### ALTO (Q1 2026)

3. **Session Limits**
   - Backend: SessionQuotaService + endpoints
   - Frontend: SessionQuotaBar
   - Effort: 1 sprint

4. **Email Verification**
   - Backend: Service + endpoints
   - Frontend: Verification flow
   - Effort: 1 sprint

5. **2FA Implementation**
   - Convertire placeholder in componenti reali
   - Effort: 0.5 sprint

### MEDIO (Q2 2026)

6. **PDF Limits Admin UI**
   - Endpoints + frontend form
   - Effort: 0.5 sprint

7. **Feature Flags Tier-Based**
   - Estendere FeatureFlagService
   - Effort: 1 sprint

8. **AI Token Tracking**
   - Database + service + dashboard
   - Effort: 1 sprint

### BASSO (Q3-Q4 2026)

9. Advanced Chat Features (voice, feedback)
10. Collaboration Features (session invites)
11. Admin Enhancements (impersonation, tracing)

---

## 7. Metriche di Completamento

### Stato Attuale

| Area | Documentato | Implementato | Gap |
|------|-------------|--------------|-----|
| Bounded Contexts (CQRS) | 10 | 8 completi | 2 critici |
| API Endpoints | ~170 | ~165 | 5 mancanti |
| Frontend Components | ~150 | ~139 | 11 placeholder |
| User Flows | 100% | 85% | 15% incompleti |

### Target Post-Interventi

| Area | Target | Timeline |
|------|--------|----------|
| CQRS Compliance | 100% | Q1 2026 |
| API Endpoints | 100% | Q1 2026 |
| Frontend Components | 95% | Q2 2026 |
| User Flows | 95% | Q2 2026 |

---

## 8. Raccomandazioni

### Immediate Actions

1. **Creare Epic**: "CQRS Pattern Compliance" per SharedGameCatalog e Authentication
2. **Prioritizzare**: Session limits e email verification per sicurezza
3. **Convertire**: 2FA placeholder in implementazione reale

### Process Improvements

1. **Pre-commit Check**: Verificare che ogni Command/Query abbia handler
2. **Documentation Sync**: Aggiornare docs quando si implementa
3. **Gap Review**: Eseguire questa analisi trimestralmente

### Technical Debt

1. **SharedGameCatalog**: Refactoring urgente necessario
2. **Authentication**: Debt medio, migrare gradualmente
3. **Frontend Placeholders**: Convertire o rimuovere

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

---

**Autore**: Claude Code Analysis
**Versione**: 1.0
**Prossima Review**: 2026-04-26

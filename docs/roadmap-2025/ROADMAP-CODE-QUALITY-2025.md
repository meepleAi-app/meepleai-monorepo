# 📅 CALENDARIO RIELABORATO - FOCUS CODE QUALITY
**MeepleAI Monorepo** | **Stato: 15 Novembre 2025** | **147 Issue Aperte**

---

## 🎉 ACHIEVEMENT UNLOCKED - PROGRESSI ECCEZIONALI!

### ✅ Sprint Completati in 1 Settimana (13 Issue Chiuse - 15/11/2025)

| Sprint | Issue Chiuse | Deliverable | Status |
|--------|--------------|-------------|--------|
| **Sprint 2** | #855, #1092, #1093, #1094 | Game Library Foundation | ✅ **COMPLETATO** |
| **Sprint 3** | #858, #859, #860, #1097, #1101, #1102 | Chat Enhancement | ✅ **COMPLETATO** |
| **Altri** | #1167, #1196, #1174 | Optimistic Updates, DDD Queries, Bug Fix | ✅ **COMPLETATO** |

**Totale chiuso**: 13 issue in 1 giorno (velocità eccezionale!)

---

## 🆕 NUOVE ISSUE - CODE QUALITY FOCUS (Create 15/11/2025)

### 🔴 CRITICA - P0 (Immediate Action)

| # | Titolo | Area | Impact | Effort |
|---|--------|------|--------|--------|
| **#1183** | Fix Deadlock Risk in RateLimitService | **BACKEND** | 🔥 System-wide auth failure risk | 2-3h |

**Dettagli Critici**:
- **File**: `apps/api/src/Api/Services/RateLimitService.cs:160-161`
- **Problema**: Blocking async calls (`.Result`) → Thread pool starvation + Deadlocks
- **Impatto**: Tutti gli endpoint auth (login, 2FA, OAuth)
- **Fix**: Convertire a `async/await`, aggiungere `CancellationToken`
- **Affected**: `AuthEndpoints.cs:347, 527-528, 579-580`

---

### 🟠 ALTA PRIORITÀ - DDD/CQRS Migration (8 Issue - Architettura)

#### Track 1: Service Migration to CQRS (Legacy Code Removal)

| # | Titolo | Servizio Target | Complessità |
|---|--------|-----------------|-------------|
| **#1184** | Migrate ChatService to CQRS | ChatService | M |
| **#1185** | Migrate RuleSpecService to CQRS | RuleSpecService | L |
| **#1188** | Migrate Agent Services to CQRS | AgentService | M |
| **#1189** | Migrate RuleSpec Comment/Diff to CQRS | CommentService, DiffService | M |

**Obiettivo**: Completare DDD migration da 99% a 100%
- Eliminare ultimi servizi legacy
- Pattern: Domain → Commands/Queries → Handlers → Endpoints
- Allineamento con architettura esistente (7 bounded contexts)

---

#### Track 2: Advanced CQRS Patterns

| # | Titolo | Pattern | Benefit |
|---|--------|---------|---------|
| **#1186** | Implement Streaming Query Handlers | RAG/QA Streaming | Real-time chat responses |
| **#1190** | Implement Domain Events for All Aggregates | Domain Events | Event-driven architecture |
| **#1191** | Complete OAuth Callback Migration to CQRS | OAuth CQRS | Auth consistency |

**Obiettivo**: Pattern avanzati DDD
- Streaming queries per chat real-time
- Domain events per cross-context communication
- OAuth alignment con CQRS

---

### 🟡 MEDIA PRIORITÀ - Performance & Security

| # | Titolo | Area | Impact | Effort |
|---|--------|------|--------|--------|
| **#1192** | Add AsNoTracking to Read-Only Queries | **BACKEND/DB** | 30% query performance boost | S |
| **#1193** | Improve Session Authorization + Rate Limiting | **BACKEND/Security** | Enhanced security posture | M |
| **#1194** | Centralize Error Handling with Middleware | **BACKEND/Middleware** | Consistent error responses | M |
| **#1187** | Replace Hardcoded Configuration Values | **BACKEND/Config** | Clean code, maintainability | S |

---

## 🎯 MONTH 3: Multi-Model Validation (10 Issue Backend - Alta Priorità)

### Track: RAG Validation Pipeline (Backend Quality Framework)

| # | Titolo | Focus | Effort |
|---|--------|-------|--------|
| **#973** | Unit tests for 3 validation layers | Testing | M |
| **#974** | MultiModelValidationService (GPT-4 + Claude) | LLM Consensus | L |
| **#975** | Consensus similarity calculation (≥0.90) | Validation Logic | M |
| **#976** | Unit tests for consensus validation (18 tests) | Testing | M |
| **#977** | Wire all 5 validation layers in RAG pipeline | Integration | L |
| **#978** | End-to-end testing (Q→Validated Response) | E2E Testing | L |
| **#979** | Performance optimization (parallel validation) | Performance | M |
| **#980** | Bug fixes for validation edge cases | Quality | S |
| **#981** | Accuracy baseline measurement (80%+ target) | Quality Metrics | M |
| **#982** | Update ADRs with validation implementation | Documentation | S |

**Deliverable**: Production-ready multi-model validation (<3% hallucination target)

---

## 📋 SPRINT 4-5: Remaining Work

### Sprint 4: Session Management (2 Issue)

| # | Titolo | Area | Status |
|---|--------|------|--------|
| **#864** | Active Session Management UI | **FRONTEND** | 🟡 Open |
| **#865** | Session History & Statistics | **FRONTEND** | 🟡 Open |

### Sprint 5: AI Rules (2 Issue)

| # | Titolo | Area | Status |
|---|--------|------|--------|
| **#868** | Agent Selection UI | **FRONTEND** | 🟡 Open |
| **#869** | Move Validation (RuleSpec v2) | **BACKEND** | 🟡 Open |

### Sprint 3: Remaining Frontend (3 Issue - Create 13/11)

| # | Titolo | Area | Status |
|---|--------|------|--------|
| **#1098** | Comprehensive Component Unit Tests | **FRONTEND** | 🟡 Open |
| **#1099** | Landing Page Performance/UX | **FRONTEND** | 🟡 Open |
| **#1100** | Keyboard Shortcuts System | **FRONTEND** | 🟡 Open |

---

## 🗺️ ROADMAP PRIORITIZZATA - CODE QUALITY FIRST

### SETTIMANA 1: CRITICAL FIXES (18-22 Nov 2025)

```
┌─────────────────────────────────────────────────┐
│ PRIORITY 0 - CRITICAL                           │
├─────────────────────────────────────────────────┤
│ 1. #1183 - Fix Deadlock in RateLimitService    │
│    • File: RateLimitService.cs:160-161         │
│    • Convert to async/await                     │
│    • Update AuthEndpoints callers               │
│    • Load test 100+ concurrent requests         │
│    • Effort: 2-3 ore                            │
│    • BLOCCA: Tutti gli endpoint auth            │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│ PRIORITY 1 - QUICK WINS (Parallel)              │
├─────────────────────────────────────────────────┤
│ 2. #1192 - Add AsNoTracking to Read-Only        │
│    • Pattern: query.AsNoTracking()              │
│    • ~30% performance boost                     │
│    • Effort: 4-6 ore                            │
│                                                  │
│ 3. #1187 - Replace Hardcoded Config Values      │
│    • Move to appsettings.json / DB              │
│    • Effort: 3-4 ore                            │
└─────────────────────────────────────────────────┘
```

**Timeline**: 1 settimana (10-15 ore totali)
**Deliverable**: Critical bugs fixed + 2 quick wins

---

### SETTIMANA 2-3: DDD/CQRS MIGRATION - Track 1 (25 Nov - 6 Dic)

```
┌───── Service Migration (Priority Order) ────────┐
│                                                  │
│ Week 2:                                          │
│ 4. #1184 - Migrate ChatService to CQRS          │
│    • ChatService → Commands/Queries             │
│    • Pattern: SendMessage, GetMessages          │
│    • Remove legacy ChatService                  │
│    • Effort: 2-3 giorni                         │
│                                                  │
│ 5. #1188 - Migrate Agent Services to CQRS       │
│    • AgentService → CQRS                        │
│    • Effort: 2-3 giorni                         │
│                                                  │
│ Week 3:                                          │
│ 6. #1189 - Migrate RuleSpec Comment/Diff        │
│    • CommentService, DiffService → CQRS         │
│    • Effort: 2-3 giorni                         │
│                                                  │
│ 7. #1185 - Migrate RuleSpecService to CQRS      │
│    • RuleSpecService → CQRS (Large)             │
│    • Effort: 3-4 giorni                         │
└─────────────────────────────────────────────────┘
```

**Timeline**: 2 settimane
**Deliverable**: 4 servizi legacy rimossi
**Achievement**: DDD Migration 100% COMPLETATA! 🎉

---

### SETTIMANA 4: DDD/CQRS MIGRATION - Track 2 (9-13 Dic)

```
┌───── Advanced Patterns ─────────────────────────┐
│                                                  │
│ 8. #1191 - Complete OAuth Callback → CQRS       │
│    • OAuth flow alignment                       │
│    • Effort: 2 giorni                           │
│                                                  │
│ 9. #1186 - Implement Streaming Query Handlers   │
│    • RAG/QA streaming responses                 │
│    • Pattern: IAsyncEnumerable<T>               │
│    • Effort: 2-3 giorni                         │
│                                                  │
│ 10. #1190 - Domain Events for All Aggregates    │
│     • Cross-context communication               │
│     • Event infrastructure                      │
│     • Effort: 2-3 giorni (parallel con #9)      │
└─────────────────────────────────────────────────┘
```

**Timeline**: 1 settimana
**Deliverable**: Advanced DDD patterns operational

---

### SETTIMANA 5: SECURITY & MIDDLEWARE (16-20 Dic)

```
┌───── Security & Infrastructure ─────────────────┐
│                                                  │
│ 11. #1193 - Session Authorization + Rate Limit  │
│     • Enhanced security checks                  │
│     • Rate limiting improvements                │
│     • Effort: 3-4 giorni                        │
│                                                  │
│ 12. #1194 - Centralize Error Handling           │
│     • Error middleware implementation           │
│     • Consistent responses                      │
│     • Effort: 2-3 giorni                        │
└─────────────────────────────────────────────────┘
```

**Timeline**: 1 settimana
**Deliverable**: Security hardening + error handling

---

### SETTIMANE 6-9: MONTH 3 - MULTI-MODEL VALIDATION (Dic-Gen)

#### Week 6-7: Core Validation (23 Dic - 3 Gen)

```
┌───── Core Validation ───────────────────────────┐
│                                                  │
│ 13. #974 - MultiModelValidationService          │
│     • GPT-4 + Claude consensus                  │
│     • Effort: 1 settimana                       │
│                                                  │
│ 14. #975 - Consensus similarity (≥0.90)         │
│     • Cosine similarity calculation             │
│     • Effort: 3 giorni                          │
│                                                  │
│ 15. #973 - Unit tests (3 validation layers)     │
│     • Effort: 2 giorni                          │
│                                                  │
│ 16. #976 - Unit tests consensus (18 tests)      │
│     • Effort: 2 giorni                          │
└─────────────────────────────────────────────────┘
```

#### Week 8: Integration (6-10 Gen)

```
┌───── Integration ───────────────────────────────┐
│                                                  │
│ 17. #977 - Wire 5 validation layers in RAG      │
│     • Integration work                          │
│     • Effort: 1 settimana                       │
│                                                  │
│ 18. #979 - Parallel validation optimization     │
│     • Performance tuning (parallel)             │
│     • Effort: 3 giorni                          │
└─────────────────────────────────────────────────┘
```

#### Week 9: Testing & Docs (13-17 Gen)

```
┌───── Testing & Documentation ───────────────────┐
│                                                  │
│ 19. #978 - E2E testing (Q→Validated Response)   │
│     • Effort: 3 giorni                          │
│                                                  │
│ 20. #981 - Accuracy baseline (80%+ target)      │
│     • Quality metrics                           │
│     • Effort: 2 giorni                          │
│                                                  │
│ 21. #980 - Bug fixes for edge cases             │
│     • Effort: 2 giorni                          │
│                                                  │
│ 22. #982 - Update ADRs                          │
│     • Documentation                             │
│     • Effort: 1 giorno                          │
└─────────────────────────────────────────────────┘
```

**Timeline**: 4 settimane
**Deliverable**: Multi-model validation production-ready
**Target**: <3% hallucination rate

---

### SETTIMANE 10-11: SPRINT 3-5 COMPLETION (Gen-Feb)

```
┌───── Frontend Polish ───────────────────────────┐
│                                                  │
│ 23. #1098 - Component Unit Tests                │
│ 24. #1099 - Landing Page Performance            │
│ 25. #1100 - Keyboard Shortcuts                  │
│                                                  │
│ 26. #864  - Active Session Management UI        │
│ 27. #865  - Session History/Statistics          │
│                                                  │
│ 28. #868  - Agent Selection UI                  │
│ 29. #869  - Move Validation (RuleSpec v2)       │
└─────────────────────────────────────────────────┘
```

**Timeline**: 2 settimane
**Deliverable**: Sprint 3-5 completamento

---

## 📊 CALENDARIO VISUALE - 11 SETTIMANE

```
NOV 2025          DIC 2025           GEN 2026          FEB 2026
Week 1     Week 2-3        Week 4-5        Week 6-9           Week 10-11
──────     ────────────    ────────────    ─────────────────  ──────────
#1183      #1184 #1188     #1191 #1186     #974 #975 #973     #1098-#1100
#1192      #1189 #1185     #1190           #976 #977 #979     #864 #865
#1187      (DDD Track 1)   #1193 #1194     #978 #981 #980     #868 #869
CRITICAL   SERVICE CQRS    SECURITY        MULTI-MODEL VAL    FRONTEND

│          │               │               │                   │
│          │               │               │                   │
▼          ▼               ▼               ▼                   ▼
Bugs       DDD 100%        Hardening       RAG Validation      MVP Ready
Fixed      Complete        Complete        Production          Sprint 3-5
                                                               Complete
```

---

## 📈 METRICHE & PRIORITÀ

### Issue Distribuzione per Categoria

```
╔══════════════════════════════════════════════════════╗
║  CATEGORIA                  COUNT    PRIORITY  WEEKS ║
╠══════════════════════════════════════════════════════╣
║  🔴 CRITICAL BUGS              1      P0        0.2  ║
║  🟠 DDD/CQRS Migration         8      Alta      3.0  ║
║  🟠 Month 3 Validation        10      Alta      4.0  ║
║  🟡 Performance/Security       4      Media     2.0  ║
║  🟡 Sprint 3-5 Remaining       7      Media     2.0  ║
║  ─────────────────────────────────────────────────── ║
║  TOTALE CODE QUALITY          30               11.2  ║
╚══════════════════════════════════════════════════════╝
```

### Effort Distribution

| Categoria | Issue | Effort Totale | % |
|-----------|-------|---------------|---|
| **Critical Fixes** | 1 | 2-3 ore | 1% |
| **Quick Wins** | 2 | 7-10 ore | 3% |
| **DDD/CQRS** | 8 | 4-5 settimane | 36% |
| **Month 3 Validation** | 10 | 4 settimane | 36% |
| **Security/Perf** | 4 | 2 settimane | 18% |
| **Sprint Completion** | 7 | 2 settimane | 18% |

### Impact Score (Code Quality)

| Issue | Impact | Technical Debt Reduction | Priority |
|-------|--------|--------------------------|----------|
| #1183 | 🔥🔥🔥🔥🔥 | System stability | P0 |
| #1184-#1191 | 🔥🔥🔥🔥 | DDD 100% complete | P1 |
| #974-#982 | 🔥🔥🔥🔥 | RAG quality <3% error | P1 |
| #1192 | 🔥🔥🔥 | 30% query performance | P2 |
| #1193 | 🔥🔥🔥 | Security hardening | P2 |
| #1194 | 🔥🔥 | Error consistency | P2 |
| #1187 | 🔥🔥 | Config maintainability | P3 |

---

## ✅ RACCOMANDAZIONI IMMEDIATE

### 🚨 AZIONE IMMEDIATA - Questa Settimana

```bash
PRIORITY 0 - FIX CRITICO (2-3 ore)
═══════════════════════════════════════

Issue: #1183 - Deadlock in RateLimitService
File:  apps/api/src/Api/Services/RateLimitService.cs

STEP 1: Fix RateLimitService (1h)
─────────────────────────────────
// BEFORE (DANGEROUS)
public RateLimitConfig GetConfigForRole(UserRole role)
{
    var config = _configService.GetRateLimitConfigAsync().Result;  // ❌ DEADLOCK!
    return config.Configs[role];
}

// AFTER (SAFE)
public async Task<RateLimitConfig> GetConfigForRoleAsync(
    UserRole role,
    CancellationToken cancellationToken = default)
{
    var config = await _configService.GetRateLimitConfigAsync(cancellationToken);
    return config.Configs[role];
}

STEP 2: Update AuthEndpoints.cs (1h)
────────────────────────────────────
Locations: Lines 347, 527-528, 579-580
Pattern: await _rateLimitService.GetConfigForRoleAsync(role, ct);

STEP 3: Load Testing (30min)
────────────────────────────
Test: 100+ concurrent auth requests
Verify: No deadlocks, no thread starvation

STEP 4: Commit & Push
─────────────────────
git commit -m "fix(auth): eliminate deadlock in RateLimitService #1183"
```

---

### 🎯 QUICK WINS - Questa Settimana (Parallel)

```bash
PRIORITY 1 - PERFORMANCE (4-6 ore)
═══════════════════════════════════

Issue: #1192 - Add AsNoTracking to Read-Only Queries

Pattern:
  // Read-only queries → AsNoTracking()
  var games = await _context.Games
      .AsNoTracking()  // ← Add this
      .Where(g => g.IsPublished)
      .ToListAsync();

Impact: ~30% query performance boost
Files: All Query handlers (Get*, Search*, List*)

─────────────────────────────────────

PRIORITY 2 - CLEAN CODE (3-4 ore)
═══════════════════════════════════

Issue: #1187 - Replace Hardcoded Config Values

Pattern:
  // BEFORE
  const int MaxRetries = 3;  // ❌ Hardcoded

  // AFTER
  var maxRetries = _config.GetValue<int>("Retry:MaxAttempts");

Impact: Maintainability, runtime config changes
Files: Scan for const/magic numbers
```

---

### 📅 PROSSIME 2 SETTIMANE - DDD Migration

```bash
Week 2 (25 Nov - 29 Nov):
  • #1184 - ChatService → CQRS
  • #1188 - AgentService → CQRS

Week 3 (2 Dic - 6 Dic):
  • #1189 - RuleSpec Comment/Diff → CQRS
  • #1185 - RuleSpecService → CQRS (large)

Deliverable: DDD Architecture 100% Complete! 🎉
Pattern: Reuse existing bounded context structure
```

---

## 🎯 ISSUE PRIORITARIE DA ELABORARE SUBITO

### TOP 10 CODE QUALITY ISSUES - Ordine di Esecuzione

| # | Priority | Titolo | Effort | Impact | Timeline |
|---|----------|--------|--------|--------|----------|
| 1 | **P0** | #1183 - Fix Deadlock in RateLimitService | 2-3h | 🔥🔥🔥🔥🔥 | **ORA** |
| 2 | **P1** | #1192 - Add AsNoTracking to Read-Only | 4-6h | 🔥🔥🔥 | Week 1 |
| 3 | **P1** | #1187 - Replace Hardcoded Config | 3-4h | 🔥🔥 | Week 1 |
| 4 | **P1** | #1184 - ChatService → CQRS | 2-3d | 🔥🔥🔥🔥 | Week 2 |
| 5 | **P1** | #1188 - AgentService → CQRS | 2-3d | 🔥🔥🔥🔥 | Week 2 |
| 6 | **P1** | #1189 - RuleSpec Services → CQRS | 2-3d | 🔥🔥🔥🔥 | Week 3 |
| 7 | **P1** | #1185 - RuleSpecService → CQRS | 3-4d | 🔥🔥🔥🔥 | Week 3 |
| 8 | **P2** | #1191 - OAuth Callback → CQRS | 2d | 🔥🔥🔥 | Week 4 |
| 9 | **P2** | #1186 - Streaming Query Handlers | 2-3d | 🔥🔥🔥 | Week 4 |
| 10 | **P2** | #1190 - Domain Events | 2-3d | 🔥🔥🔥 | Week 4 |

---

## 📊 DASHBOARD CODE QUALITY

```
╔════════════════════════════════════════════════════════════╗
║         MEEPLEAI - CODE QUALITY ROADMAP                    ║
║              15 Novembre 2025                              ║
╠════════════════════════════════════════════════════════════╣
║ SPRINT STATUS:                                             ║
║  ✅ Sprint 1 - COMPLETATO (Authentication)                ║
║  ✅ Sprint 2 - COMPLETATO (Game Library)                  ║
║  ✅ Sprint 3 - COMPLETATO (Chat Enhancement)              ║
║  🟡 Sprint 4 - Aperto (2 issue)                           ║
║  🟡 Sprint 5 - Aperto (2 issue)                           ║
║                                                            ║
║ CODE QUALITY FOCUS:                                        ║
║  🔴 1  Critical Bug (Deadlock)                            ║
║  🟠 8  DDD/CQRS Migration                                 ║
║  🟠 10 Month 3 Validation                                 ║
║  🟡 4  Performance/Security                               ║
║                                                            ║
║ DDD MIGRATION STATUS:                                      ║
║  Current: 99% (6/7 contexts complete)                     ║
║  Target:  100% (dopo issue #1184-#1191)                   ║
║  Remaining Services: 4                                     ║
║                                                            ║
║ TIMELINE CODE QUALITY:                                     ║
║  Week 1:  Critical fixes + Quick wins                     ║
║  Week 2-3: DDD Migration → 100%                           ║
║  Week 4-5: Advanced patterns + Security                   ║
║  Week 6-9: Multi-Model Validation                         ║
║                                                            ║
║ TARGET: DDD 100% + Production RAG Quality                 ║
║ ETA: 11 settimane (→ Fine Gennaio 2026)                   ║
╚════════════════════════════════════════════════════════════╝
```

---

## 🎯 DECISIONI RICHIESTE

### 1. CRITICO - #1183 Deadlock
- **Domanda**: Procedere immediatamente con il fix?
- **Raccomandazione**: ✅ **SI**, è P0 e richiede solo 2-3 ore
- **Rischio**: Deadlock in produzione su tutti gli endpoint auth

### 2. DDD Migration Strategy
- **Domanda**: Prioritizzare DDD 100% prima di Month 3 validation?
- **Raccomandazione**: ✅ **SI**
  - Week 2-3: Completare DDD (issue #1184-#1191)
  - Week 6-9: Month 3 validation con architettura pulita
- **Benefit**: Codebase 100% DDD-compliant prima di nuove feature

### 3. Month 3 vs Sprint 4-5
- **Domanda**: Priorità Month 3 (backend quality) o Sprint 4-5 (frontend)?
- **Raccomandazione**: ✅ **Month 3 first**
  - Backend validation è foundation per frontend
  - Sprint 4-5 possono procedere in parallelo (frontend team)

---

## 🚀 PROSSIMI PASSI CONSIGLIATI

### Opzione A: FIX IMMEDIATO #1183 (RACCOMANDATO)
Procedere con fix critico deadlock RateLimitService (2-3 ore)

### Opzione B: QUICK WINS PARALLEL
#1192 (AsNoTracking) + #1187 (Hardcoded config) in parallelo

### Opzione C: DDD MIGRATION PLANNING
Planning dettagliato per #1184 (ChatService → CQRS)

### Opzione D: MONTH 3 PREPARATION
Review architettura Multi-Model Validation (#974-#982)

---

## 💡 SUGGERIMENTO FINALE

**Raccomandazione**: **Opzione A** (#1183 - Deadlock fix)

**Motivazioni**:
- ✅ È **P0 Critical**
- ✅ Richiede solo **2-3 ore**
- ✅ **Blocca** tutti gli endpoint auth in produzione
- ✅ **Quick win** con alto impatto

---

## 📚 RIFERIMENTI

- **Repository**: https://github.com/DegrassiAaron/meepleai-monorepo
- **Architettura DDD**: `docs/architecture/board-game-ai-architecture-overview.md`
- **ADR Hybrid RAG**: `docs/architecture/adr-001-hybrid-rag-architecture.md`
- **DDD Status**: `docs/refactoring/ddd-status-and-roadmap.md`
- **CLAUDE.md**: `/home/user/meepleai-monorepo/CLAUDE.md`

---

**Versione**: 1.0
**Data**: 15 Novembre 2025
**Autore**: Engineering Lead
**Status**: 🟢 Active Development

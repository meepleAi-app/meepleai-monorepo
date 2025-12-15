# Test Coverage Gap Analysis

**Data Analisi**: 2025-12-14
**Autore**: Claude Code
**Branch**: issue/2181/enable-security-analyzers

---

## Statistiche Generali

| Area | File Test | Status |
|------|-----------|--------|
| Backend Unit Tests | 302 | ✅ |
| Frontend Unit Tests | 217 | ✅ |
| E2E Tests | 76 | ✅ |
| **Totale** | **595** | - |

---

## GAP CRITICI - Feature Senza Test

### 1. Game FAQs (17 file sorgente, 0 test)

| Handler/Entity | Path | Priority |
|----------------|------|----------|
| `CreateGameFAQCommandHandler` | GameManagement/Application/Handlers | 🔴 HIGH |
| `UpdateGameFAQCommandHandler` | GameManagement/Application/Handlers | 🔴 HIGH |
| `DeleteGameFAQCommandHandler` | GameManagement/Application/Handlers | 🔴 HIGH |
| `UpvoteGameFAQCommandHandler` | GameManagement/Application/Handlers | 🔴 HIGH |
| `GetGameFAQsQueryHandler` | GameManagement/Application/Handlers | 🔴 HIGH |
| `GameFAQ` (Entity) | GameManagement/Domain/Entities | 🟡 MEDIUM |
| `GameFAQRepository` | GameManagement/Infrastructure | 🟡 MEDIUM |
| `FAQQuestion` (VO) | GameManagement/Domain/ValueObjects | 🟢 LOW |
| `FAQAnswer` (VO) | GameManagement/Domain/ValueObjects | 🟢 LOW |

**Flussi Non Testati**:
- Creazione FAQ per un gioco
- Modifica FAQ esistente
- Eliminazione FAQ
- Upvote/downvote FAQ
- Recupero lista FAQ per gioco

---

### 2. N8N Workflow Templates (8 file sorgente, 0 test)

| Handler | Path | Priority |
|---------|------|----------|
| `ImportN8nTemplateCommandHandler` | WorkflowIntegration/Application/Commands | 🔴 HIGH |
| `GetN8nTemplatesQueryHandler` | WorkflowIntegration/Application/Queries | 🔴 HIGH |
| `GetN8nTemplateByIdQueryHandler` | WorkflowIntegration/Application/Queries | 🔴 HIGH |
| `ValidateN8nTemplateQueryHandler` | WorkflowIntegration/Application/Queries | 🟡 MEDIUM |

**Flussi Non Testati**:
- Import di un template n8n
- Validazione template prima dell'import
- Lista template disponibili
- Recupero dettaglio template

---

### 3. Config Import/Export (4 file sorgente, 0 test)

| Handler | Path | Priority |
|---------|------|----------|
| `ImportConfigsCommandHandler` | SystemConfiguration/Application/Handlers | 🔴 HIGH |
| `ExportConfigsQueryHandler` | SystemConfiguration/Application/Handlers | 🔴 HIGH |

**Flussi Non Testati**:
- Export configurazioni sistema (backup)
- Import configurazioni sistema (restore)
- Validazione formato configurazioni

---

### 4. API Key Rotation (2 file sorgente, 0 test)

| Handler | Path | Priority |
|---------|------|----------|
| `RotateApiKeyCommandHandler` | Authentication/Application/Commands/ApiKeys | 🔴 HIGH |

**Flussi Non Testati**:
- Rotazione API key (security-critical)
- Invalidazione vecchia key
- Generazione nuova key

---

### 5. Agent Management (2 file sorgente, 0 test)

| Handler | Path | Priority |
|---------|------|----------|
| `CreateAgentCommandHandler` | KnowledgeBase/Application/Handlers | 🔴 HIGH |

**Flussi Non Testati**:
- Creazione nuovo agent RAG
- Configurazione agent parameters

*Nota: `InvokeAgentCommandHandler` è testato, ma CRUD manca*

---

### 6. Chat Export (2 file sorgente, 0 unit test)

| Handler | Path | Priority |
|---------|------|----------|
| `ExportChatCommandHandler` | KnowledgeBase/Application/Handlers | 🟡 MEDIUM |
| `ChatExportService` | Api/Services | 🟡 MEDIUM |

**Flussi Non Testati (Unit)**:
- Export conversazione in formato JSON
- Export conversazione in formato Markdown
- Validazione permessi export

*Nota: Ha copertura E2E (`chat-export.spec.ts`) ma manca unit test backend*

---

### 7. BoardGameGeek Integration (4 file sorgente, 0 test)

| Handler | Path | Priority |
|---------|------|----------|
| `SearchBggGamesQueryHandler` | GameManagement/Application/Queries/BggApi | 🔴 HIGH |
| `GetBggGameDetailsQueryHandler` | GameManagement/Application/Queries/BggApi | 🔴 HIGH |
| `BggApiService` | Api/Services | 🟡 MEDIUM |

**Flussi Non Testati**:
- Ricerca giochi su BoardGameGeek
- Fetch dettagli gioco da BGG
- Gestione errori API esterna
- Rate limiting chiamate BGG

---

## Feature con Buona Copertura

| Feature | Backend Tests | E2E Tests | Status |
|---------|--------------|-----------|--------|
| Authentication (Login/Register) | ✅ 15+ | ✅ 5+ | ✅ |
| Two-Factor Authentication | ✅ 10+ | ✅ 3+ | ✅ |
| OAuth (Google/Discord/GitHub) | ✅ 5+ | ✅ 3+ | ✅ |
| Password Management | ✅ 5+ | ✅ 2+ | ✅ |
| Session Management | ✅ 5+ | ✅ | ✅ |
| API Key Create/List/Delete | ✅ 5+ | ✅ | ✅ |
| Share Links | ✅ 9 | ✅ | ✅ |
| Game CRUD | ✅ 10+ | ✅ | ✅ |
| Game Sessions | ✅ 5+ | ✅ | ✅ |
| Rule Comments | ✅ 6+ | - | ✅ |
| RAG/Chat | ✅ 20+ | ✅ 10+ | ✅ |
| Chat Threads | ✅ 5+ | ✅ | ✅ |
| PDF Upload/Processing | ✅ 15+ | ✅ 5+ | ✅ |
| Document Collections | ✅ 3 | - | ✅ |
| Admin Stats/Analytics | ✅ 5 | ✅ 5+ | ✅ |
| Alert Management | ✅ 6 | ✅ | ✅ |
| Audit Logging (Domain) | ✅ 5 | - | ✅ |
| N8N Config | ✅ 3+ | ✅ | ✅ |
| Workflow Error Logging | ✅ 1 | - | ✅ |
| System Configuration | ✅ 7 | ✅ | ✅ |
| Rate Limiting | ✅ 3+ | - | ✅ |
| Backup Codes | ✅ 7 | ✅ | ✅ |

---

## Riepilogo Gap per Bounded Context

| Bounded Context | Handlers Non Testati | Priorità |
|-----------------|---------------------|----------|
| **GameManagement** | 5 (FAQ) + 2 (BGG) = **7** | 🔴 |
| **WorkflowIntegration** | **4** (Templates) | 🔴 |
| **SystemConfiguration** | **2** (Import/Export) | 🔴 |
| **Authentication** | **1** (RotateApiKey) | 🔴 |
| **KnowledgeBase** | **2** (CreateAgent, ExportChat) | 🟡 |

**Totale Handler Non Testati: 16**

---

## Raccomandazioni Prioritarie

### Priorità 1 - Critica (Feature complete senza alcun test)

1. **`GameFAQ` handlers** (5 handler)
   - Impact: User-facing feature
   - Risk: Bug in produzione non rilevabili
   - Effort: ~2-3 giorni

2. **`ImportConfigsCommand/ExportConfigsQuery`** (2 handler)
   - Impact: Backup/restore configurazioni
   - Risk: Perdita dati configurazione
   - Effort: ~1 giorno

3. **`RotateApiKeyCommandHandler`** (1 handler)
   - Impact: Security-critical
   - Risk: Vulnerabilità sicurezza
   - Effort: ~0.5 giorni

### Priorità 2 - Alta (Feature API esterne)

1. **BGG Integration** (2 handler + 1 service)
   - Impact: Integrazione esterna
   - Risk: Failure silenti, rate limiting
   - Effort: ~1-2 giorni

2. **N8N Templates** (4 handler)
   - Impact: Workflow automation
   - Risk: Import corrotti
   - Effort: ~1-2 giorni

### Priorità 3 - Media (Copertura parziale)

1. **`CreateAgentCommandHandler`** (1 handler)
   - Note: Solo invoke testato
   - Effort: ~0.5 giorni

2. **`ExportChatCommandHandler`** (1 handler)
   - Note: E2E presente, unit mancante
   - Effort: ~0.5 giorni

---

## Coverage Stimata per Feature

```
Feature                    Coverage    Gap
─────────────────────────────────────────
Authentication            ██████████  95%   API Key Rotation
Game Catalog              ████████░░  80%   FAQ, BGG
KnowledgeBase/RAG         █████████░  90%   Agent CRUD
Document Processing       █████████░  90%   -
WorkflowIntegration       ████░░░░░░  40%   Templates
SystemConfiguration       ███████░░░  70%   Import/Export
Administration            █████████░  90%   -
Cross-Cutting             ████████░░  80%   -
```

---

## Test da Creare

### Backend Unit Tests Necessari

```
apps/api/tests/Api.Tests/BoundedContexts/
├── GameManagement/Application/Handlers/
│   ├── CreateGameFAQCommandHandlerTests.cs      [NEW]
│   ├── UpdateGameFAQCommandHandlerTests.cs      [NEW]
│   ├── DeleteGameFAQCommandHandlerTests.cs      [NEW]
│   ├── UpvoteGameFAQCommandHandlerTests.cs      [NEW]
│   ├── GetGameFAQsQueryHandlerTests.cs          [NEW]
│   ├── SearchBggGamesQueryHandlerTests.cs       [NEW]
│   └── GetBggGameDetailsQueryHandlerTests.cs    [NEW]
├── WorkflowIntegration/Application/Handlers/
│   ├── ImportN8nTemplateCommandHandlerTests.cs  [NEW]
│   ├── GetN8nTemplatesQueryHandlerTests.cs      [NEW]
│   ├── GetN8nTemplateByIdQueryHandlerTests.cs   [NEW]
│   └── ValidateN8nTemplateQueryHandlerTests.cs  [NEW]
├── SystemConfiguration/Application/Handlers/
│   ├── ImportConfigsCommandHandlerTests.cs      [NEW]
│   └── ExportConfigsQueryHandlerTests.cs        [NEW]
├── Authentication/Application/Commands/
│   └── RotateApiKeyCommandHandlerTests.cs       [NEW]
└── KnowledgeBase/Application/Handlers/
    ├── CreateAgentCommandHandlerTests.cs        [NEW]
    └── ExportChatCommandHandlerTests.cs         [NEW]
```

### E2E Tests Consigliati

```
apps/web/e2e/
├── game-faq.spec.ts                             [NEW]
├── n8n-templates.spec.ts                        [NEW]
├── config-import-export.spec.ts                 [NEW]
├── api-key-rotation.spec.ts                     [NEW]
└── bgg-integration.spec.ts                      [NEW]
```

---

## Note Metodologiche

### Criteri di Priorità

- 🔴 **HIGH**: Feature user-facing o security-critical senza test
- 🟡 **MEDIUM**: Feature con copertura parziale o internal-only
- 🟢 **LOW**: Value objects, DTOs, utilities

### Esclusioni dall'Analisi

- Test di infrastruttura (Docker, CI/CD)
- Test di configurazione ambiente
- Mock e fixtures (non sono test funzionali)

### Riferimenti

- [Feature Class Mapping](../04-frontend/feature-class-mapping.md)
- [User Flows Comprehensive](../04-frontend/user-flows-comprehensive.md)
- [Test Writing Guide](./test-writing-guide.md)

---

*Documento generato automaticamente da analisi codebase*

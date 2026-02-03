# FLUSSI CRITICI - Sezione da Inserire in Testa a sequenza.md

---

## 🚀 FLUSSI CRITICI - User & Admin Journeys (PRIORITÀ MASSIMA)

**Status**: ✅ Epic e Issue create - Ready for implementation
**Timeline**: 3 weeks con parallelizzazione (vs 7 weeks sequential)
**Efficiency**: 65% time saving (~4 weeks saved)

### Obiettivo
Completare i due flussi end-to-end più critici per user e admin:

1. **FLUSSO ADMIN**: Dashboard → Wizard → Upload PDF → Publish to Shared Library
2. **FLUSSO USER**: Dashboard → Collection → Upload Private PDF → Chat → History

---

### 📅 Week 1 - Fondazioni (Sequential, BLOCKER)

**Prerequisiti per entrambi i flussi**:

| Issue | Titolo | Area | SP | Status | Branch |
|:-----:|--------|------|:--:|:------:|:------:|
| #3324 | SSE Infrastructure | Backend | 5 | ⬜ Open | - |
| #3370 | usePdfProcessingProgress hook | Frontend | 2 | ⬜ Open | - |

**Totale**: 7 SP, 5-7 giorni
**Critical Path**: SSE blocca progress real-time per PDF e chat

---

### 📅 Week 2 - Parallel Streams (3 stream concorrenti)

#### Stream A - Admin Flow (Epic #3306 estesa)

**Frontend Track**:
| Issue | Titolo | Area | SP | Status |
|:-----:|--------|------|:--:|:------:|
| #3480 | Admin Wizard - Publish to Shared Library | Frontend | 3 | ⬜ Open |
| #3482 | Game Approval Status UI | Frontend | 2 | ⬜ Open |

**Backend Track**:
| Issue | Titolo | Area | SP | Status |
|:-----:|--------|------|:--:|:------:|
| #3481 | SharedGameCatalog Publication Workflow | Backend | 5 | ⬜ Open |

**Totale Stream A**: 10 SP → ~5 giorni (parallelo)

#### Stream B - User Collection (Epic #3475 NUOVA)

**Frontend Track**:
| Issue | Titolo | Area | SP | Status |
|:-----:|--------|------|:--:|:------:|
| #3476 | User Collection Dashboard | Frontend | 5 | ⬜ Open |
| #3477 | Add Game to Collection Wizard | Frontend | 5 | ⬜ Open |

**Backend Track**:
| Issue | Titolo | Area | SP | Status |
|:-----:|--------|------|:--:|:------:|
| #3478 | UserLibraryEntry PDF Association | Backend | 3 | ⬜ Open |
| #3479 | Private PDF Upload Endpoint | Backend | 3 | ⬜ Open |

**Totale Stream B**: 16 SP → ~8 giorni (parallelo)

#### Stream C - Agent Foundation (Epic #3386 parziale)

**Frontend Track**:
| Issue | Titolo | Area | SP | Status |
|:-----:|--------|------|:--:|:------:|
| #3376 | Agent Creation Wizard | Frontend | 5 | ⬜ Open |
| #3375 | Agent Session Launch | Frontend | 3 | ⬜ Open |

**Totale Stream C**: 8 SP → ~8 giorni

**Week 2 Summary**: 34 SP parallelizzati → ~8 giorni (vs 34 giorni sequential)
**Efficiency**: ~70% time saving

---

### 📅 Week 3 - Integration & Chat History (Epic #3386 estesa)

**Dependencies**: Requires Week 2 Stream B (Collection) + Stream C (Agent) complete

**Backend Track**:
| Issue | Titolo | Area | SP | Status |
|:-----:|--------|------|:--:|:------:|
| #3483 | Chat Session Persistence Service | Backend | 5 | ⬜ Open |

**Frontend Track**:
| Issue | Titolo | Area | SP | Status |
|:-----:|--------|------|:--:|:------:|
| #3484 | Chat History Integration | Frontend | 3 | ⬜ Open |

**Totale Week 3**: 8 SP → ~5 giorni (parallelo)

---

### 🎯 Epic Summary - Flussi Critici

#### Epic #3475 (NUOVA): User Private Library & Collections Management
**Link**: https://github.com/DegrassiAaron/meepleai-monorepo/issues/3475
**Obiettivo**: User gestisce collezioni personali con PDF privati

| Issue | Titolo | SP | Week |
|:-----:|--------|:--:|:----:|
| [#3476](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3476) | User Collection Dashboard | 5 | Week 2 |
| [#3477](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3477) | Add Game to Collection Wizard | 5 | Week 2 |
| [#3478](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3478) | UserLibraryEntry PDF Association | 3 | Week 2 |
| [#3479](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3479) | Private PDF Upload Endpoint | 3 | Week 2 |

**Totale**: 4 issues, 16 SP
**Spec**: `docs/claudedocs/epic-user-private-library-spec.md`

#### Epic #3306 (ESTESA): Dashboard Hub & Game Management
**Link**: https://github.com/DegrassiAaron/meepleai-monorepo/issues/3306
**Nuove Issue per Admin Flow**:

| Issue | Titolo | SP | Week |
|:-----:|--------|:--:|:----:|
| [#3480](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3480) | Admin Wizard - Publish to Shared Library | 3 | Week 2 |
| [#3481](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3481) | SharedGameCatalog Publication Workflow | 5 | Week 2 |
| [#3482](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3482) | Game Approval Status UI | 2 | Week 2 |

**Totale estensione**: 3 issues, 10 SP

#### Epic #3386 (ESTESA): Agent Creation & Testing Flow
**Link**: https://github.com/DegrassiAaron/meepleai-monorepo/issues/3386
**Nuove Issue per Chat History**:

| Issue | Titolo | SP | Week |
|:-----:|--------|:--:|:----:|
| [#3483](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3483) | Chat Session Persistence Service | 5 | Week 3 |
| [#3484](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3484) | Chat History Integration | 3 | Week 3 |

**Totale estensione**: 2 issues, 8 SP

---

### 🏗️ Architecture Highlights

#### Private vs Shared PDF Strategy
```
Shared PDF:  collection = "shared_{gameId}"
Private PDF: collection = "private_{userId}_{gameId}"
```
**Rationale**: Data isolation + code reuse (same processing pipeline)

#### UserLibraryEntry Extension
```csharp
public class UserLibraryEntry {
    public Guid? PrivatePdfId { get; private set; } // NEW
    public bool HasPrivatePdf => PrivatePdfId.HasValue; // NEW
}
```
**Rationale**: Backward compatible, maintains existing relationships

#### Wizard Pattern Reuse
- **Source**: Agent Creation Wizard (#3376)
- **Pattern**: Multi-step + Zustand state + validation
- **Benefits**: Proven pattern, consistent UX, faster development

---

### ✅ Definition of Done - Flussi Critici

#### FLUSSO 1 - Admin
- [ ] Admin può creare gioco da dashboard personale
- [ ] Wizard permette upload PDF principale
- [ ] Gioco pubblicato in SharedGameCatalog con approval status
- [ ] Stato approvazione visualizzabile e gestibile
- [ ] Test E2E: wizard completo → gioco in catalog

#### FLUSSO 2 - User
- [ ] User può visualizzare collezione personale con stats
- [ ] Wizard permette aggiunta gioco con PDF privato
- [ ] PDF associato correttamente a UserLibraryEntry
- [ ] User può creare chat con agente sul gioco
- [ ] Chat salvata automaticamente in history
- [ ] Test E2E: add game → upload PDF → chat → history

#### Cross-Cutting
- [ ] SSE infrastructure funzionante (#3324)
- [ ] Progress PDF real-time visualizzato (#3370)
- [ ] Parallelizzazione backend/frontend verificata
- [ ] Documentation aggiornata (sequenza.md, roadmap.md)
- [ ] Performance: Collection dashboard < 2s, Chat history < 500ms

---

### 📊 Success Metrics

**Coverage**:
- FLUSSO 1 (Admin): 75% → 100% (+25%)
- FLUSSO 2 (User): 40% → 100% (+60%)

**Efficiency**:
- Parallelization: 3 concurrent streams Week 2
- Time-to-completion: ~21 giorni vs 49 giorni sequential
- **Time Saved**: ~28 giorni (~4 weeks, ~65% faster)

**Epic Health**:
- Epic nuova (#3475): 4 issues, 16 SP (focused, manageable)
- Epic estese: +10 SP (#3306), +8 SP (#3386)
- No epic > 60 SP (mantiene dimensioni gestibili)

---

**NOTE**: Questa sezione va inserita PRIMA della sezione "🎯 PRIORITÀ CORRENTE - Issue Critiche" in `sequenza.md`

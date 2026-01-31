# Epic: Game Session Toolkit - Collaborative Scorekeeper

**Epic ID**: EPIC-GST-001
**Priority**: High
**Estimated Effort**: 3-4 settimane (MVP), 5-6 settimane (Full Features)
**Created**: 2026-01-29

---

## 🎯 Epic Overview

Implementare un sistema completo di gestione sessioni di gioco collaborativo con scorekeeper digitale, supporto multi-dispositivo real-time, e integrazione con la UserLibrary.

### Problem Statement

I giocatori di board game hanno bisogno di:
- **Scorekeeper digitale** durante partite fisiche (senza app dedicate per ogni gioco)
- **Sincronizzazione multi-dispositivo** (più persone inseriscono punteggi contemporaneamente)
- **Storico sessioni** collegato alla propria libreria giochi
- **Strumenti casuali** (dadi virtuali, mazzi di carte) per completare l'esperienza

### Solution

Un toolkit completo con due modalità:
1. **Generic Toolkit** (navbar) - Scorekeeper universale per qualsiasi gioco
2. **Game-Specific Toolkit** (library card/page) - Template pre-configurati per giochi popolari

**Funzionalità chiave**:
- Scoresheet digitale con round/categorie
- Real-time sync (SSE) per collaborazione multi-device
- Note private con oscuramento visivo
- Dadi virtuali (d4-d100 + custom faces)
- Mazzi di carte (standard 52, database games, custom)
- Timer, coin flip, wheel spinner
- Storico sessioni con statistiche

---

## 📋 Epic Structure

### Phase 1: MVP - Core Scorekeeper (2-3 settimane)
**Focus**: Backend foundation + frontend scoresheet con sync real-time

**Issue Dependencies**:
```
EPIC-GST-001
├── GST-001: Backend Bounded Context & Database Schema
├── GST-002: Backend CQRS Commands & Queries (depends on GST-001)
├── GST-003: Backend Real-Time SSE Infrastructure (depends on GST-002)
├── GST-004: Frontend Generic Toolkit Routes (depends on GST-003)
├── GST-005: Frontend Game-Specific Integration (depends on GST-004)
├── GST-006: Session History & UserLibrary Link (depends on GST-005)
└── GST-007: MVP Testing & E2E (depends on GST-006)
```

### Phase 2: Advanced Features (2-3 settimane)
**Focus**: Dadi, carte, note private, offline PWA

**Issue Dependencies**:
```
EPIC-GST-001
├── GST-008: Dice Roller Component & Backend
├── GST-009: Card Deck System (Standard + Custom)
├── GST-010: Private Notes with Obscurement
├── GST-011: Timer & Random Tools (Coin Flip, Wheel)
├── GST-012: Offline PWA & Service Worker
└── GST-013: Session Sharing (PDF Export, Social)
```

---

## 🎨 Technical Specifications

### Bounded Context: GameSession
**New bounded context** con pattern CQRS + Event-Driven

**Database Schema** (7 tabelle):
- `game_sessions` - Sessioni principali
- `session_participants` - Giocatori in sessione
- `score_entries` - Entry punteggi per round/categoria
- `player_notes` - Note private/condivise
- `dice_rolls` - Storico lanci dadi
- `card_draws` - Storico pescate carte
- `game_decks` - Mazzi custom/game-specific

### API Endpoints (CQRS)

**Commands**:
- `POST /api/v1/sessions` - CreateSessionCommand
- `PUT /api/v1/sessions/{id}/scores` - UpdateScoreCommand
- `PUT /api/v1/sessions/{id}/finalize` - FinalizeSessionCommand
- `POST /api/v1/sessions/{id}/notes` - AddNoteCommand
- `POST /api/v1/sessions/{id}/dice` - RollDiceCommand
- `POST /api/v1/sessions/{id}/cards/draw` - DrawCardCommand

**Queries**:
- `GET /api/v1/sessions/active` - GetActiveSessionQuery
- `GET /api/v1/sessions/{id}/scoreboard` - GetScoreboardQuery
- `GET /api/v1/sessions/history` - GetSessionHistoryQuery
- `GET /api/v1/sessions/{id}/details` - GetSessionDetailsQuery

**Real-Time**:
- `GET /api/v1/sessions/{id}/stream` - SSE event stream

### Frontend Routes

**Generic Toolkit**:
- `/toolkit` - Landing page (create/join)
- `/toolkit/{sessionId}` - Active session page
- `/toolkit/history` - Storico sessioni generiche

**Game-Specific Toolkit**:
- `/library/games/{gameId}/toolkit` - Landing con template
- `/library/games/{gameId}/toolkit/{sessionId}` - Active session

### UI Components (✅ Already Created)
- `SessionHeader` - Header con session code, status, actions
- `ParticipantCard` - Card giocatore con rank, score, real-time indicators
- `ScoreInput` - Form input punteggi con quick actions
- `Scoreboard` - Tabella punteggi con round breakdown

**Location**: `apps/web/src/components/session/`

---

## 📊 Success Metrics

### MVP Success Criteria
- [ ] Utenti possono creare sessione generic in <10 secondi
- [ ] Real-time sync funziona con <100ms latency
- [ ] Mobile-first: 80%+ utilizzo da smartphone
- [ ] Zero test failures su integration + E2E suite

### Phase 2 Success Criteria
- [ ] 50%+ sessioni usano almeno 1 random tool (dadi/carte)
- [ ] Offline mode funziona senza connessione
- [ ] Session sharing: 30%+ utenti condividono risultati

### Analytics da Tracciare
- Numero sessioni create (generic vs game-specific)
- Tempo medio sessione
- Numero medio partecipanti
- Feature usage: dadi, carte, note
- Real-time collaboration: numero device concorrenti

---

## 🚀 Rollout Plan

### Week 1-2: Backend Foundation (GST-001, GST-002)
- Database schema + migrations
- CQRS commands/queries
- Unit + integration tests

### Week 2-3: Real-Time Infrastructure (GST-003)
- SSE service
- Event handlers
- Redis pub/sub integration

### Week 3-4: Frontend Integration (GST-004, GST-005, GST-006)
- Generic toolkit routes
- Game-specific integration
- UserLibrary linking
- Session history

### Week 4: Testing & Polish (GST-007)
- E2E Playwright tests
- Bug fixes
- Performance optimization

### Week 5-6: Phase 2 Features (GST-008 → GST-013)
- Dice roller
- Card decks
- Notes system
- PWA offline mode

---

## 🔗 Related Resources

### Documentation
- **Technical Spec**: `claudedocs/game-session-toolkit-ui-components.md`
- **Component README**: `apps/web/src/components/session/README.md`
- **Usage Examples**: `apps/web/src/components/session/EXAMPLES.md`
- **Demo Page**: `/toolkit/demo`

### Design Assets
- **UI Components**: `apps/web/src/components/session/`
- **Custom CSS**: `apps/web/src/components/session/session-toolkit.css`
- **Color Palette**: Amber (#D97706), Orange (#EA580C), Gold (#FCD34D)

### Architecture References
- **CQRS Pattern**: `docs/01-architecture/patterns/cqrs.md`
- **Event-Driven**: `docs/01-architecture/patterns/event-driven.md`
- **Real-Time SSE**: Context7 MCP - SSE patterns

---

## ⚠️ Risks & Mitigations

| Risk | Probabilità | Impatto | Mitigazione |
|------|-------------|---------|-------------|
| **Real-time sync conflicts** (concurrent updates) | Media | Alto | Optimistic concurrency + RowVersion, conflict resolution UI |
| **Offline-first complexity** | Alta | Medio | MVP online-only, Phase 2 PWA con service worker |
| **SSE browser compatibility** | Bassa | Medio | Polyfill EventSource, fallback polling |
| **Database JSONB performance** (custom dice/cards) | Media | Medio | Index JSONB columns, denormalization se necessario |
| **Mobile real-time battery drain** | Bassa | Basso | SSE auto-reconnect con exponential backoff |

---

## 🎯 Definition of Done (Epic)

Epic considerata completa quando:

**MVP (Phase 1)**:
- [x] UI components creati e documentati
- [ ] Backend bounded context completamente implementato
- [ ] Database schema deployed con migrations
- [ ] CQRS commands/queries funzionanti
- [ ] SSE real-time sync operativo
- [ ] Generic toolkit accessibile da navbar
- [ ] Game-specific toolkit nelle library cards
- [ ] Session history con link a UserLibrary
- [ ] 90%+ test coverage backend, 85%+ frontend
- [ ] E2E tests Playwright per critical paths
- [ ] Mobile responsive testato su iPhone/Android
- [ ] Dark mode completamente supportato
- [ ] Accessibility WCAG 2.1 Level AA

**Phase 2 (Full Features)**:
- [ ] Dice roller funzionante (d4-d100 + custom)
- [ ] Card deck system (Standard52 + custom + database)
- [ ] Private notes con oscuramento
- [ ] Timer/coin flip/wheel implementati
- [ ] Offline PWA con service worker
- [ ] Session sharing (PDF, PNG, social)
- [ ] Analytics dashboard per session stats

---

## 📝 Notes

### Design Decisions
- **SSE over WebSockets**: Più semplice, auto-reconnect, compatibile HTTP/2
- **Bounded Context Separato**: Complessità sufficiente per giustificare (real-time, persistence, analytics)
- **Mobile-First**: 80%+ utilizzo atteso da smartphone durante partite fisiche
- **Analog Gaming Aesthetic**: Differenziazione visiva forte vs generic scorekeepers

### Future Enhancements (Post-Epic)
- AI-powered score suggestions (based on game rules)
- Voice input per hands-free scoring
- NFC/QR code per quick join
- Bluetooth sync tra device vicini (offline multiplayer)
- Integration con Board Game Geek API per regole/setup

---

**Epic Owner**: TBD
**Stakeholders**: Product, Engineering, Design
**Status**: 🟡 Planning
**Next Action**: Create individual issues GST-001 through GST-013

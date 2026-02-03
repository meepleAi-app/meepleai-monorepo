# Update Epic #3167 GST with completed checkboxes
$newBody = @"
# Epic: Game Session Toolkit - Collaborative Scorekeeper

**Epic ID**: EPIC-GST-001
**Priority**: High
**Status**: 🟡 Backend Complete (4/7), Frontend In Progress
**Estimated Effort**: 3-4 settimane (MVP), 5-6 settimane (Full Features)
**Created**: 2026-01-29
**Updated**: 2026-01-31

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

### Phase 1: MVP - Core Scorekeeper (2-3 settimane) - 57% Complete

**Issue Dependencies**:
\`\`\`
EPIC-GST-001
├── ✅ GST-001: Backend Bounded Context & Database Schema
├── ✅ GST-002: Backend CQRS Commands & Queries
├── ✅ GST-003: Backend Real-Time SSE Infrastructure
├── ✅ GST-004: Frontend Generic Toolkit Routes
├── ⏳ GST-005: Frontend Game-Specific Integration
├── ⏳ GST-006: Session History & UserLibrary Link
└── ⏳ GST-007: MVP Testing & E2E
\`\`\`

**Completed** (4/7):
- [x] #3160 GST-001: Backend Bounded Context & Database Schema ✅
- [x] #3161 GST-002: Backend CQRS Commands & Queries ✅
- [x] #3162 GST-003: Backend Real-Time SSE Infrastructure ✅
- [x] #3163 GST-004: Frontend Generic Toolkit Routes ✅

**In Progress** (3/7):
- [ ] #3164 GST-005: Frontend Game-Specific Integration
- [ ] #3165 GST-006: Session History & UserLibrary Link
- [ ] #3166 GST-007: MVP Testing & E2E

### Phase 2: Advanced Features (2-3 settimane) - Not Started

**Issue Dependencies**:
\`\`\`
EPIC-GST-001
├── GST-008: Dice Roller Component & Backend
├── GST-009: Card Deck System (Standard + Custom)
├── GST-010: Private Notes with Obscurement
├── GST-011: Timer & Random Tools (Coin Flip, Wheel)
├── GST-012: Offline PWA & Service Worker
└── GST-013: Session Sharing (PDF Export, Social)
\`\`\`

**Planned**:
- [ ] #TBD GST-008: Dice Roller Component & Backend
- [ ] #TBD GST-009: Card Deck System
- [ ] #TBD GST-010: Private Notes
- [ ] #TBD GST-011: Random Tools
- [ ] #TBD GST-012: Offline PWA
- [ ] #TBD GST-013: Session Sharing

---

## 🎨 Technical Specifications

### Bounded Context: GameSession
**New bounded context** con pattern CQRS + Event-Driven

**Database Schema** (7 tabelle):
- ✅ \`game_sessions\` - Sessioni principali (GST-001 ✅)
- ✅ \`session_participants\` - Giocatori in sessione (GST-001 ✅)
- ✅ \`score_entries\` - Entry punteggi (GST-001 ✅)
- ✅ \`game_session_states\` - State snapshots (GST-001 ✅)
- ⏳ \`player_notes\` - Note private/condivise (Phase 2)
- ⏳ \`dice_rolls\` - Storico lanci (Phase 2)
- ⏳ \`card_draws\` - Storico pescate (Phase 2)

### API Endpoints (CQRS)

**Commands** (GST-002 ✅):
- ✅ POST /api/v1/game-sessions - CreateSessionCommand
- ✅ PUT /api/v1/game-sessions/{id}/state - UpdateSessionStateCommand
- ✅ POST /api/v1/game-sessions/{id}/finalize - FinalizeSessionCommand
- ⏳ POST /api/v1/game-sessions/{id}/notes - AddNoteCommand (Phase 2)

**Queries** (GST-002 ✅):
- ✅ GET /api/v1/game-sessions/active - GetActiveSessionsQuery
- ✅ GET /api/v1/game-sessions/{id} - GetSessionByIdQuery
- ✅ GET /api/v1/game-sessions/history - GetSessionHistoryQuery

**Real-Time** (GST-003 ✅):
- ✅ GET /api/v1/game-sessions/{id}/stream - SSE event stream

### Frontend Routes

**Generic Toolkit** (GST-004 ✅):
- ✅ /toolkit - Landing page
- ✅ /toolkit/{sessionId} - Active session page
- ⏳ /toolkit/history - Storico sessioni (GST-006)

**Game-Specific Toolkit** (GST-005):
- ⏳ /library/games/{gameId}/toolkit - Landing con template
- ⏳ /library/games/{gameId}/toolkit/{sessionId} - Active session

---

## 📊 Progress Summary

### Completion Status
- **Phase 1 MVP**: 57% (4/7 issues)
- **Phase 2 Advanced**: 0% (0/6 issues - not started)
- **Overall Epic**: 40% (4/10 planned issues)

### Recent Achievements (2026-01-30)
- ✅ GST-001: Complete GameSession bounded context
- ✅ GST-002: All CQRS commands/queries implemented
- ✅ GST-003: SSE infrastructure operational
- ✅ GST-004: Generic toolkit routes created
- 🎊 Backend foundation 100% complete!

### Integration with AI Agent System
- ✅ AgentSession uses game_sessions table (FK dependency)
- ✅ Agent state sync with game events (AGT-015)
- ✅ Session context available for agent prompts

---

## 🚀 Next Actions (Priority Order)

1. **GST-005**: Game-Specific Toolkit Integration (P1-High)
2. **GST-006**: Session History & UserLibrary Link (P1-High)
3. **GST-007**: MVP Testing & E2E (P0-Critical)

**Estimated**: 1-2 weeks to complete Phase 1 MVP

---

## 📝 Notes

### Design Decisions
- **SSE over WebSockets**: Più semplice, auto-reconnect, compatibile HTTP/2
- **Bounded Context Separato**: Complessità sufficiente per giustificare (real-time, persistence, analytics)
- **Mobile-First**: 80%+ utilizzo atteso da smartphone durante partite fisiche

### Dependencies Resolved
- ✅ game_sessions table now available for AgentSession FK
- ✅ SSE infrastructure used by AI Agent chat (#3187)
- ✅ State management patterns shared across contexts

---

**Epic Owner**: Backend Team (Phase 1), Full Stack Team (Phase 2)
**Stakeholders**: Product, Engineering, Design
**Status**: 🟡 In Progress - Backend Complete, Frontend 25%
**Next Milestone**: Phase 1 MVP Complete (3 issues remaining)
"@

Write-Host "[INFO] Updating Epic #3167 GST..." -ForegroundColor Cyan

try {
    gh issue edit 3167 --body $newBody
    Write-Host "[OK] Epic #3167 updated! Backend 4/7 complete" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Failed to update: $_" -ForegroundColor Red
}

# 🎮 Game Session Toolkit - Piano di Implementazione Completo

**Epic ID**: EPIC-GST-001
**Created**: 2026-01-29
**Status**: ✅ Ready for Sprint Planning

---

## 📋 Executive Summary

Piano di implementazione completo per il **Game Session Toolkit** di MeepleAI: sistema collaborativo multi-device per scorekeeper digitale, dadi virtuali, mazzi di carte, e gestione sessioni di gioco.

### Deliverables Creati

**Documentazione**:
- ✅ Epic principale (`claudedocs/game-session-toolkit-epic.md`)
- ✅ 7 Issue MVP dettagliate (`claudedocs/issues/GST-001` → `GST-007`)
- ✅ Specifica tecnica completa (`claudedocs/game-session-toolkit-ui-components.md`)
- ✅ UI Components production-ready (`apps/web/src/components/session/`)
- ✅ Component README (1,300 righe)
- ✅ Usage examples (600 righe)
- ✅ Demo page interattiva (`/toolkit/demo`)

**Codice UI** (già creato):
- ✅ SessionHeader.tsx (150 righe)
- ✅ ParticipantCard.tsx (220 righe)
- ✅ ScoreInput.tsx (280 righe)
- ✅ Scoreboard.tsx (380 righe)
- ✅ Custom CSS animations (200 righe)
- ✅ TypeScript types & barrel exports

**Totale**: ~4,000 righe di codice/documentazione

---

## 🎯 Epic Structure & Timeline

### Phase 1: MVP (2-3 settimane)

**Week 1**: Backend Foundation
```
┌─────────────┬─────────────┬─────────────┐
│   GST-001   │   GST-002   │   GST-003   │
│  5 giorni   │  5 giorni   │  3 giorni   │
│  Bounded    │    CQRS     │  SSE Real-  │
│  Context    │  Commands/  │   Time      │
│  Database   │  Queries    │  Infra      │
└─────────────┴─────────────┴─────────────┘
```

**Week 2**: Frontend Integration
```
┌─────────────┬─────────────┐
│   GST-004   │   GST-005   │
│  4 giorni   │  3 giorni   │
│  Generic    │  Game-      │
│  Toolkit    │  Specific   │
│  Routes     │  Templates  │
└─────────────┴─────────────┘
```

**Week 3**: History & Testing
```
┌─────────────┬─────────────┐
│   GST-006   │   GST-007   │
│  2 giorni   │  3 giorni   │
│  Session    │  MVP        │
│  History    │  Testing    │
│  UserLib    │  & QA       │
└─────────────┴─────────────┘
```

### Phase 2: Advanced Features (2-3 settimane)

**Week 4-5**: Random Tools
```
┌─────────────┬─────────────┬─────────────┐
│   GST-008   │   GST-009   │   GST-010   │
│  Dice       │   Card      │  Private    │
│  Roller     │   Decks     │  Notes      │
└─────────────┴─────────────┴─────────────┘
```

**Week 6**: Offline & Sharing
```
┌─────────────┬─────────────┬─────────────┐
│   GST-011   │   GST-012   │   GST-013   │
│  Timer &    │  Offline    │  Session    │
│  Random     │  PWA        │  Sharing    │
│  Tools      │  Service    │  PDF/PNG    │
└─────────────┴─────────────┴─────────────┘
```

---

## 📂 Issue Dependency Graph

```
EPIC-GST-001: Game Session Toolkit
│
├─── GST-001: Backend Bounded Context & Database ⏱️ 5d
│    └─── GST-002: Backend CQRS Commands & Queries ⏱️ 5d
│         └─── GST-003: Backend Real-Time SSE Infrastructure ⏱️ 3d
│              └─── GST-004: Frontend Generic Toolkit Routes ⏱️ 4d
│                   └─── GST-005: Frontend Game-Specific Integration ⏱️ 3d
│                        └─── GST-006: Session History & UserLibrary ⏱️ 2d
│                             └─── GST-007: MVP Testing & QA ⏱️ 3d
│
└─── Phase 2 (Post-MVP)
     ├─── GST-008: Dice Roller Component & Backend ⏱️ 3d
     ├─── GST-009: Card Deck System ⏱️ 4d
     ├─── GST-010: Private Notes with Obscurement ⏱️ 2d
     ├─── GST-011: Timer & Random Tools ⏱️ 2d
     ├─── GST-012: Offline PWA & Service Worker ⏱️ 5d
     └─── GST-013: Session Sharing (PDF, PNG, Social) ⏱️ 3d
```

**Total MVP**: 25 giorni (5 settimane con buffer)
**Total Phase 2**: 19 giorni (4 settimane)

---

## 🏗️ Architecture Overview

### Bounded Context: GameSession

**Domain Layer**:
- Entities: `Session`, `ScoreEntry`, `PlayerNote`, `DiceRoll`, `CardDraw`
- Value Objects: `ParticipantInfo`, `ScoreCalculation`, `SessionResult`
- Repositories: `ISessionRepository`, `IScoreEntryRepository`
- Events: `SessionCreatedEvent`, `ScoreUpdatedEvent`, `SessionFinalizedEvent`

**Application Layer**:
- **Commands**: CreateSession, UpdateScore, AddParticipant, AddNote, FinalizeSession
- **Queries**: GetActiveSession, GetScoreboard, GetSessionDetails, GetSessionHistory
- **Validators**: FluentValidation per ogni command/query
- **Handlers**: MediatR pattern per CQRS

**Infrastructure Layer**:
- **Persistence**: SessionDbContext, SessionRepository, ScoreEntryRepository
- **Services**: SessionSyncService (SSE pub/sub)
- **Events**: Event handlers per real-time broadcasting

### Database Schema (7 tabelle)

```sql
game_sessions           -- Core session metadata
  ├── session_participants  -- Giocatori in sessione
  ├── score_entries         -- Entry punteggi per round/categoria
  ├── player_notes          -- Note private/condivise
  ├── dice_rolls            -- Storico lanci dadi (Phase 2)
  ├── card_draws            -- Storico pescate carte (Phase 2)
  └── game_decks            -- Mazzi custom (Phase 2)
```

**Integrazione UserLibrary**:
- Link: `games_played.session_id` → `game_sessions.id`
- Auto-creazione entry dopo `FinalizeSession`

### Frontend Routes

**Generic Toolkit** (Navbar):
- `/toolkit` - Landing (create/join)
- `/toolkit/{sessionId}` - Active session con SSE
- `/toolkit/history` - Storico sessioni

**Game-Specific Toolkit** (Library):
- `/library/games/{gameId}/toolkit` - Template landing
- `/library/games/{gameId}/toolkit/{sessionId}` - Active session

### UI Components (✅ Already Created)

**Location**: `apps/web/src/components/session/`

1. **SessionHeader** - Sticky header con session code, status, actions
2. **ParticipantCard** - Card giocatore (2 variants: compact/full)
3. **ScoreInput** - Form punteggi con quick actions (-5, -1, +1, +5)
4. **Scoreboard** - Tabella punteggi con round breakdown, real-time sync

**Design System**: "Analog Gaming Digitized"
- Wood grain textures (SVG noise filters)
- Embossed vintage numbers
- Warm amber/orange palette
- Physics-based animations
- Mobile-first responsive

---

## 📊 Issue Summary Table

| ID | Title | Priority | Effort | Dependencies | Status |
|----|-------|----------|--------|--------------|--------|
| GST-001 | Backend Bounded Context & Database | P0 | 5d | None | 🟡 Todo |
| GST-002 | Backend CQRS Commands & Queries | P0 | 5d | GST-001 | 🟡 Todo |
| GST-003 | Backend Real-Time SSE Infrastructure | P0 | 3d | GST-002 | 🟡 Todo |
| GST-004 | Frontend Generic Toolkit Routes | P0 | 4d | GST-003 | 🟡 Todo |
| GST-005 | Frontend Game-Specific Integration | P1 | 3d | GST-004 | 🟡 Todo |
| GST-006 | Session History & UserLibrary | P1 | 2d | GST-005 | 🟡 Todo |
| GST-007 | MVP Testing & QA | P0 | 3d | GST-006 | 🟡 Todo |
| **MVP Total** | | | **25d** | | |
| GST-008 | Dice Roller Component & Backend | P2 | 3d | GST-007 | ⏸️ Backlog |
| GST-009 | Card Deck System | P2 | 4d | GST-007 | ⏸️ Backlog |
| GST-010 | Private Notes with Obscurement | P2 | 2d | GST-007 | ⏸️ Backlog |
| GST-011 | Timer & Random Tools | P2 | 2d | GST-007 | ⏸️ Backlog |
| GST-012 | Offline PWA & Service Worker | P2 | 5d | GST-007 | ⏸️ Backlog |
| GST-013 | Session Sharing (PDF, PNG, Social) | P2 | 3d | GST-007 | ⏸️ Backlog |
| **Phase 2 Total** | | | **19d** | | |

---

## 🎯 Success Metrics

### MVP Targets

**Adoption**:
- [ ] 100+ sessioni create in prima settimana
- [ ] 50%+ utenti usano toolkit almeno 1 volta/mese
- [ ] 80%+ utilizzo da mobile (iPhone/Android)

**Performance**:
- [ ] SSE latency <100ms (95th percentile)
- [ ] Page load <2s (Lighthouse Performance ≥90)
- [ ] Zero downtime deployments

**Quality**:
- [ ] 90%+ test coverage backend
- [ ] 85%+ test coverage frontend
- [ ] WCAG 2.1 Level AA accessibility
- [ ] Zero critical security issues

### Phase 2 Targets

**Feature Usage**:
- [ ] 50%+ sessioni usano dadi virtuali
- [ ] 30%+ sessioni usano carte custom
- [ ] 20%+ sessioni condivise (PDF/social)

**Offline**:
- [ ] PWA installata da 30%+ utenti mobile
- [ ] Offline mode funzionante 100% del tempo

---

## 🔒 Security Considerations

### Authentication & Authorization
- [ ] Session access verificato (owner or participant)
- [ ] SSE stream richiede valid session token
- [ ] Score updates validati contro session membership
- [ ] Private notes accessibili solo a owner

### Data Protection
- [ ] Soft delete per sessioni (GDPR compliance)
- [ ] Session codes non-guessable (6 caratteri alfanumerici, no I/O/0/1)
- [ ] Rate limiting su CreateSession (max 10/hour/user)
- [ ] XSS protection su note content (HTML sanitization)

### Infrastructure
- [ ] Secrets management: JWT, database passwords in .secret files
- [ ] HTTPS only (Traefik termination)
- [ ] CSP headers per XSS mitigation
- [ ] Semgrep + detect-secrets pre-commit hooks

---

## 📚 Resources & Documentation

### Technical Specs
- **Epic Overview**: `claudedocs/game-session-toolkit-epic.md`
- **UI Components**: `claudedocs/game-session-toolkit-ui-components.md`
- **Component README**: `apps/web/src/components/session/README.md`
- **Usage Examples**: `apps/web/src/components/session/EXAMPLES.md`

### Issue Tracking
- **Issue Directory**: `claudedocs/issues/GST-001` → `GST-007`
- **GitHub Project**: TBD (create GitHub project board)

### Demo & Preview
- **Demo Page**: http://localhost:3000/toolkit/demo (dopo `pnpm dev`)
- **Component Storybook**: TBD (optional)

### Architecture References
- **DDD Patterns**: `docs/01-architecture/patterns/domain-driven-design.md`
- **CQRS Pattern**: `docs/01-architecture/patterns/cqrs.md`
- **SSE Real-Time**: Context7 MCP - Server-Sent Events patterns
- **Testing Guide**: `docs/05-testing/backend/backend-testing-patterns.md`

---

## ⚠️ Risks & Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **Real-time sync conflicts** | Alto | Media | Optimistic concurrency + RowVersion, conflict resolution UI |
| **SSE browser compatibility** | Medio | Bassa | EventSource polyfill, fallback polling |
| **Database JSONB performance** | Medio | Media | Index JSONB columns, denormalization se necessario |
| **Mobile battery drain (SSE)** | Basso | Bassa | Auto-reconnect con exponential backoff, heartbeat optimization |
| **Scope creep (Phase 2)** | Alto | Alta | MVP strict scope, Phase 2 post-production validation |

---

## 🚀 Next Steps

### Immediate Actions (Today)

1. **Create GitHub Issues**:
   - [ ] Import issue files in GitHub (GST-001 → GST-007)
   - [ ] Create Epic in GitHub Projects
   - [ ] Assign to sprint team

2. **Sprint Planning**:
   - [ ] Review issue dependencies
   - [ ] Assign developers to issues
   - [ ] Set sprint dates (2-week sprints)

3. **Environment Setup**:
   - [ ] Verify dev environment ready (PostgreSQL, Redis optional)
   - [ ] Review secret management (`.secret` files)
   - [ ] Confirm CI/CD pipeline supports new bounded context

### Week 1 Kickoff

**Sprint 1 Goals**:
- [ ] Complete GST-001 (Bounded Context & Database)
- [ ] Start GST-002 (CQRS Commands/Queries)
- [ ] Daily standups: SSE architecture discussion

**Team Coordination**:
- [ ] Backend team: Focus on GST-001, GST-002
- [ ] Frontend team: Review UI components, prepare API integration
- [ ] QA team: Prepare test strategy for GST-007

---

## 📝 Final Checklist

### Pre-Sprint Preparation

**Documentation**:
- [x] Epic created e documentata
- [x] 7 Issue MVP dettagliate
- [x] Technical spec completa
- [x] UI components + demo creati
- [ ] GitHub project board creato
- [ ] Team briefing scheduled

**Code**:
- [x] UI components production-ready
- [x] Component README + examples
- [x] Demo page funzionante
- [ ] Backend skeleton setup (GST-001 ready to start)

**Infrastructure**:
- [ ] Dev environment PostgreSQL ready
- [ ] Redis optional setup documented
- [ ] CI/CD pipeline verificato
- [ ] Secret management reviewed

**Team**:
- [ ] Developers assigned to issues
- [ ] Sprint dates confirmed (2-week sprints)
- [ ] Kickoff meeting scheduled
- [ ] Slack channel #game-toolkit created

---

## 🎉 Conclusion

**Status**: ✅ **Ready for Sprint Planning**

Tutti i deliverables per l'epic Game Session Toolkit sono completi:
- Epic dettagliata con roadmap completa
- 7 Issue MVP con task granulari e acceptance criteria
- UI components production-ready con design system "Analog Gaming Digitized"
- Demo page interattiva per preview
- Documentazione tecnica completa (4,000+ righe)

**Prossimo step**: Importare issue in GitHub, assegnare team, e iniziare Sprint 1 con GST-001 (Backend Bounded Context & Database).

---

**Last Updated**: 2026-01-29
**Epic Owner**: TBD
**Status**: 🟢 Ready to Start
**Estimated MVP Completion**: 3-4 settimane (5 settimane con buffer)

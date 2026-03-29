# MeepleAI User Stories Tracking

> **Last updated**: 2026-03-26
> **Product Spec**: [docs/architecture/overview/product-specification.md](../architecture/overview/product-specification.md)
> **Alpha Scope**: [docs/alpha-zero-scope.md](../alpha-zero-scope.md)

---

## Legend

| Status | Meaning |
|--------|---------|
| ✅ Complete | Frontend + Backend fully working |
| 🟡 Backend Only | Backend ready, frontend dormant/hidden |
| 🔵 Planned | Implementation plan created, not started |
| ⬜ Not Started | No implementation yet |

---

## Alpha Zero (Implemented)

### Authentication (BC: Authentication)

| ID | User Story | Status | PR/Notes |
|----|-----------|--------|----------|
| US-1 | Come utente, voglio registrarmi con email/password | ✅ Complete | + welcome, verify-email, verification-pending |
| US-2 | Come utente, voglio fare login con OAuth (Google) | ✅ Complete | Google OAuth + callback flow |
| US-3 | Come utente, voglio resettare la password | ✅ Complete | reset-password flow |
| US-4 | Come admin, voglio gestire access request (invite-only) | ✅ Complete | approve/reject/bulk operations |
| US-5 | Come admin, voglio gestire invitations | ✅ Complete | InvitationToken + game suggestions |
| US-6 | Come utente, voglio gestire API keys | ✅ Complete | CRUD + usage logging |

### Game Library (BC: UserLibrary + GameManagement)

| ID | User Story | Status | PR/Notes |
|----|-----------|--------|----------|
| US-7 | Come utente, voglio aggiungere giochi alla mia libreria (via BGG) | ✅ Complete | BGG search + import |
| US-8 | Come utente, voglio gestire collezioni di giochi | ✅ Complete | CRUD + bulk add/remove |
| US-9 | Come utente, voglio creare giochi privati custom | ✅ Complete | Private games + toolkit config |

### RAG / Knowledge Base (BC: DocumentProcessing + KnowledgeBase)

| ID | User Story | Status | PR/Notes |
|----|-----------|--------|----------|
| US-12 | Come utente, voglio uploadare PDF di regolamenti | ✅ Complete | Upload + chunking + OCR |
| US-13 | Come utente, voglio chattare con l'AI sui regolamenti | ✅ Complete | RAG chat + citations |
| US-14 | Come admin, voglio monitorare la pipeline RAG | ✅ Complete | Dashboard metriche reali |
| US-15 | Come admin, voglio gestire embeddings e vettori | ✅ Complete | Embedding flow + vector search |
| US-16 | Come utente, voglio risposte in dual-language (IT/EN) | ✅ Complete | PR#123 — semantic chunking + language detection |

### Administration (BC: Administration + SystemConfiguration)

| ID | User Story | Status | PR/Notes |
|----|-----------|--------|----------|
| US-17 | Come admin, voglio gestire utenti e ruoli | ✅ Complete | CRUD + SuperAdmin + bulk ops |
| US-18 | Come admin, voglio vedere analytics (MAU, chat, PDF) | ✅ Complete | Dashboard overview + activity |
| US-19 | Come admin, voglio gestire il catalogo giochi condivisi | ✅ Complete | CRUD + BGG import + seeding |
| US-20 | Come admin, voglio il RAG wizard per gioco | ✅ Complete | Upload → embed → agent setup |
| US-21 | Come admin, voglio gestire feature flags | ✅ Complete | CRUD + history + rollback |

### UI/UX (BC: Frontend)

| ID | User Story | Status | PR/Notes |
|----|-----------|--------|----------|
| US-22 | Come utente, voglio navigazione unificata (sidebar + tabs) | ✅ Complete | PR#124 — UnifiedShell + emoji icons |
| US-23 | Come utente, voglio MeepleCard per tutti gli entity display | ✅ Complete | 5 varianti + design tokens |
| US-24 | Come utente, voglio layout responsive mobile-friendly | ✅ Complete | Layout redesign + library redesign |

---

## Priority 1: Attivazione Feature Dormant (Backend Ready → Frontend)

### Sessioni & Partite

| ID | User Story | Status | BC | Backend | Plan |
|----|-----------|--------|-----|---------|------|
| US-30 | Come utente, voglio creare e gestire sessioni di gioco live | 🔵 Planned | SessionTracking | ✅ 28 cmd + 12 query | [Plan](../superpowers/plans/2026-03-26-us30-live-sessions.md) |
| US-32 | Come utente, voglio tracciare le partite giocate (play records) | 🔵 Planned | GameManagement | ✅ PlayRecord + RecordPlayer | [Plan](../superpowers/plans/2026-03-26-us32-play-records.md) |
| US-31 | Come utente, voglio pianificare serate di gioco (game nights) | 🔵 Planned | GameManagement | ✅ GameNightEvent + RSVP | [Plan](../superpowers/plans/2026-03-26-us31-game-nights.md) |

### Engagement & Power Users

| ID | User Story | Status | BC | Backend | Plan |
|----|-----------|--------|-----|---------|------|
| US-41 | Come utente, voglio ricevere notifiche (email, push, in-app) | 🔵 Planned | UserNotifications | ✅ 24 cmd + 18 query | [Plan](../superpowers/plans/2026-03-26-us41-notifications.md) |
| US-33 | Come utente, voglio esplorare e personalizzare agenti AI | 🔵 Planned | KnowledgeBase | ✅ AgentDefinition CRUD | [Plan](../superpowers/plans/2026-03-26-us33-agent-browser.md) |

---

## Priority 2: Feature Parziali da Completare

| ID | User Story | Status | BC | Backend | Plan |
|----|-----------|--------|-----|---------|------|
| US-53 | Come utente, voglio house rules della community (upvote/downvote) | 🔵 Planned | AgentMemory | ✅ HouseRule entity | [Plan](../superpowers/plans/2026-03-26-us53-house-rules.md) |
| US-10 | Come utente, voglio gestire la mia wishlist di giochi | 🔵 Planned | UserLibrary | ✅ WishlistItem CRUD | [Plan](../superpowers/plans/2026-03-26-us10-wishlist.md) |
| US-43 | Come utente, voglio vedere relazioni tra giochi (espansioni, varianti) | 🔵 Planned | EntityRelationships | ✅ EntityLink + BGG import | [Plan](../superpowers/plans/2026-03-26-us43-entity-relationships.md) |

---

## Priority 3: Nuove Feature (V2) — Non Pianificate

| ID | User Story | Status | BC | Complessità |
|----|-----------|--------|-----|-------------|
| US-50 | Come utente, voglio avversari AI per giochi supportati | ⬜ Not Started | (nuovo) | 🔴 Alta |
| US-51 | Come utente, voglio visualizzazione board 2D | ⬜ Not Started | (nuovo) | 🔴 Alta |
| US-52 | Come utente, voglio allegare foto del tavolo alla chat | ⬜ Not Started | KnowledgeBase | 🟡 Media |
| US-53 | Come utente, voglio house rules community | 🔵 Planned | AgentMemory | 🟢 Bassa |
| US-54 | Come utente, voglio workflow agents (email, calendar) | ⬜ Not Started | WorkflowIntegration | 🟡 Media |
| US-55 | Come utente, voglio ottimizzazione per italiano | ⬜ Not Started | KnowledgeBase | 🟡 Media |

---

## Dormant Features (Backend pronto, no piano ancora)

| ID | User Story | BC | Backend Status |
|----|-----------|-----|---------------|
| US-34 | A/B testing agenti | KnowledgeBase | AbTestSession + AbTestVariant |
| US-35 | Profilo giocatore con achievements | Gamification | Achievement + UserAchievement |
| US-36 | Playlists di giochi | GameManagement | GameNightPlaylist + PlaylistGame |
| US-37 | Rule disputes & FAQ | GameManagement | RuleDispute + RuleConflictFAQ |
| US-38 | Strategie di gioco | GameManagement | GameStrategy |
| US-39 | Whiteboard collaborativa | SessionTracking | Whiteboard + WhiteboardStroke |
| US-40 | Toolkit AI per sessione | GameToolkit | GenerateToolkitFromKb |
| US-42 | Workflow n8n integration | WorkflowIntegration | WorkflowDefinition |
| US-44 | Business simulations | BusinessSimulations | LedgerEntry + forecasts |

---

## Future Vision (V3+) — Non Pianificate

| ID | User Story | Complessità |
|----|-----------|-------------|
| US-60 | Tournament system (Swiss, brackets, leaderboard) | 🔴 Alta |
| US-61 | Social: friends, invites, activity feed | 🟡 Media |
| US-62 | Multiplayer real-time (WebSocket board sync) | 🔴 Alta |
| US-63 | Computer Vision (board photo recognition) | 🔴 Alta |
| US-64 | Gamification completa (XP, leveling, challenges) | 🟡 Media |
| US-70 | Adaptive AI (learns playstyle) | 🔴 Alta |
| US-71 | Content creation (AI-generated variants) | 🔴 Alta |
| US-72 | Publisher tools (B2B dashboard) | 🟡 Media |
| US-73 | Marketplace (premium AI models, DLC) | 🔴 Alta |

---

## Metrics

| Metric | Count |
|--------|-------|
| **Total User Stories** | 42 |
| **✅ Implemented** | 21 (50%) |
| **🔵 Planned** | 8 (19%) |
| **🟡 Backend Only (no plan)** | 9 (21%) |
| **⬜ Not Started** | 4 (10%) |

---

## Implementation Order

### Sprint N (Priority 1 — Core Game Loop)
1. US-30: Sessioni di gioco live
2. US-32: Play records
3. US-31: Game nights

### Sprint N+1 (Priority 1 — Engagement)
4. US-41: Notifiche utente
5. US-33: Agent browser

### Sprint N+2 (Priority 2 — Completamento)
6. US-53: House rules community
7. US-10: Wishlist frontend
8. US-43: Entity relationships UI

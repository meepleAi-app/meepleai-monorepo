# Plan: Game Library Detail Page V1

## Hypothesis
Implementare una pagina dettaglio gioco nella libreria personale che supporti due journey principali:
1. **Pre-Game**: Assistenza rapida (AI chat, checklist setup, regolamento)
2. **Post-Game**: Registrazione adattiva (quick 30s → dettagliata)

## Design Proposals
Sono state create 3 proposte di design distintive:
- **Action Hero**: Brutalist arcade style, focus su CTAs bold e immediate
- **Stats Dashboard**: Editorial analytics, bilanciamento dati + azioni
- **Contextual Wizard**: Organic flow, progressive disclosure guidata

## Expected Outcomes
- Epic con 15-20 issue suddivise per dominio (Backend, Frontend, AI, Testing)
- Struttura CQRS completa per backend API
- Component architecture React modulare e testabile
- Test coverage: Backend 90%+, Frontend 85%+
- Timeline stimata: 3-4 settimane (parallelo frontend/backend)

## Technical Stack
- Backend: .NET 9, ASP.NET Minimal APIs, MediatR, FluentValidation
- Frontend: Next.js 14, Tailwind CSS, shadcn/ui, Zustand
- AI: RAG con Qdrant, context-aware chat
- Testing: xUnit, Testcontainers, Vitest, Playwright

## Risks & Mitigation
- **Risk**: Complessità UI con 3 stati (nuovo/prestito/wishlist)
  - Mitigation: Component architecture modulare, state-driven rendering
- **Risk**: AI context-awareness richiede RAG ottimizzato
  - Mitigation: Caching aggressive, fallback a regolamento statico
- **Risk**: Adaptive registration può confondere utenti
  - Mitigation: Progressive disclosure chiara, skip sempre disponibile

## Architecture Decisions

### Backend Structure (CQRS + DDD)
```
BoundedContexts/UserLibrary/
├── Domain/
│   ├── Entities/
│   │   ├── UserGame.cs (aggregate root)
│   │   ├── GameSession.cs
│   │   └── GameChecklist.cs
│   ├── ValueObjects/
│   │   ├── GameState.cs (enum: Nuovo, InPrestito, Wishlist)
│   │   └── GameStats.cs
│   └── Repositories/
│       └── IUserGameRepository.cs
├── Application/
│   ├── Queries/
│   │   ├── GetGameDetailQuery.cs + Handler + Validator
│   │   └── GetGameChecklistQuery.cs + Handler + Validator
│   ├── Commands/
│   │   ├── UpdateGameStateCommand.cs + Handler + Validator
│   │   ├── RecordGameSessionCommand.cs + Handler + Validator
│   │   └── SendLoanReminderCommand.cs + Handler + Validator
│   └── DTOs/
│       ├── GameDetailDto.cs
│       ├── GameSessionDto.cs
│       └── GameChecklistDto.cs
└── Infrastructure/
    ├── Persistence/
    │   └── UserGameRepository.cs
    └── Services/
        └── LoanReminderService.cs
```

### Frontend Structure (Feature-Based)
```
apps/web/src/
├── app/
│   └── games/
│       └── [id]/
│           └── page.tsx (GameDetailPage)
├── components/
│   └── game-detail/
│       ├── GameHeroSection.tsx
│       ├── StatsGrid.tsx
│       ├── PrimaryActionBar.tsx
│       ├── ContextualStateCard.tsx
│       ├── StickyBottomBar.tsx
│       ├── AdaptiveRegistrationModal.tsx
│       └── SetupChecklistDrawer.tsx
├── lib/
│   ├── stores/
│   │   └── game-detail-store.ts (Zustand)
│   └── api/
│       └── game-detail-api.ts
└── __tests__/
    ├── components/
    │   └── game-detail/
    └── e2e/
        └── game-detail.spec.ts
```

## Success Criteria

### Backend APIs
- [ ] GetGameDetailQuery returns complete game + stats + state
- [ ] UpdateGameStateCommand handles 3 states with validation
- [ ] RecordGameSessionCommand supports quick + detailed registration
- [ ] GetGameChecklistQuery returns game-specific setup steps
- [ ] SendLoanReminderCommand triggers notification

### Frontend Components
- [ ] GameDetailPage renders correctly for all 3 states
- [ ] StatsGrid displays 4 metrics with proper formatting
- [ ] PrimaryActionBar shows contextual CTAs based on state
- [ ] AdaptiveRegistrationModal: quick form → expand to detailed
- [ ] SetupChecklistDrawer: wizard mode skippable to checklist

### AI Integration
- [ ] Context-aware chat initialization (new user vs expert)
- [ ] Quick regolamento view integrated
- [ ] RAG query returns relevant rules sections

### Testing
- [ ] Backend: 90%+ coverage (unit + integration)
- [ ] Frontend: 85%+ coverage (component + E2E)
- [ ] E2E user journeys: pre-game, post-game, state transitions

## Timeline Estimate

**Week 1: Backend Foundation**
- Days 1-2: Domain entities + repositories
- Days 3-4: Queries (GetGameDetail, GetChecklist)
- Day 5: Commands (UpdateState, RecordSession)

**Week 2: Frontend Core**
- Days 1-2: Zustand store + API integration
- Days 3-4: Main components (Hero, Stats, ActionBar)
- Day 5: State-specific components

**Week 3: Advanced Features**
- Days 1-2: Adaptive registration modal
- Days 3-4: Setup checklist drawer + AI integration
- Day 5: Sticky bottom bar + secondary actions

**Week 4: Testing & Polish**
- Days 1-2: Backend unit + integration tests
- Days 3-4: Frontend component tests + E2E
- Day 5: Bug fixes, performance optimization

**Parallel Execution Opportunities:**
- Backend + Frontend teams work simultaneously (Weeks 1-2)
- Component development parallel after store ready (Week 2-3)
- Testing parallel across both layers (Week 4)

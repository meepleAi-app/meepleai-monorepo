# Epic: GameCarousel Integration & Production Readiness

**Epic ID**: EPIC-GC-001
**Priority**: P1
**Status**: Planning
**Target Sprint**: Current + 1 to Current + 3
**Estimated Effort**: 34 story points
**Created**: 2026-02-05
**Owner**: Frontend Team

---

## 📋 Epic Summary

Completare l'integrazione del componente **GameCarousel** nella codebase di produzione. Il prototipo è già stato sviluppato e validato visivamente. Questa epic copre il collegamento alle API reali, l'aggiunta di controlli utente, l'integrazione nella homepage, la documentazione Storybook, i test completi e la documentazione tecnica.

### Business Value
- **User Experience**: Navigazione immersiva della collezione giochi con esperienza "Netflix-like"
- **Engagement**: Aumento del tempo di permanenza sulla piattaforma (+15% target)
- **Discovery**: Miglioramento della scoperta di nuovi giochi attraverso sezioni curate

### Technical Value
- **Riutilizzabilità**: Componente utilizzabile in homepage, gallery, profilo, collezioni
- **Performance**: Lazy loading immagini, virtualizzazione per grandi dataset
- **Accessibilità**: Completo supporto WCAG AA (keyboard, screen reader, touch)

---

## 🎯 Goals (In Scope)

✅ Connessione alle API reali del backend per i dati dei giochi
✅ Controlli di ordinamento e filtro per l'utente
✅ Integrazione nella homepage con sezioni curate
✅ Storybook stories per documentazione e design system
✅ Test unitari (≥85% coverage) e test di integrazione
✅ Documentazione tecnica completa

---

## ❌ Non-Goals (Out of Scope)

❌ Raccomandazioni AI personalizzate (future epic)
❌ Infinite scroll con paginazione server-side (valutare in futuro)
❌ Animazioni avanzate 3D WebGL (mantenere CSS transforms)
❌ Caching avanzato con service worker (già gestito da React Query)

---

## 🎯 Success Criteria

### UX Metrics
- [ ] Tempo di caricamento carousel < 500ms (LCP)
- [ ] Smooth scrolling a 60fps su tutti i dispositivi target
- [ ] Zero layout shift durante il caricamento (CLS < 0.1)
- [ ] Navigazione completa via tastiera (WCAG AA)

### Technical Metrics
- [ ] Test coverage ≥ 85% (unit + integration)
- [ ] Bundle size increment < 15KB gzipped
- [ ] Lighthouse Performance score ≥ 90
- [ ] Zero errori TypeScript/ESLint

### Business Metrics
- [ ] Tempo medio sulla gallery page > 45 secondi
- [ ] Click-through rate su game cards > 8%
- [ ] Bounce rate gallery < 40%

---

## 📦 Issues Breakdown

### Issue #1: API Integration for Game Data
**Story Points**: 8
**Priority**: P0 (Critical Path)
**Dependencies**: None
**Bounded Context**: GameManagement, SharedGameCatalog

#### User Story
> Come utente della gallery, voglio vedere giochi reali dal catalogo, così da poter esplorare la collezione effettiva della piattaforma.

#### Acceptance Criteria
- [ ] Hook `useCarouselGames(options)` che fetcha dati dall'API
- [ ] Supporto per diverse sorgenti: featured, trending, by-category
- [ ] Loading states con skeleton appropriato
- [ ] Error handling con retry automatico
- [ ] Caching con React Query (stale-while-revalidate)

#### Technical Notes
```typescript
// Hook interface
interface UseCarouselGamesOptions {
  source: 'featured' | 'trending' | 'category' | 'user-library';
  categoryId?: string;
  limit?: number;
}

// Endpoints da utilizzare
GET /api/v1/shared-games?sort=rating&limit=10  // Featured
GET /api/v1/shared-games?sort=popularity&limit=10  // Trending
GET /api/v1/shared-games?category={id}&limit=10  // By category
GET /api/v1/library/games  // User library (authenticated)
```

#### BDD Scenarios
```gherkin
Feature: GameCarousel API Integration

Scenario: Load featured games successfully
  Given the API returns 7 featured games
  When the carousel renders
  Then I should see the first game in the center
  And I should see loading skeleton during fetch
  And the games should match the API response

Scenario: Handle API error gracefully
  Given the API returns a 500 error
  When the carousel renders
  Then I should see an error message
  And I should see a "Retry" button
  When I click "Retry"
  Then the carousel should attempt to reload
```

---

### Issue #2: Sorting and Filtering Controls
**Story Points**: 5
**Priority**: P1
**Dependencies**: Issue #1
**Bounded Context**: GameManagement

#### User Story
> Come utente, voglio poter ordinare e filtrare i giochi nel carousel, così da trovare più facilmente quello che cerco.

#### Acceptance Criteria
- [ ] Dropdown per ordinamento (Rating, Popolarità, Nome, Data)
- [ ] Filtri per categoria/genere (opzionale, collassabile)
- [ ] Persistenza preferenze in localStorage
- [ ] URL query params per condivisione filtri
- [ ] Animazione smooth durante cambio ordinamento

#### Technical Notes
```typescript
interface CarouselSortOption {
  value: 'rating' | 'popularity' | 'name' | 'date';
  label: string;
  icon?: LucideIcon;
}

// Component props extension
interface GameCarouselProps {
  // ... existing props
  sortable?: boolean;
  defaultSort?: CarouselSortOption['value'];
  onSortChange?: (sort: CarouselSortOption['value']) => void;
}
```

#### BDD Scenarios
```gherkin
Feature: Carousel Sorting Controls

Scenario: Sort by rating
  Given the carousel shows games sorted by popularity
  When I select "Rating" from the sort dropdown
  Then the games should reorder by rating (highest first)
  And the URL should update with ?sort=rating

Scenario: Persist sort preference
  Given I sorted games by "Name"
  When I refresh the page
  Then the carousel should still be sorted by "Name"
```

---

### Issue #3: Homepage Integration
**Story Points**: 5
**Priority**: P1
**Dependencies**: Issue #1
**Bounded Context**: UserLibrary, SharedGameCatalog

#### User Story
> Come visitatore della homepage, voglio vedere carousel di giochi in evidenza, così da scoprire immediatamente cosa offre la piattaforma.

#### Acceptance Criteria
- [ ] Sezione "Featured Games" nella homepage
- [ ] Sezione "Trending Now" (opzionale, se autenticato)
- [ ] Sezione "From Your Library" (solo se autenticato)
- [ ] Responsive layout con spacing appropriato
- [ ] Lazy loading per sezioni below-the-fold

#### Technical Notes
```tsx
// Homepage integration example
<section className="py-12">
  <GameCarousel
    games={featuredGames}
    title="Featured Games"
    subtitle="I migliori giochi selezionati per te"
    onGameSelect={handleGameSelect}
  />
</section>

{isAuthenticated && (
  <section className="py-12 bg-muted/30">
    <GameCarousel
      games={trendingGames}
      title="Trending Now"
      subtitle="I più giocati questa settimana"
    />
  </section>
)}
```

#### BDD Scenarios
```gherkin
Feature: Homepage Carousel Integration

Scenario: Display featured carousel to anonymous user
  Given I am not logged in
  When I visit the homepage
  Then I should see "Featured Games" carousel
  And I should NOT see "From Your Library" carousel

Scenario: Display personalized carousels to authenticated user
  Given I am logged in
  When I visit the homepage
  Then I should see "Featured Games" carousel
  And I should see "From Your Library" carousel
```

---

### Issue #4: Storybook Stories
**Story Points**: 3
**Priority**: P2
**Dependencies**: Issue #1
**Bounded Context**: N/A (Design System)

#### User Story
> Come sviluppatore, voglio documentazione Storybook del GameCarousel, così da poter esplorare varianti e props interattivamente.

#### Acceptance Criteria
- [ ] Story "Default" con mock data
- [ ] Story "Loading" con skeleton state
- [ ] Story "Error" con error state
- [ ] Story "Empty" senza giochi
- [ ] Story "With Sorting" con controlli ordinamento
- [ ] Story "Responsive" con viewport controls
- [ ] Controls panel per tutte le props
- [ ] Docs page con usage examples

#### Technical Notes
```typescript
// stories/game-carousel.stories.tsx
export default {
  title: 'Data Display/GameCarousel',
  component: GameCarousel,
  parameters: {
    docs: {
      description: {
        component: 'Immersive 3D carousel for browsing games',
      },
    },
  },
  argTypes: {
    games: { control: 'object' },
    autoPlay: { control: 'boolean' },
    showDots: { control: 'boolean' },
  },
} satisfies Meta<typeof GameCarousel>;
```

---

### Issue #5: Unit & Integration Tests
**Story Points**: 8
**Priority**: P1
**Dependencies**: Issue #1, Issue #2
**Bounded Context**: N/A (Quality)

#### User Story
> Come sviluppatore, voglio test completi del GameCarousel, così da garantire stabilità durante refactoring e nuove feature.

#### Acceptance Criteria
- [ ] Unit tests per componenti interni (NavButton, DotsIndicator)
- [ ] Unit tests per hooks (useSwipe, useKeyboardNavigation)
- [ ] Integration tests per navigazione carousel
- [ ] Integration tests per API integration
- [ ] Accessibility tests (axe-core)
- [ ] Snapshot tests per varianti principali
- [ ] Coverage report ≥ 85%

#### Technical Notes
```typescript
// Test file structure
__tests__/
├── game-carousel.test.tsx          // Unit tests
├── game-carousel.integration.test.tsx  // Integration
├── game-carousel.a11y.test.tsx     // Accessibility
└── game-carousel.snapshot.test.tsx // Snapshots

// Key test cases
describe('GameCarousel', () => {
  it('renders center card with featured variant');
  it('navigates to next game on arrow click');
  it('loops infinitely from last to first');
  it('supports keyboard navigation');
  it('supports touch swipe gestures');
  it('announces current game to screen readers');
  it('handles empty games array gracefully');
  it('shows loading skeleton during fetch');
});
```

#### BDD Scenarios
```gherkin
Feature: GameCarousel Navigation

Scenario: Navigate with keyboard
  Given the carousel is focused
  When I press the right arrow key
  Then the next game should be centered
  And screen reader should announce the new game

Scenario: Infinite loop navigation
  Given I am viewing the last game (7 of 7)
  When I click "Next"
  Then I should see game 1 of 7
```

---

### Issue #6: Technical Documentation
**Story Points**: 5
**Priority**: P2
**Dependencies**: All previous issues
**Bounded Context**: N/A (Documentation)

#### User Story
> Come nuovo sviluppatore del team, voglio documentazione tecnica completa, così da poter utilizzare e modificare il GameCarousel correttamente.

#### Acceptance Criteria
- [ ] README.md nel folder del componente
- [ ] JSDoc completo per tutte le props e types
- [ ] Architecture Decision Record (ADR) per scelte tecniche
- [ ] Guida migrazione da mock data a API reale
- [ ] Performance best practices documentate
- [ ] Troubleshooting section per problemi comuni

#### Technical Notes
```markdown
# docs/07-frontend/components/game-carousel.md

## Overview
## Props Reference
## Usage Examples
## API Integration
## Performance Considerations
## Accessibility
## Testing Guide
## Troubleshooting
```

---

## 🔗 Dependencies

### External Dependencies
- React Query (già installato)
- Lucide React icons (già installato)
- Tailwind CSS 4 (già configurato)

### Internal Dependencies
- `MeepleCard` component (✅ già implementato)
- Shared Games API endpoints (✅ già disponibili)
- User Library API endpoints (✅ già disponibili)
- Design tokens system (✅ già configurato)

---

## 🚀 Rollout Plan

### Sprint N+1 (Foundation)
| Issue | Story Points | Owner |
|-------|-------------|-------|
| #1 API Integration | 8 | TBD |
| #3 Homepage Integration | 5 | TBD |
| **Sprint Total** | **13** | |

### Sprint N+2 (Enhancement)
| Issue | Story Points | Owner |
|-------|-------------|-------|
| #2 Sorting Controls | 5 | TBD |
| #5 Tests | 8 | TBD |
| **Sprint Total** | **13** | |

### Sprint N+3 (Polish)
| Issue | Story Points | Owner |
|-------|-------------|-------|
| #4 Storybook | 3 | TBD |
| #6 Documentation | 5 | TBD |
| **Sprint Total** | **8** | |

---

## 📊 Metrics & KPIs

### Development Metrics
| Metric | Target | Tracking |
|--------|--------|----------|
| Test Coverage | ≥ 85% | Vitest coverage report |
| Bundle Size Delta | < 15KB | Webpack analyzer |
| TypeScript Errors | 0 | CI/CD pipeline |
| Accessibility Score | 100% | axe-core |

### User Metrics (Post-Launch)
| Metric | Target | Tracking |
|--------|--------|----------|
| Gallery Page Time | > 45s | Analytics |
| Card CTR | > 8% | Analytics |
| Bounce Rate | < 40% | Analytics |

---

## 🚨 Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| API response too slow | Medium | High | Implement optimistic UI + skeleton |
| Bundle size bloat | Low | Medium | Code splitting, tree shaking audit |
| Mobile performance | Medium | High | Test on real devices, reduce animations |
| Accessibility gaps | Low | High | axe-core in CI, manual testing |

---

## ✅ Definition of Done (Epic Level)

- [ ] Tutti i 6 issue completati e merged
- [ ] Test coverage ≥ 85% su tutto il componente
- [ ] Zero errori TypeScript/ESLint
- [ ] Storybook stories pubblicate
- [ ] Documentazione tecnica completa
- [ ] Performance audit passed (Lighthouse ≥ 90)
- [ ] Accessibility audit passed (axe-core 100%)
- [ ] Code review approvato da 2+ team members
- [ ] QA sign-off su tutti i browser target
- [ ] Product Owner approval

---

## 📚 Reference Materials

- **Prototipo**: `apps/web/src/components/ui/data-display/game-carousel.tsx`
- **Demo Page**: `apps/web/src/app/(public)/gallery/page.tsx`
- **MeepleCard Docs**: `docs/frontend/components/meeple-card.md`
- **Design System**: `docs/design-system/cards.md`
- **API Docs**: `http://localhost:8080/scalar/v1`

---

## 📝 Change Log

| Date | Author | Changes |
|------|--------|---------|
| 2026-02-05 | Claude Code | Epic created with 6 issues |

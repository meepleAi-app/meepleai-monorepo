# [TECH] GameCarousel Unit & Integration Tests

**Issue ID**: GC-005
**Epic**: EPIC-GC-001 (GameCarousel Integration)
**Story Points**: 8
**Priority**: P1
**Status**: To Do
**Assignee**: TBD

---

## 📋 User Story

> Come sviluppatore,
> Voglio test completi del GameCarousel,
> Così da garantire stabilità durante refactoring e nuove feature.

---

## 🎯 Acceptance Criteria

- [ ] Unit tests per GameCarousel component (≥85% coverage)
- [ ] Unit tests per subcomponents (NavButton, DotsIndicator)
- [ ] Unit tests per hooks (useSwipe, useKeyboardNavigation, useCarouselGames)
- [ ] Integration tests per navigazione completa
- [ ] Integration tests per API integration con MSW
- [ ] Accessibility tests con axe-core
- [ ] Snapshot tests per varianti principali
- [ ] Coverage report generato e verificato
- [ ] Tests eseguibili in CI/CD

---

## 🧪 Test Cases

### Unit Tests - Component
```typescript
// __tests__/game-carousel.test.tsx

describe('GameCarousel', () => {
  describe('Rendering', () => {
    it('renders center card with featured variant');
    it('renders side cards with grid variant');
    it('applies correct scale to side cards');
    it('renders title and subtitle when provided');
    it('renders navigation arrows');
    it('renders dot indicators when showDots is true');
    it('renders skeleton when loading');
    it('renders empty state when games array is empty');
  });

  describe('Navigation', () => {
    it('moves to next game when clicking next arrow');
    it('moves to previous game when clicking prev arrow');
    it('loops from last to first game');
    it('loops from first to last game');
    it('clicking side card brings it to center');
    it('clicking center card triggers onGameSelect');
    it('disables navigation during animation');
  });

  describe('Keyboard Navigation', () => {
    it('moves to next game on ArrowRight press');
    it('moves to previous game on ArrowLeft press');
    it('only responds to keyboard when focused');
  });

  describe('Touch/Swipe', () => {
    it('moves to next game on swipe left');
    it('moves to previous game on swipe right');
    it('ignores swipes below threshold');
  });

  describe('Auto-play', () => {
    it('advances automatically when autoPlay is true');
    it('pauses on hover');
    it('pauses on focus');
    it('resumes after mouse leave');
    it('respects autoPlayInterval');
  });

  describe('Accessibility', () => {
    it('has correct aria-label on carousel');
    it('announces current game to screen readers');
    it('navigation buttons have accessible names');
    it('dots have correct aria-selected state');
  });
});
```

### Unit Tests - Hooks
```typescript
// __tests__/hooks/useCarouselGames.test.ts

describe('useCarouselGames', () => {
  it('fetches featured games successfully');
  it('fetches trending games successfully');
  it('fetches category games with categoryId');
  it('fetches user library games when authenticated');
  it('returns loading state during fetch');
  it('returns error state on API failure');
  it('retries on failure up to 3 times');
  it('caches data for staleTime duration');
  it('refetches data after cacheTime expires');
  it('transforms API response to CarouselGame format');
});

// __tests__/hooks/useCarouselSort.test.ts

describe('useCarouselSort', () => {
  it('returns default sort when no preference exists');
  it('reads sort from URL query params');
  it('reads sort from localStorage as fallback');
  it('updates URL when sort changes');
  it('persists sort to localStorage');
  it('URL takes precedence over localStorage');
});
```

### Integration Tests
```typescript
// __tests__/game-carousel.integration.test.tsx

describe('GameCarousel Integration', () => {
  describe('API Integration', () => {
    it('loads and displays games from API');
    it('shows skeleton during loading');
    it('shows error state on API failure');
    it('retries on error when clicking retry button');
    it('handles empty response gracefully');
  });

  describe('Full Navigation Flow', () => {
    it('completes full loop through all games');
    it('maintains state after re-render');
    it('handles rapid navigation clicks');
  });

  describe('Sorting Integration', () => {
    it('re-fetches games when sort changes');
    it('updates URL and localStorage together');
    it('restores sort preference on page load');
  });
});
```

### Accessibility Tests
```typescript
// __tests__/game-carousel.a11y.test.tsx

describe('GameCarousel Accessibility', () => {
  it('passes axe accessibility checks');
  it('has no critical accessibility violations');
  it('is navigable by keyboard only');
  it('announces changes to screen readers');
  it('has sufficient color contrast');
  it('respects prefers-reduced-motion');
});
```

### Snapshot Tests
```typescript
// __tests__/game-carousel.snapshot.test.tsx

describe('GameCarousel Snapshots', () => {
  it('matches snapshot with default props');
  it('matches snapshot with loading state');
  it('matches snapshot with empty state');
  it('matches snapshot with sorting controls');
  it('matches snapshot on mobile viewport');
});
```

---

## 🔧 Technical Implementation

### Test Setup
```typescript
// __tests__/setup.ts

import '@testing-library/jest-dom';
import { server } from './mocks/server';

beforeAll(() => server.listen());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
```

### MSW Handlers
```typescript
// __tests__/mocks/handlers.ts

import { http, HttpResponse } from 'msw';

export const handlers = [
  http.get('/api/v1/shared-games', ({ request }) => {
    const url = new URL(request.url);
    const sort = url.searchParams.get('sort');

    if (sort === 'error') {
      return HttpResponse.json({ error: 'Server error' }, { status: 500 });
    }

    return HttpResponse.json({
      data: mockApiGames,
      total: mockApiGames.length,
    });
  }),

  http.get('/api/v1/library/games', () => {
    return HttpResponse.json({
      data: mockLibraryGames,
      total: mockLibraryGames.length,
    });
  }),
];
```

### Test Utilities
```typescript
// __tests__/utils/test-utils.tsx

import { render } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });
}

export function renderWithProviders(ui: React.ReactElement) {
  const queryClient = createTestQueryClient();
  return render(
    <QueryClientProvider client={queryClient}>
      {ui}
    </QueryClientProvider>
  );
}

export * from '@testing-library/react';
```

---

## 📁 Files to Create/Modify

| Action | File Path |
|--------|-----------|
| CREATE | `apps/web/src/components/ui/data-display/__tests__/game-carousel.test.tsx` |
| CREATE | `apps/web/src/components/ui/data-display/__tests__/game-carousel.integration.test.tsx` |
| CREATE | `apps/web/src/components/ui/data-display/__tests__/game-carousel.a11y.test.tsx` |
| CREATE | `apps/web/src/components/ui/data-display/__tests__/game-carousel.snapshot.test.tsx` |
| CREATE | `apps/web/src/hooks/__tests__/useCarouselGames.test.ts` |
| CREATE | `apps/web/src/hooks/__tests__/useCarouselSort.test.ts` |
| CREATE | `apps/web/src/__tests__/mocks/handlers.ts` |
| CREATE | `apps/web/src/__tests__/mocks/server.ts` |
| CREATE | `apps/web/src/__tests__/utils/test-utils.tsx` |

---

## 📊 Coverage Targets

| File | Line Coverage | Branch Coverage |
|------|---------------|-----------------|
| game-carousel.tsx | ≥85% | ≥80% |
| useCarouselGames.ts | ≥90% | ≥85% |
| useCarouselSort.ts | ≥90% | ≥85% |
| carousel-sort-dropdown.tsx | ≥85% | ≥80% |
| **Overall** | **≥85%** | **≥80%** |

---

## 🔗 Dependencies

- **Blocked By**: GC-001 (API Integration), GC-002 (Sorting)
- **Blocks**: None (can be done in parallel with GC-004, GC-006)

---

## ✅ Definition of Done

- [ ] Tutti i test cases implementati
- [ ] Coverage ≥85% verificato
- [ ] Zero test failures
- [ ] Accessibility tests passano (0 violazioni)
- [ ] Snapshots aggiornati e committed
- [ ] Tests eseguibili in CI/CD pipeline
- [ ] MSW handlers documentati
- [ ] Code review approvato

# [FEATURE] GameCarousel API Integration

**Issue ID**: GC-001
**Epic**: EPIC-GC-001 (GameCarousel Integration)
**Story Points**: 8
**Priority**: P0 (Critical Path)
**Status**: To Do
**Assignee**: TBD

---

## 📋 User Story

> Come utente della gallery,
> Voglio vedere giochi reali dal catalogo,
> Così da poter esplorare la collezione effettiva della piattaforma.

---

## 🎯 Acceptance Criteria

- [ ] Hook `useCarouselGames(options)` implementato
- [ ] Supporto sorgenti: featured, trending, category, user-library
- [ ] Loading states con `GameCarouselSkeleton`
- [ ] Error handling con retry automatico (3 tentativi)
- [ ] Caching React Query con stale-while-revalidate
- [ ] TypeScript types per response API
- [ ] Unit tests per hook (≥90% coverage)

---

## 🧪 BDD Scenarios

```gherkin
Feature: GameCarousel API Integration

  Background:
    Given the GameCarousel component is rendered
    And the API base URL is configured

  Scenario: Load featured games successfully
    Given the API returns 7 featured games
    When the carousel fetches data
    Then the loading skeleton should appear
    And after loading completes
    Then I should see 7 game cards
    And the first game should be centered

  Scenario: Handle API error with retry
    Given the API returns a 500 error
    When the carousel fetches data
    Then I should see an error state
    And I should see a "Riprova" button
    When I click "Riprova"
    Then the carousel should retry the fetch
    Given the retry succeeds
    Then I should see the game cards

  Scenario: Handle empty response
    Given the API returns an empty array
    When the carousel fetches data
    Then I should see "Nessun gioco trovato" message

  Scenario: Cache data with stale-while-revalidate
    Given I previously loaded featured games
    When I navigate away and return
    Then I should immediately see cached games
    And a background refresh should occur
```

---

## 🔧 Technical Implementation

### Hook Interface
```typescript
// hooks/useCarouselGames.ts

interface UseCarouselGamesOptions {
  source: 'featured' | 'trending' | 'category' | 'user-library';
  categoryId?: string;
  limit?: number;
  enabled?: boolean;
}

interface UseCarouselGamesResult {
  games: CarouselGame[];
  isLoading: boolean;
  isError: boolean;
  error: Error | null;
  refetch: () => void;
}

export function useCarouselGames(options: UseCarouselGamesOptions): UseCarouselGamesResult;
```

### API Endpoints
| Source | Endpoint | Auth Required |
|--------|----------|---------------|
| featured | `GET /api/v1/shared-games?sort=rating&limit=10` | No |
| trending | `GET /api/v1/shared-games?sort=popularity&limit=10` | No |
| category | `GET /api/v1/shared-games?category={id}&limit=10` | No |
| user-library | `GET /api/v1/library/games` | Yes |

### React Query Configuration
```typescript
const queryOptions = {
  staleTime: 5 * 60 * 1000, // 5 minutes
  cacheTime: 30 * 60 * 1000, // 30 minutes
  retry: 3,
  retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000),
};
```

### Data Transformation
```typescript
// Transform API response to CarouselGame format
function transformToCarouselGame(apiGame: SharedGameDto): CarouselGame {
  return {
    id: apiGame.id,
    title: apiGame.name,
    subtitle: apiGame.publisher,
    imageUrl: apiGame.thumbnailUrl || apiGame.imageUrl,
    rating: apiGame.averageRating,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: `${apiGame.minPlayers}-${apiGame.maxPlayers}` },
      { icon: Clock, value: `${apiGame.playingTime} min` },
    ],
    badge: apiGame.isNew ? 'New' : undefined,
  };
}
```

---

## 📁 Files to Create/Modify

| Action | File Path |
|--------|-----------|
| CREATE | `apps/web/src/hooks/useCarouselGames.ts` |
| CREATE | `apps/web/src/hooks/__tests__/useCarouselGames.test.ts` |
| MODIFY | `apps/web/src/app/(public)/gallery/page.tsx` |
| MODIFY | `apps/web/src/components/ui/data-display/game-carousel.tsx` |

---

## 🔗 Dependencies

- **Blocked By**: None
- **Blocks**: GC-002, GC-003, GC-005

---

## ✅ Definition of Done

- [ ] Hook implementato e funzionante
- [ ] Gallery page usa dati reali (non mock)
- [ ] Loading/error states visibili
- [ ] Unit tests passano (coverage ≥90%)
- [ ] TypeScript compila senza errori
- [ ] Code review approvato
- [ ] QA verification su staging

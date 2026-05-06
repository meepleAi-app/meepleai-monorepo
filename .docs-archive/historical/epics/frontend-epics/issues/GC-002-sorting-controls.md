# [FEATURE] GameCarousel Sorting & Filtering Controls

**Issue ID**: GC-002
**Epic**: EPIC-GC-001 (GameCarousel Integration)
**Story Points**: 5
**Priority**: P1
**Status**: To Do
**Assignee**: TBD

---

## 📋 User Story

> Come utente,
> Voglio poter ordinare e filtrare i giochi nel carousel,
> Così da trovare più facilmente quello che cerco.

---

## 🎯 Acceptance Criteria

- [ ] Dropdown ordinamento (Rating, Popolarità, Nome, Data)
- [ ] Icone per ogni opzione di ordinamento
- [ ] Persistenza preferenze in localStorage
- [ ] URL query params sincronizzati (?sort=rating)
- [ ] Animazione smooth durante cambio ordinamento
- [ ] Accessibilità completa (keyboard, screen reader)
- [ ] Mobile-friendly (touch targets ≥44px)

---

## 🧪 BDD Scenarios

```gherkin
Feature: Carousel Sorting Controls

  Background:
    Given the carousel displays 10 games
    And sorting controls are enabled

  Scenario: Sort by rating (descending)
    Given games are currently sorted by popularity
    When I click the sort dropdown
    And I select "Rating"
    Then the games should reorder by rating (highest first)
    And the URL should update to include "?sort=rating"
    And a smooth animation should play during reorder

  Scenario: Sort by name (alphabetical)
    When I select "Nome A-Z" from the sort dropdown
    Then games should be ordered alphabetically by title
    And the dropdown should show "Nome A-Z" as selected

  Scenario: Persist sort preference
    Given I sorted games by "Rating"
    When I close the browser
    And I return to the gallery page
    Then the carousel should still be sorted by "Rating"
    And the URL should reflect the sort parameter

  Scenario: Share sorted view
    Given I sorted games by "Data"
    When I copy the page URL
    And another user opens that URL
    Then they should see games sorted by "Data"

  Scenario: Keyboard accessibility
    Given the sort dropdown is focused
    When I press Enter
    Then the dropdown should open
    When I press Arrow Down twice
    And I press Enter
    Then the second option should be selected
```

---

## 🔧 Technical Implementation

### Sort Options Configuration
```typescript
// types/carousel-sort.ts

export type CarouselSortValue = 'rating' | 'popularity' | 'name' | 'date';

export interface CarouselSortOption {
  value: CarouselSortValue;
  label: string;
  icon: LucideIcon;
  direction: 'asc' | 'desc';
}

export const CAROUSEL_SORT_OPTIONS: CarouselSortOption[] = [
  { value: 'rating', label: 'Rating', icon: Star, direction: 'desc' },
  { value: 'popularity', label: 'Popolarità', icon: TrendingUp, direction: 'desc' },
  { value: 'name', label: 'Nome A-Z', icon: SortAsc, direction: 'asc' },
  { value: 'date', label: 'Più recenti', icon: Calendar, direction: 'desc' },
];
```

### Extended GameCarousel Props
```typescript
interface GameCarouselProps {
  // ... existing props

  /** Enable sorting controls */
  sortable?: boolean;

  /** Default sort option */
  defaultSort?: CarouselSortValue;

  /** Callback when sort changes */
  onSortChange?: (sort: CarouselSortValue) => void;

  /** Sync sort with URL query params */
  syncWithUrl?: boolean;
}
```

### Custom Hook for Sort State
```typescript
// hooks/useCarouselSort.ts

interface UseCarouselSortOptions {
  defaultSort?: CarouselSortValue;
  syncWithUrl?: boolean;
  storageKey?: string;
}

export function useCarouselSort(options: UseCarouselSortOptions) {
  // 1. Read from URL if syncWithUrl
  // 2. Fall back to localStorage
  // 3. Fall back to defaultSort
  // 4. Update URL and localStorage on change

  return {
    currentSort: CarouselSortValue,
    setSort: (sort: CarouselSortValue) => void,
    sortOptions: CarouselSortOption[],
  };
}
```

### SortDropdown Component
```typescript
// components/carousel-sort-dropdown.tsx

interface CarouselSortDropdownProps {
  value: CarouselSortValue;
  onChange: (value: CarouselSortValue) => void;
  options: CarouselSortOption[];
  className?: string;
}

export function CarouselSortDropdown({
  value,
  onChange,
  options,
  className,
}: CarouselSortDropdownProps) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" className={className}>
          <currentOption.icon className="w-4 h-4 mr-2" />
          {currentOption.label}
          <ChevronDown className="w-4 h-4 ml-2" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent>
        {options.map(option => (
          <DropdownMenuItem
            key={option.value}
            onClick={() => onChange(option.value)}
          >
            <option.icon className="w-4 h-4 mr-2" />
            {option.label}
            {value === option.value && <Check className="w-4 h-4 ml-auto" />}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
```

---

## 📁 Files to Create/Modify

| Action | File Path |
|--------|-----------|
| CREATE | `apps/web/src/types/carousel-sort.ts` |
| CREATE | `apps/web/src/hooks/useCarouselSort.ts` |
| CREATE | `apps/web/src/components/ui/data-display/carousel-sort-dropdown.tsx` |
| MODIFY | `apps/web/src/components/ui/data-display/game-carousel.tsx` |
| CREATE | `apps/web/src/hooks/__tests__/useCarouselSort.test.ts` |

---

## 🎨 Design Specs

### Sort Dropdown Styling
- Position: Top-right of carousel header
- Button: `variant="outline"` with icon + label
- Dropdown: Standard shadcn/ui dropdown menu
- Active indicator: Check icon on selected option
- Mobile: Full-width dropdown trigger

### Animation
- Sort transition: 300ms ease-out
- Card reorder: Staggered fade + slide
- Use `layout` from framer-motion if available

---

## 🔗 Dependencies

- **Blocked By**: GC-001 (API Integration)
- **Blocks**: GC-005 (Tests)

---

## ✅ Definition of Done

- [ ] Sort dropdown implementato e funzionante
- [ ] 4 opzioni di ordinamento disponibili
- [ ] Preferenze persistite in localStorage
- [ ] URL query params sincronizzati
- [ ] Animazioni smooth durante cambio sort
- [ ] Test unitari passano
- [ ] Accessibilità verificata (keyboard + screen reader)
- [ ] Code review approvato

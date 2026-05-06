# [FEATURE] GameCarousel Homepage Integration

**Issue ID**: GC-003
**Epic**: EPIC-GC-001 (GameCarousel Integration)
**Story Points**: 5
**Priority**: P1
**Status**: To Do
**Assignee**: TBD

---

## 📋 User Story

> Come visitatore della homepage,
> Voglio vedere carousel di giochi in evidenza,
> Così da scoprire immediatamente cosa offre la piattaforma.

---

## 🎯 Acceptance Criteria

- [ ] Sezione "Giochi in Evidenza" sempre visibile
- [ ] Sezione "Trending" visibile a tutti
- [ ] Sezione "Dalla Tua Libreria" solo per utenti autenticati
- [ ] Lazy loading per sezioni below-the-fold
- [ ] Skeleton loading durante fetch
- [ ] Link "Vedi tutti" che porta a /gallery
- [ ] Responsive layout (mobile/tablet/desktop)
- [ ] Performance: LCP < 2.5s

---

## 🧪 BDD Scenarios

```gherkin
Feature: Homepage Carousel Integration

  Scenario: Anonymous user sees public carousels
    Given I am not logged in
    When I visit the homepage
    Then I should see "Giochi in Evidenza" carousel
    And I should see "Trending Questa Settimana" carousel
    And I should NOT see "Dalla Tua Libreria" carousel

  Scenario: Authenticated user sees personalized carousels
    Given I am logged in
    And I have 5 games in my library
    When I visit the homepage
    Then I should see "Giochi in Evidenza" carousel
    And I should see "Trending Questa Settimana" carousel
    And I should see "Dalla Tua Libreria" carousel
    And "Dalla Tua Libreria" should show my library games

  Scenario: Click on game card navigates to detail
    Given I am on the homepage
    When I click on a game card in the carousel
    Then I should navigate to /giochi/{gameId}

  Scenario: Click "Vedi tutti" navigates to gallery
    Given I am on the homepage
    When I click "Vedi tutti" link on Featured carousel
    Then I should navigate to /gallery?filter=featured

  Scenario: Lazy load below-the-fold carousels
    Given I am on the homepage
    And "Trending" carousel is below the fold
    When I scroll down to "Trending" section
    Then the carousel should start loading
    And I should see skeleton until data loads
```

---

## 🔧 Technical Implementation

### Homepage Section Component
```typescript
// components/home/game-carousel-section.tsx

interface GameCarouselSectionProps {
  title: string;
  subtitle?: string;
  source: 'featured' | 'trending' | 'user-library';
  viewAllHref?: string;
  requiresAuth?: boolean;
}

export function GameCarouselSection({
  title,
  subtitle,
  source,
  viewAllHref,
  requiresAuth = false,
}: GameCarouselSectionProps) {
  const { isAuthenticated } = useAuth();
  const { games, isLoading, isError } = useCarouselGames({ source });

  // Don't render if requires auth and not authenticated
  if (requiresAuth && !isAuthenticated) return null;

  return (
    <section className="py-8 md:py-12">
      <div className="flex items-center justify-between mb-4 px-4 md:px-8">
        <div>
          <h2 className="font-quicksand font-bold text-2xl">{title}</h2>
          {subtitle && <p className="text-muted-foreground">{subtitle}</p>}
        </div>
        {viewAllHref && (
          <Link href={viewAllHref} className="text-primary hover:underline">
            Vedi tutti →
          </Link>
        )}
      </div>

      {isLoading ? (
        <GameCarouselSkeleton />
      ) : isError ? (
        <CarouselErrorState onRetry={refetch} />
      ) : (
        <GameCarousel games={games} onGameSelect={handleSelect} />
      )}
    </section>
  );
}
```

### Homepage Layout Update
```typescript
// app/(public)/page.tsx

export default function HomePage() {
  return (
    <div>
      {/* Hero Section (existing) */}
      <HeroSection />

      {/* Featured Games - Always visible, priority load */}
      <GameCarouselSection
        title="Giochi in Evidenza"
        subtitle="I migliori giochi selezionati per te"
        source="featured"
        viewAllHref="/gallery?filter=featured"
      />

      {/* Trending - Lazy loaded */}
      <LazySection>
        <GameCarouselSection
          title="Trending Questa Settimana"
          subtitle="I più giocati dalla community"
          source="trending"
          viewAllHref="/gallery?filter=trending"
          className="bg-muted/30"
        />
      </LazySection>

      {/* User Library - Auth required, lazy loaded */}
      <LazySection>
        <GameCarouselSection
          title="Dalla Tua Libreria"
          subtitle="Continua a esplorare i tuoi giochi"
          source="user-library"
          viewAllHref="/dashboard/collection"
          requiresAuth
        />
      </LazySection>

      {/* Other homepage sections... */}
    </div>
  );
}
```

### Lazy Loading Implementation
```typescript
// components/ui/lazy-section.tsx

interface LazySectionProps {
  children: React.ReactNode;
  rootMargin?: string;
  threshold?: number;
}

export function LazySection({
  children,
  rootMargin = '100px',
  threshold = 0.1,
}: LazySectionProps) {
  const [isVisible, setIsVisible] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setIsVisible(true);
          observer.disconnect();
        }
      },
      { rootMargin, threshold }
    );

    if (ref.current) observer.observe(ref.current);
    return () => observer.disconnect();
  }, [rootMargin, threshold]);

  return (
    <div ref={ref}>
      {isVisible ? children : <div className="h-[400px]" />}
    </div>
  );
}
```

---

## 📁 Files to Create/Modify

| Action | File Path |
|--------|-----------|
| CREATE | `apps/web/src/components/home/game-carousel-section.tsx` |
| CREATE | `apps/web/src/components/ui/lazy-section.tsx` |
| MODIFY | `apps/web/src/app/(public)/page.tsx` |
| CREATE | `apps/web/src/components/home/__tests__/game-carousel-section.test.tsx` |

---

## 🎨 Design Specs

### Section Spacing
- Vertical padding: `py-8 md:py-12`
- Horizontal padding: `px-4 md:px-8`
- Between sections: Alternating background (`bg-muted/30`)

### Responsive Breakpoints
| Breakpoint | Cards Visible | Section Padding |
|------------|---------------|-----------------|
| Mobile (<640px) | 1 | 16px |
| Tablet (640-1024px) | 3 | 24px |
| Desktop (>1024px) | 5 | 32px |

### "Vedi tutti" Link
- Position: Top-right aligned with title
- Style: `text-primary` with hover underline
- Mobile: Below title, full width

---

## 🔗 Dependencies

- **Blocked By**: GC-001 (API Integration)
- **Blocks**: GC-005 (Tests)

---

## ✅ Definition of Done

- [ ] 3 sezioni carousel implementate in homepage
- [ ] Sezione "Dalla Tua Libreria" visibile solo se autenticato
- [ ] Lazy loading funzionante per sezioni below-the-fold
- [ ] Link "Vedi tutti" navigano correttamente
- [ ] Click su card naviga a dettaglio gioco
- [ ] Performance verificata (LCP < 2.5s)
- [ ] Test di integrazione passano
- [ ] Code review approvato

# [TECH] GameCarousel Storybook Stories

**Issue ID**: GC-004
**Epic**: EPIC-GC-001 (GameCarousel Integration)
**Story Points**: 3
**Priority**: P2
**Status**: To Do
**Assignee**: TBD

---

## 📋 User Story

> Come sviluppatore,
> Voglio documentazione Storybook del GameCarousel,
> Così da poter esplorare varianti e props interattivamente.

---

## 🎯 Acceptance Criteria

- [ ] Story "Default" con dati mock realistici
- [ ] Story "Loading" che mostra skeleton state
- [ ] Story "Error" che mostra error state con retry
- [ ] Story "Empty" senza giochi (empty state)
- [ ] Story "WithSorting" con controlli ordinamento
- [ ] Story "SingleCard" per viewport mobile
- [ ] Story "AutoPlay" con rotazione automatica
- [ ] Controls panel per tutte le props
- [ ] Docs page con esempi d'uso e best practices
- [ ] Accessibility addon integrato

---

## 🔧 Technical Implementation

### Story File Structure
```typescript
// stories/data-display/game-carousel.stories.tsx

import type { Meta, StoryObj } from '@storybook/react';
import { within, userEvent } from '@storybook/testing-library';
import { expect } from '@storybook/jest';

import { GameCarousel, GameCarouselSkeleton } from '@/components/ui/data-display/game-carousel';
import { mockFeaturedGames, mockEmptyGames } from './mock-data';

const meta: Meta<typeof GameCarousel> = {
  title: 'Data Display/GameCarousel',
  component: GameCarousel,
  tags: ['autodocs'],
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component: `
## GameCarousel

Un carousel immersivo 3D per navigare la collezione di giochi.

### Features
- Card centrale in focus con variante "featured"
- Card laterali scalate e sfocate per effetto profondità
- Navigazione infinita (loop)
- Supporto touch swipe, keyboard, e click
- Responsive: 1 card mobile, 3 tablet, 5 desktop
- Accessibilità WCAG AA completa

### Usage
\`\`\`tsx
import { GameCarousel } from '@/components/ui/data-display';

<GameCarousel
  games={games}
  title="Featured Games"
  onGameSelect={(game) => navigate(\`/games/\${game.id}\`)}
/>
\`\`\`
        `,
      },
    },
  },
  argTypes: {
    games: {
      description: 'Array of games to display',
      control: 'object',
    },
    title: {
      description: 'Section title',
      control: 'text',
    },
    subtitle: {
      description: 'Section subtitle',
      control: 'text',
    },
    autoPlay: {
      description: 'Enable automatic rotation',
      control: 'boolean',
    },
    autoPlayInterval: {
      description: 'Auto-play interval in milliseconds',
      control: { type: 'number', min: 1000, max: 10000, step: 500 },
    },
    showDots: {
      description: 'Show navigation dots',
      control: 'boolean',
    },
    onGameSelect: {
      description: 'Callback when a game is selected',
      action: 'gameSelected',
    },
  },
};

export default meta;
type Story = StoryObj<typeof GameCarousel>;
```

### Default Story
```typescript
export const Default: Story = {
  args: {
    games: mockFeaturedGames,
    title: 'Featured Games',
    subtitle: 'Top-rated games loved by our community',
    showDots: true,
  },
};
```

### Loading Story
```typescript
export const Loading: Story = {
  render: () => <GameCarouselSkeleton />,
  parameters: {
    docs: {
      description: {
        story: 'Skeleton state shown while loading game data.',
      },
    },
  },
};
```

### Error Story
```typescript
export const Error: Story = {
  render: () => (
    <div className="py-12 px-8">
      <h2 className="font-quicksand font-bold text-2xl mb-2">Featured Games</h2>
      <div className="flex flex-col items-center justify-center py-20 text-center">
        <AlertCircle className="w-12 h-12 text-destructive mb-4" />
        <p className="text-lg font-medium mb-2">Impossibile caricare i giochi</p>
        <p className="text-muted-foreground mb-4">Si è verificato un errore. Riprova.</p>
        <Button onClick={() => alert('Retry clicked')}>
          <RefreshCw className="w-4 h-4 mr-2" />
          Riprova
        </Button>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Error state with retry button when API fails.',
      },
    },
  },
};
```

### Empty Story
```typescript
export const Empty: Story = {
  args: {
    games: [],
    title: 'Your Library',
    subtitle: 'Games you have added',
  },
  parameters: {
    docs: {
      description: {
        story: 'Empty state when no games are available.',
      },
    },
  },
};
```

### With Sorting Story
```typescript
export const WithSorting: Story = {
  args: {
    games: mockFeaturedGames,
    title: 'Browse Games',
    sortable: true,
    defaultSort: 'rating',
  },
  parameters: {
    docs: {
      description: {
        story: 'Carousel with sorting controls enabled.',
      },
    },
  },
};
```

### AutoPlay Story
```typescript
export const AutoPlay: Story = {
  args: {
    games: mockFeaturedGames,
    title: 'Featured Games',
    autoPlay: true,
    autoPlayInterval: 3000,
    showDots: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Carousel with automatic rotation. Pauses on hover.',
      },
    },
  },
};
```

### Mobile Viewport Story
```typescript
export const Mobile: Story = {
  args: {
    games: mockFeaturedGames,
    title: 'Featured Games',
    showDots: true,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    docs: {
      description: {
        story: 'Single card view on mobile devices.',
      },
    },
  },
};
```

### Interactive Test Story
```typescript
export const NavigationTest: Story = {
  args: {
    games: mockFeaturedGames,
    title: 'Navigation Test',
    showDots: true,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Find and click next button
    const nextButton = canvas.getByRole('button', { name: /next game/i });
    await userEvent.click(nextButton);

    // Verify the carousel moved
    const announcement = canvas.getByText(/showing .*, 2 of/i);
    await expect(announcement).toBeInTheDocument();

    // Click previous button
    const prevButton = canvas.getByRole('button', { name: /previous game/i });
    await userEvent.click(prevButton);

    // Verify back to first
    const backAnnouncement = canvas.getByText(/showing .*, 1 of/i);
    await expect(backAnnouncement).toBeInTheDocument();
  },
};
```

### Mock Data File
```typescript
// stories/data-display/mock-data.ts

import { Users, Clock } from 'lucide-react';
import type { CarouselGame } from '@/components/ui/data-display/game-carousel';

export const mockFeaturedGames: CarouselGame[] = [
  {
    id: '1',
    title: 'Gloomhaven',
    subtitle: 'Cephalofair Games',
    imageUrl: '/placeholder-game-1.jpg',
    rating: 8.7,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: '1-4' },
      { icon: Clock, value: '60-120 min' },
    ],
    badge: 'Top Rated',
  },
  // ... more mock games
];

export const mockEmptyGames: CarouselGame[] = [];
```

---

## 📁 Files to Create/Modify

| Action | File Path |
|--------|-----------|
| CREATE | `apps/web/src/stories/data-display/game-carousel.stories.tsx` |
| CREATE | `apps/web/src/stories/data-display/mock-data.ts` |
| MODIFY | `apps/web/.storybook/main.ts` (if needed) |

---

## 🔗 Dependencies

- **Blocked By**: GC-001 (API Integration)
- **Blocks**: None

---

## ✅ Definition of Done

- [ ] Tutte le stories implementate e funzionanti
- [ ] Controls panel configurato per tutte le props
- [ ] Docs page con esempi d'uso
- [ ] Interactive tests passano
- [ ] Accessibility addon mostra 0 violazioni
- [ ] Stories visibili in Storybook deployment
- [ ] Code review approvato

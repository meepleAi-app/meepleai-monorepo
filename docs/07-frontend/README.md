# Frontend Documentation

**Next.js 14 App Router + React 18**

---

## Quick Start

**Prerequisites**:
- Node.js 20+
- pnpm 8+

**Setup**:
```bash
cd apps/web
pnpm install
cp .env.development.example .env.local
pnpm dev  # http://localhost:3000
```

---

## Architecture

### Tech Stack

**Framework**:
- Next.js 14 (App Router)
- React 18 (Server + Client Components)
- TypeScript 5

**UI**:
- Tailwind CSS 3
- shadcn/ui components
- Radix UI primitives
- Lucide icons

**State Management**:
- Zustand (client state)
- React Query (server state)
- React Hook Form (forms)

**Routing**:
- File-based routing (`src/app/`)
- Route groups for organization
- Server-side data fetching

---

## Project Structure

```
apps/web/
├── src/
│   ├── app/                   # App Router pages
│   │   ├── (auth)/           # Auth route group
│   │   ├── (dashboard)/      # Dashboard route group
│   │   └── layout.tsx        # Root layout
│   ├── components/           # React components
│   │   ├── ui/              # shadcn/ui components
│   │   └── features/        # Feature components
│   ├── lib/                  # Utilities & services
│   │   ├── api/             # API clients
│   │   ├── hooks/           # Custom hooks
│   │   └── utils/           # Utility functions
│   └── styles/              # Global styles
├── public/                   # Static assets
└── __tests__/               # Test files
```

---

## Development Workflow

### Adding Features

1. **Create Component**:
```tsx
// src/components/features/game/GameCard.tsx
import { Card } from "@/components/ui/card"

interface GameCardProps {
  game: GameData
  onSelect: (id: string) => void
}

export function GameCard({ game, onSelect }: GameCardProps) {
  return (
    <Card className="p-4">
      <h3>{game.name}</h3>
    </Card>
  )
}
```

2. **Add Route**:
```tsx
// src/app/(dashboard)/games/page.tsx
import { GameCard } from "@/components/features/game/GameCard"

export default async function GamesPage() {
  const games = await fetchGames()

  return (
    <div className="grid grid-cols-3 gap-4">
      {games.map(game => (
        <GameCard key={game.id} game={game} />
      ))}
    </div>
  )
}
```

3. **Add Tests**:
```tsx
// __tests__/components/GameCard.test.tsx
import { render, screen } from '@testing-library/react'
import { GameCard } from '@/components/features/game/GameCard'

test('renders game name', () => {
  render(<GameCard game={{ name: "Chess" }} />)
  expect(screen.getByText("Chess")).toBeInTheDocument()
})
```

---

## State Management Patterns

### Zustand Store

```tsx
// src/lib/stores/gameStore.ts
import { create } from 'zustand'

interface GameStore {
  games: GameData[]
  selectedGame: GameData | null
  setSelectedGame: (game: GameData) => void
  fetchGames: () => Promise<void>
}

export const useGameStore = create<GameStore>((set) => ({
  games: [],
  selectedGame: null,
  setSelectedGame: (game) => set({ selectedGame: game }),
  fetchGames: async () => {
    const games = await fetch('/api/v1/games').then(r => r.json())
    set({ games })
  }
}))
```

### React Query

```tsx
// src/lib/hooks/useGames.ts
import { useQuery } from '@tanstack/react-query'
import { gamesClient } from '@/lib/api/clients/gamesClient'

export function useGames() {
  return useQuery({
    queryKey: ['games'],
    queryFn: () => gamesClient.getGames()
  })
}
```

---

## Testing

### Unit Tests (Vitest)

```bash
pnpm test              # Run tests
pnpm test:coverage     # With coverage
pnpm test:ui           # Interactive UI
```

### E2E Tests (Playwright)

```bash
pnpm test:e2e          # Run E2E tests
pnpm test:e2e:ui       # Interactive mode
```

**Target**: 85%+ coverage

---

## Performance Optimization

### Server Components

Use Server Components by default, Client Components only when needed:

```tsx
// ✅ Server Component (default)
export default async function GamesPage() {
  const games = await fetchGames()
  return <GamesList games={games} />
}

// ✅ Client Component (only when needed)
'use client'
export function InteractiveGameCard() {
  const [expanded, setExpanded] = useState(false)
  return <Card onClick={() => setExpanded(!expanded)} />
}
```

### Image Optimization

```tsx
import Image from 'next/image'

<Image
  src="/games/chess.jpg"
  alt="Chess"
  width={300}
  height={200}
  placeholder="blur"
  priority={false}
/>
```

### Code Splitting

```tsx
import dynamic from 'next/dynamic'

const HeavyComponent = dynamic(() => import('./HeavyComponent'), {
  loading: () => <Spinner />,
  ssr: false
})
```

---

## Styling

### Tailwind Conventions

**Spacing**: Use Tailwind spacing scale (4px increments)
```tsx
<div className="p-4 mb-6 gap-2">
```

**Colors**: Use theme colors from `tailwind.config.ts`
```tsx
<button className="bg-primary text-primary-foreground">
```

**Responsive**: Mobile-first approach
```tsx
<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3">
```

### shadcn/ui Components

```bash
# Add component
npx shadcn-ui@latest add button

# Usage
import { Button } from "@/components/ui/button"
<Button variant="outline">Click me</Button>
```

---

## API Integration

### API Clients

```tsx
// src/lib/api/clients/gamesClient.ts
import { apiClient } from './apiClient'

export const gamesClient = {
  getGames: () => apiClient.get<GameData[]>('/games'),
  getGame: (id: string) => apiClient.get<GameData>(`/games/${id}`),
  createGame: (data: CreateGameDto) => apiClient.post('/games', data)
}
```

### Error Handling

```tsx
import { toast } from '@/components/ui/use-toast'

try {
  await gamesClient.createGame(data)
  toast({ title: "Success", description: "Game created" })
} catch (error) {
  toast({
    title: "Error",
    description: error.message,
    variant: "destructive"
  })
}
```

---

## Code Standards

**Naming**:
- PascalCase: Components, types
- camelCase: Functions, variables
- UPPER_SNAKE_CASE: Constants

**File Organization**:
- One component per file
- Co-locate tests with components
- Group by feature, not by type

**Type Safety**:
- Use TypeScript strict mode
- Avoid `any`, prefer `unknown`
- Define prop interfaces

---

## Related Documentation

- [API Documentation](../03-api/README.md)
- [Testing Guide](../05-testing/README.md)
- [User Flows](../11-user-flows/README.md)
- [Development Guide](../02-development/README.md)

---

**Last Updated**: 2026-01-31
**Maintainer**: Frontend Team

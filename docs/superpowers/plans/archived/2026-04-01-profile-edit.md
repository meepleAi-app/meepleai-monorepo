# Profile Edit — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Completare la pagina `/profile` con avatar upload funzionante, editing del display name, achievements inline e test corretti.

**Architecture:** Il profilo carica i dati utente completi via `api.auth.getProfile()` (useQuery), che include `avatarUrl`. L'`AvatarUpload` component esiste già ed è wireable all'API. L'editing del display name usa un Sheet (drawer) con form minima, salvato via `api.auth.updateProfile()`. Gli achievements vengono estratti in un `AchievementsGrid` condiviso tra la tab inline e la pagina dedicata.

**Tech Stack:** Next.js 16, React 19, TanStack Query v5, Zustand, Zod, shadcn/ui, `react-image-crop`

---

## File Map

| File | Azione | Responsabilità |
|------|--------|----------------|
| `apps/web/src/app/(authenticated)/profile/__tests__/page.test.tsx` | Modifica | Fix 3 test rotti |
| `apps/web/src/components/profile/AchievementsGrid.tsx` | Crea | Grid achievements riusabile (estratto da achievements/page.tsx) |
| `apps/web/src/components/profile/EditProfileSheet.tsx` | Crea | Sheet per editing displayName |
| `apps/web/src/app/(authenticated)/profile/achievements/page.tsx` | Modifica | Usa AchievementsGrid invece di inlining il codice |
| `apps/web/src/app/(authenticated)/profile/page.tsx` | Modifica | AvatarUpload integrato, stats useQuery, AchievementsTab inline, EditProfileSheet |
| `apps/web/src/components/profile/index.ts` | Modifica | Esporta nuovi componenti |

---

## Task 1: Fix test rotti

**Files:**
- Modify: `apps/web/src/app/(authenticated)/profile/__tests__/page.test.tsx`

Tre test falliscono:
1. Riga 88: aspetta `'Total Games'` — il componente mostra `'Giochi'`
2. Riga 97: aspetta `'Game Sessions'` — il componente mostra `'Storia di gioco'`
3. Righe 118-128: aspetta `'Activity Feed Coming Soon'` — il componente ora mostra `<ActivityFeed />`

- [ ] **Step 1.1: Aggiungi mock useActivityFeed e useRecentSessions**

Nel file di test, aggiungi questi mock prima dei test esistenti (dopo il mock `useAuth`):

```typescript
const mockUseRecentSessions = vi.hoisted(() => vi.fn());
const mockUseActivityFeed = vi.hoisted(() => vi.fn());

vi.mock('@/hooks/useRecentSessions', () => ({
  useRecentSessions: mockUseRecentSessions,
}));

vi.mock('@/hooks/useActivityFeed', () => ({
  useActivityFeed: mockUseActivityFeed,
}));
```

- [ ] **Step 1.2: Aggiungi default mock values nel beforeEach**

```typescript
beforeEach(() => {
  vi.clearAllMocks();
  mockUseAuth.mockReturnValue({ user: mockUser });
  mockGetStats.mockResolvedValue(mockStats);
  mockUseRecentSessions.mockReturnValue({ sessions: [], isLoading: false, error: null });
  mockUseActivityFeed.mockReturnValue({ items: [], isLoading: false, error: null });
});
```

- [ ] **Step 1.3: Fix test "shows library stats on Overview tab"**

Riga 84-93 — sostituisci:
```typescript
it('shows library stats on Overview tab', async () => {
  renderWithQuery(<ProfilePage />);

  await waitFor(() => {
    expect(screen.getByText('Giochi')).toBeInTheDocument();
  });

  expect(screen.getByText('24')).toBeInTheDocument();
  expect(screen.getByText('8')).toBeInTheDocument();
});
```

- [ ] **Step 1.4: Fix test "shows quick action links"**

Riga 95-104 — sostituisci:
```typescript
it('shows quick action links on Overview tab', async () => {
  renderWithQuery(<ProfilePage />);

  await waitFor(() => {
    expect(screen.getByText('Quick Actions')).toBeInTheDocument();
  });

  expect(screen.getByText('My Library')).toBeInTheDocument();
  expect(screen.getByText('Storia di gioco')).toBeInTheDocument();
});
```

- [ ] **Step 1.5: Fix test "switches to Activity tab"**

Riga 118-128 — sostituisci:
```typescript
it('switches to Activity tab and shows activity feed', async () => {
  renderWithQuery(<ProfilePage />);

  await waitFor(() => {
    expect(screen.getByRole('tab', { name: /Activity/i })).toBeInTheDocument();
  });

  fireEvent.click(screen.getByRole('tab', { name: /Activity/i }));

  expect(
    screen.getByText('Nessuna attività ancora')
  ).toBeInTheDocument();
});
```

- [ ] **Step 1.6: Esegui i test**

```bash
cd apps/web
pnpm test src/app/\(authenticated\)/profile/__tests__/page.test.tsx
```

Expected: tutti i test PASS (8/8).

- [ ] **Step 1.7: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/profile/__tests__/page.test.tsx
git commit -m "fix(profile): correggi 3 test rotti — stringhe IT e ActivityFeed mock"
```

---

## Task 2: Estrai AchievementsGrid

**Files:**
- Create: `apps/web/src/components/profile/AchievementsGrid.tsx`
- Modify: `apps/web/src/app/(authenticated)/profile/achievements/page.tsx`

Estratto dalla page degli achievements: la griglia con filtri, loading, empty state, card.

- [ ] **Step 2.1: Crea AchievementsGrid**

Crea `apps/web/src/components/profile/AchievementsGrid.tsx`:

```typescript
'use client';

import React, { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { Trophy, Lock, TrendingUp, Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { apiClient } from '@/lib/api/client';
import { cn } from '@/lib/utils';

export interface AchievementDto {
  id: string;
  code: string;
  name: string;
  description: string;
  iconUrl: string;
  points: number;
  rarity: string;
  category: string;
  threshold: number;
  progress: number | null;
  isUnlocked: boolean;
  unlockedAt: string | null;
}

type AchievementFilter = 'all' | 'earned' | 'locked' | 'in-progress';

function getStatus(a: AchievementDto): 'earned' | 'locked' | 'in-progress' {
  if (a.isUnlocked) return 'earned';
  if (a.progress !== null && a.progress > 0) return 'in-progress';
  return 'locked';
}

function getIcon(a: AchievementDto): string {
  if (a.iconUrl) return a.iconUrl;
  switch (a.category.toLowerCase()) {
    case 'gameplay': return '🎮';
    case 'collection': return '📚';
    case 'social': return '🤝';
    default: return '🏆';
  }
}

export function AchievementsGrid(): React.ReactElement {
  const [filter, setFilter] = useState<AchievementFilter>('all');

  const { data: achievements, isLoading, error } = useQuery<AchievementDto[]>({
    queryKey: ['achievements'],
    queryFn: async () => {
      const res = await apiClient.get<AchievementDto[]>('/api/v1/achievements');
      return res ?? [];
    },
    staleTime: 5 * 60 * 1000,
  });

  const filtered = (achievements ?? []).filter(a => {
    if (filter === 'all') return true;
    return getStatus(a) === filter;
  });

  return (
    <div>
      {/* Filters */}
      <div className="flex gap-2 mb-6">
        {(['all', 'earned', 'in-progress', 'locked'] as const).map(f => (
          <Button
            key={f}
            variant={filter === f ? 'default' : 'outline'}
            onClick={() => setFilter(f)}
            size="sm"
          >
            {f === 'all' ? 'Tutti' : f === 'earned' ? 'Ottenuti' : f === 'in-progress' ? 'In Corso' : 'Bloccati'}
          </Button>
        ))}
      </div>

      {/* Loading */}
      {isLoading && (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      )}

      {/* Error */}
      {error && (
        <div className="text-center py-16 text-muted-foreground">
          <p>Impossibile caricare gli achievements. Riprova più tardi.</p>
        </div>
      )}

      {/* Empty state */}
      {!isLoading && !error && filtered.length === 0 && (
        <div className="text-center py-16 text-muted-foreground">
          <Trophy className="h-12 w-12 mx-auto mb-4 opacity-50" />
          <p className="text-lg font-medium">
            {filter === 'all' ? 'Nessun achievement disponibile' : `Nessun achievement "${filter}"`}
          </p>
          <p className="text-sm mt-1">Inizia a giocare per sbloccare achievements!</p>
        </div>
      )}

      {/* Achievement Grid */}
      {!isLoading && !error && filtered.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {filtered.map(achievement => {
            const status = getStatus(achievement);
            const icon = getIcon(achievement);
            return (
              <div
                key={achievement.id}
                className={cn(
                  'p-6 rounded-xl border transition-all',
                  status === 'earned' && 'bg-card border-primary/50',
                  status === 'locked' && 'bg-muted/50 border-border opacity-60',
                  status === 'in-progress' && 'bg-card border-amber-500/50'
                )}
              >
                <div className="flex items-start gap-4 mb-4">
                  <div className="text-4xl">{icon}</div>
                  <div className="flex-1">
                    <h3 className="font-bold text-lg mb-1">{achievement.name}</h3>
                    <p className="text-sm text-muted-foreground">{achievement.description}</p>
                  </div>
                  {status === 'earned' && <Trophy className="h-5 w-5 text-primary" />}
                  {status === 'locked' && <Lock className="h-5 w-5 text-muted-foreground" />}
                  {status === 'in-progress' && <TrendingUp className="h-5 w-5 text-amber-500" />}
                </div>

                {status === 'in-progress' && achievement.progress !== null && achievement.threshold > 0 && (
                  <div className="mb-3">
                    <div className="flex justify-between text-xs mb-1">
                      <span>Progresso</span>
                      <span>{achievement.progress}/{achievement.threshold}</span>
                    </div>
                    <div className="h-2 bg-muted rounded-full overflow-hidden">
                      <div
                        className="h-full bg-amber-500 transition-all"
                        style={{ width: `${Math.min((achievement.progress / achievement.threshold) * 100, 100)}%` }}
                      />
                    </div>
                  </div>
                )}

                {status === 'earned' && achievement.unlockedAt && (
                  <p className="text-xs text-muted-foreground">
                    Ottenuto il {new Date(achievement.unlockedAt).toLocaleDateString('it-IT')}
                  </p>
                )}

                <div className="mt-3">
                  <span className={cn(
                    'text-xs px-2 py-1 rounded',
                    achievement.rarity.toLowerCase() === 'common' && 'bg-gray-100 text-gray-700',
                    achievement.rarity.toLowerCase() === 'rare' && 'bg-blue-100 text-blue-700',
                    achievement.rarity.toLowerCase() === 'epic' && 'bg-purple-100 text-purple-700',
                    achievement.rarity.toLowerCase() === 'legendary' && 'bg-amber-100 text-amber-700',
                  )}>
                    {achievement.rarity}
                  </span>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 2.2: Semplifica achievements/page.tsx**

Sostituisci tutto il contenuto di `apps/web/src/app/(authenticated)/profile/achievements/page.tsx` con:

```typescript
/**
 * Achievements Page (Issue #4117, #5322)
 * Display user achievements with filtering and progress tracking.
 */

import { AchievementsGrid } from '@/components/profile/AchievementsGrid';

export default function AchievementsPage() {
  return (
    <div className="container mx-auto py-8">
      <div className="mb-8">
        <h1 className="text-3xl font-bold font-quicksand mb-2">Achievements</h1>
        <p className="text-muted-foreground">Tieni traccia dei tuoi traguardi di gioco</p>
      </div>
      <AchievementsGrid />
    </div>
  );
}
```

- [ ] **Step 2.3: Esegui lint e typecheck**

```bash
cd apps/web
pnpm typecheck 2>&1 | grep -E "profile|achievements" | head -20
```

Expected: nessun errore sui file modificati.

- [ ] **Step 2.4: Commit**

```bash
git add apps/web/src/components/profile/AchievementsGrid.tsx
git add apps/web/src/app/\(authenticated\)/profile/achievements/page.tsx
git commit -m "refactor(profile): estrai AchievementsGrid come componente riusabile"
```

---

## Task 3: EditProfileSheet

**Files:**
- Create: `apps/web/src/components/profile/EditProfileSheet.tsx`

Il Sheet permette di modificare `displayName`. Usa `api.auth.updateProfile()` e invalida la cache utente al salvataggio.

- [ ] **Step 3.1: Crea EditProfileSheet**

Crea `apps/web/src/components/profile/EditProfileSheet.tsx`:

```typescript
'use client';

import { useState } from 'react';

import { useQueryClient } from '@tanstack/react-query';
import { Pencil } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/overlays/sheet';
import { userKeys } from '@/hooks/queries/useCurrentUser';
import { api } from '@/lib/api';

interface EditProfileSheetProps {
  currentDisplayName: string;
}

export function EditProfileSheet({ currentDisplayName }: EditProfileSheetProps) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [displayName, setDisplayName] = useState(currentDisplayName);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSave() {
    const trimmed = displayName.trim();
    if (!trimmed) {
      setError('Il nome non può essere vuoto.');
      return;
    }
    setIsSaving(true);
    setError(null);
    try {
      await api.auth.updateProfile({ displayName: trimmed });
      await queryClient.invalidateQueries({ queryKey: userKeys.all });
      setOpen(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Errore durante il salvataggio.');
    } finally {
      setIsSaving(false);
    }
  }

  function handleOpenChange(next: boolean) {
    if (!next) {
      setDisplayName(currentDisplayName);
      setError(null);
    }
    setOpen(next);
  }

  return (
    <Sheet open={open} onOpenChange={handleOpenChange}>
      <SheetTrigger asChild>
        <Button variant="ghost" size="sm" className="gap-1 font-nunito">
          <Pencil className="h-3.5 w-3.5" />
          Modifica
        </Button>
      </SheetTrigger>
      <SheetContent>
        <SheetHeader>
          <SheetTitle className="font-quicksand">Modifica profilo</SheetTitle>
          <SheetDescription className="font-nunito">
            Aggiorna il tuo nome visualizzato.
          </SheetDescription>
        </SheetHeader>

        <div className="py-6 space-y-4">
          <div className="space-y-2">
            <Label htmlFor="displayName" className="font-nunito">
              Nome visualizzato
            </Label>
            <Input
              id="displayName"
              value={displayName}
              onChange={e => setDisplayName(e.target.value)}
              placeholder="Il tuo nome"
              maxLength={50}
              disabled={isSaving}
              className="font-nunito"
            />
          </div>
          {error && (
            <p className="text-sm text-destructive font-nunito">{error}</p>
          )}
        </div>

        <SheetFooter>
          <Button
            onClick={handleSave}
            disabled={isSaving || !displayName.trim()}
            className="font-nunito"
          >
            {isSaving ? 'Salvataggio...' : 'Salva'}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}
```

- [ ] **Step 3.2: Esegui typecheck**

```bash
cd apps/web
pnpm typecheck 2>&1 | grep "EditProfileSheet" | head -10
```

Expected: nessun errore.

- [ ] **Step 3.3: Commit**

```bash
git add apps/web/src/components/profile/EditProfileSheet.tsx
git commit -m "feat(profile): aggiungi EditProfileSheet per editing display name"
```

---

## Task 4: Aggiorna profile/page.tsx

**Files:**
- Modify: `apps/web/src/app/(authenticated)/profile/page.tsx`

Quattro cambiamenti in un unico commit:
1. Header: sostituisce il div con initials con `AvatarUpload` (incluso `api.auth.getProfile()` via `useQuery`)
2. Header: aggiunge pulsante `EditProfileSheet`
3. `OverviewTab`: migra da `useState/useEffect` a `useQuery`
4. `AchievementsTab`: mostra `AchievementsGrid` inline

- [ ] **Step 4.1: Riscrivi profile/page.tsx**

Sostituisci l'intero file con:

```typescript
/**
 * Profile Landing Page - /profile
 *
 * Overview landing page con tre tab:
 *   - Overview: library stats + quick action links
 *   - Achievements: achievements inline
 *   - Activity: activity feed
 */

'use client';

import { useEffect, useState } from 'react';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Activity,
  BookOpen,
  ChevronRight,
  FileText,
  Gamepad2,
  Heart,
  LayoutDashboard,
  Package,
  Trophy,
} from 'lucide-react';
import Link from 'next/link';

import { ActivityFeed } from '@/components/profile/ActivityFeed';
import { AchievementsGrid } from '@/components/profile/AchievementsGrid';
import { AvatarUpload } from '@/components/profile/AvatarUpload';
import { EditProfileSheet } from '@/components/profile/EditProfileSheet';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { userKeys } from '@/hooks/queries/useCurrentUser';
import { libraryKeys } from '@/hooks/queries/useLibrary';
import { useRecentSessions } from '@/hooks/useRecentSessions';
import { useAuth } from '@/hooks/useAuth';
import { api } from '@/lib/api';
import type { UserLibraryStats } from '@/lib/api/schemas/library.schemas';
import { useCardHand } from '@/stores/use-card-hand';

// ─── Types ────────────────────────────────────────────────────────────────────

type Tab = 'overview' | 'achievements' | 'activity';

// ─── TabBar ───────────────────────────────────────────────────────────────────

function TabBar({ active, onChange }: { active: Tab; onChange: (t: Tab) => void }) {
  const tabs: { id: Tab; label: string; icon: React.ElementType }[] = [
    { id: 'overview', label: 'Overview', icon: LayoutDashboard },
    { id: 'achievements', label: 'Achievements', icon: Trophy },
    { id: 'activity', label: 'Activity', icon: Activity },
  ];

  return (
    <div className="flex border-b border-border mb-6" role="tablist">
      {tabs.map(tab => {
        const Icon = tab.icon;
        const isActive = active === tab.id;
        return (
          <button
            key={tab.id}
            role="tab"
            aria-selected={isActive}
            onClick={() => onChange(tab.id)}
            className={`flex items-center gap-2 px-4 py-3 text-sm font-nunito font-medium border-b-2 transition-colors ${
              isActive
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            }`}
          >
            <Icon className="h-4 w-4" />
            {tab.label}
          </button>
        );
      })}
    </div>
  );
}

// ─── QuickActionLink ──────────────────────────────────────────────────────────

function QuickActionLink({
  href,
  icon: Icon,
  label,
  description,
}: {
  href: string;
  icon: React.ElementType;
  label: string;
  description: string;
}) {
  return (
    <Link
      href={href}
      className="flex items-center gap-3 p-3 rounded-lg border border-border/50 hover:bg-muted/40 transition-colors group"
    >
      <div className="rounded-md bg-primary/10 p-2 shrink-0">
        <Icon className="h-4 w-4 text-primary" />
      </div>
      <div className="flex-1 min-w-0">
        <p className="font-medium text-sm font-nunito">{label}</p>
        <p className="text-xs text-muted-foreground font-nunito">{description}</p>
      </div>
      <ChevronRight className="h-4 w-4 text-muted-foreground shrink-0 group-hover:translate-x-0.5 transition-transform" />
    </Link>
  );
}

// ─── StatTile ─────────────────────────────────────────────────────────────────

function StatTile({
  icon: Icon,
  label,
  value,
  color,
}: {
  icon: React.ElementType;
  label: string;
  value: number;
  color: string;
}): React.ReactElement {
  return (
    <div className="flex items-center gap-2 p-3 rounded-lg bg-muted/30">
      <Icon className={`h-4 w-4 shrink-0 ${color}`} />
      <div>
        <p className="text-xs text-muted-foreground font-nunito">{label}</p>
        <p className="text-lg font-bold font-quicksand">{value}</p>
      </div>
    </div>
  );
}

// ─── OverviewTab ──────────────────────────────────────────────────────────────

function OverviewTab() {
  const { sessions, isLoading: sessionsLoading } = useRecentSessions(3);

  const {
    data: stats,
    isLoading,
    error,
  } = useQuery<UserLibraryStats>({
    queryKey: libraryKeys.stats(),
    queryFn: () => api.library.getStats(),
    staleTime: 2 * 60 * 1000,
  });

  return (
    <div className="space-y-6">
      {/* Library Stats */}
      <Card className="border-l-4 border-l-[hsl(262,83%,58%)] shadow-sm">
        <CardHeader className="pb-2">
          <CardTitle className="font-quicksand text-lg">Library Stats</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading && (
            <div className="grid grid-cols-3 gap-3">
              {[...Array(6)].map((_, i) => (
                <Skeleton key={i} className="h-14 w-full rounded-lg" />
              ))}
            </div>
          )}
          {error && (
            <Alert>
              <AlertDescription className="font-nunito text-sm">
                {error instanceof Error ? error.message : 'Errore nel caricamento stats.'}
              </AlertDescription>
            </Alert>
          )}
          {!isLoading && !error && stats && (
            <div className="grid grid-cols-3 gap-3">
              <StatTile icon={Gamepad2} label="Giochi" value={stats.totalGames} color="text-primary" />
              <StatTile icon={Heart} label="Preferiti" value={stats.favoriteGames} color="text-red-400" />
              <StatTile icon={BookOpen} label="Posseduti" value={stats.ownedCount ?? 0} color="text-green-500" />
              <StatTile icon={Trophy} label="Wishlist" value={stats.wishlistCount ?? 0} color="text-amber-500" />
              <StatTile icon={FileText} label="PDF caricati" value={stats.privatePdfs ?? 0} color="text-blue-500" />
              <StatTile icon={Package} label="In prestito" value={stats.inPrestitoCount ?? 0} color="text-orange-400" />
            </div>
          )}
        </CardContent>
      </Card>

      {/* Ultime partite */}
      <Card className="border-l-4 border-l-green-400 shadow-sm">
        <CardHeader className="pb-2 flex flex-row items-center justify-between">
          <CardTitle className="font-quicksand text-lg">Ultime partite</CardTitle>
          <Button asChild variant="ghost" size="sm" className="font-nunito gap-1">
            <Link href="/play-records">
              Tutte <ChevronRight className="h-3 w-3" />
            </Link>
          </Button>
        </CardHeader>
        <CardContent>
          {sessionsLoading && (
            <div className="space-y-2">
              {[...Array(3)].map((_, i) => (
                <Skeleton key={i} className="h-10 w-full rounded-lg" />
              ))}
            </div>
          )}
          {!sessionsLoading && sessions.length === 0 && (
            <p className="text-sm text-muted-foreground font-nunito">
              Nessuna partita ancora.{' '}
              <Link href="/sessions" className="underline">
                Inizia una sessione
              </Link>
            </p>
          )}
          {!sessionsLoading &&
            sessions.map(s => (
              <div
                key={s.id}
                className="flex items-center justify-between py-2 border-b border-border/30 last:border-0"
              >
                <span className="font-nunito text-sm font-medium">{s.gameName}</span>
                <span className="text-xs text-muted-foreground font-nunito">
                  {new Date(s.sessionDate).toLocaleDateString('it-IT', {
                    day: '2-digit',
                    month: 'short',
                  })}
                </span>
              </div>
            ))}
        </CardContent>
      </Card>

      {/* Quick Actions */}
      <Card className="border-l-4 border-l-amber-400 shadow-sm">
        <CardHeader className="pb-2">
          <CardTitle className="font-quicksand text-lg">Quick Actions</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-2">
            <QuickActionLink
              href="/profile/achievements"
              icon={Trophy}
              label="Achievements"
              description="Vedi badge e traguardi"
            />
            <QuickActionLink
              href="/library"
              icon={BookOpen}
              label="My Library"
              description="Sfoglia la tua collezione"
            />
            <QuickActionLink
              href="/play-records"
              icon={Gamepad2}
              label="Storia di gioco"
              description="Tutte le partite giocate"
            />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

// ─── AchievementsTab ──────────────────────────────────────────────────────────

function AchievementsTab() {
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground font-nunito">
          Badge guadagnati e progressi
        </p>
        <Button asChild variant="ghost" size="sm" className="font-nunito gap-1">
          <Link href="/profile/achievements">
            Tutti <ChevronRight className="h-3 w-3" />
          </Link>
        </Button>
      </div>
      <AchievementsGrid />
    </div>
  );
}

// ─── ActivityTab ──────────────────────────────────────────────────────────────

function ActivityTab(): React.ReactElement {
  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground font-nunito">
        Le tue ultime partite, achievement e aggiornamenti
      </p>
      <ActivityFeed />
    </div>
  );
}

// ─── Main Component ───────────────────────────────────────────────────────────

export default function ProfilePage() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const { drawCard } = useCardHand();
  const [activeTab, setActiveTab] = useState<Tab>('overview');

  const { data: profile } = useQuery({
    queryKey: [...userKeys.current(), 'profile'],
    queryFn: () => api.auth.getProfile(),
    staleTime: 5 * 60 * 1000,
  });

  useEffect(() => {
    drawCard({
      id: 'section-profile',
      entity: 'custom',
      title: 'Profile',
      href: '/profile',
    });
  }, [drawCard]);

  const displayName = profile?.displayName ?? user?.displayName ?? user?.email ?? 'Player';
  const avatarUrl = profile?.avatarUrl ?? null;

  async function handleAvatarUpload(file: File): Promise<void> {
    await api.auth.uploadAvatar(file);
    await queryClient.invalidateQueries({ queryKey: userKeys.all });
  }

  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="container mx-auto max-w-3xl">
        {/* Profile Header */}
        <div className="flex items-center gap-4 mb-8 p-6 rounded-2xl bg-white/70 backdrop-blur-md border border-border/50 shadow-sm">
          <AvatarUpload
            currentAvatarUrl={avatarUrl}
            displayName={displayName}
            onUpload={handleAvatarUpload}
            size={64}
          />
          <div className="flex-1 min-w-0">
            <h1 className="text-2xl font-bold font-quicksand truncate">{displayName}</h1>
            {user?.email && (
              <p className="text-sm text-muted-foreground font-nunito truncate">{user.email}</p>
            )}
            {user?.role && (
              <span className="inline-block mt-1 text-xs px-2 py-0.5 rounded bg-amber-100 text-amber-900 font-nunito font-medium">
                {user.role}
              </span>
            )}
          </div>
          <EditProfileSheet currentDisplayName={displayName} />
        </div>

        {/* Tabs */}
        <TabBar active={activeTab} onChange={setActiveTab} />

        {/* Tab Content */}
        {activeTab === 'overview' && <OverviewTab />}
        {activeTab === 'achievements' && <AchievementsTab />}
        {activeTab === 'activity' && <ActivityTab />}
      </div>
    </div>
  );
}
```

- [ ] **Step 4.2: Esegui typecheck**

```bash
cd apps/web
pnpm typecheck 2>&1 | grep -E "profile|page" | head -20
```

Expected: nessun errore di tipo.

- [ ] **Step 4.3: Esegui i test della page**

```bash
cd apps/web
pnpm test src/app/\(authenticated\)/profile/__tests__/page.test.tsx
```

Expected: 8/8 PASS (alcuni test potrebbero richiedere mock aggiuntivi per `api.auth.getProfile` — vedi step successivo se falliscono).

- [ ] **Step 4.4: (Se test falliscono) Aggiungi mock getProfile**

Se il test fallisce per `api.auth.getProfile`, aggiungi nel file di test:

```typescript
const mockGetProfile = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    auth: { getProfile: mockGetProfile },
    library: { getStats: mockGetStats },
  },
}));

// Nel beforeEach:
mockGetProfile.mockResolvedValue({
  id: 'user-1',
  email: 'alice@example.com',
  displayName: 'Alice Smith',
  role: 'User',
  createdAt: '2025-01-01T00:00:00Z',
  isTwoFactorEnabled: false,
  twoFactorEnabledAt: null,
  language: 'it',
  theme: 'light',
  emailNotifications: true,
  dataRetentionDays: 30,
  avatarUrl: null,
});
```

- [ ] **Step 4.5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/profile/page.tsx
git commit -m "feat(profile): AvatarUpload integrato, achievements inline, stats useQuery, EditProfileSheet"
```

---

## Task 5: Aggiorna index.ts ed esegui test finali

**Files:**
- Modify: `apps/web/src/components/profile/index.ts`

- [ ] **Step 5.1: Aggiorna exports**

Leggi il file attuale e aggiungi le nuove esportazioni:

```typescript
export { ActivityFeed } from './ActivityFeed';
export { AvatarUpload } from './AvatarUpload';
export { ClaimGuestGames } from './ClaimGuestGames';
export { AchievementsGrid } from './AchievementsGrid';
export { EditProfileSheet } from './EditProfileSheet';
```

- [ ] **Step 5.2: Esegui lint**

```bash
cd apps/web
pnpm lint -- --max-warnings=0 src/components/profile/ src/app/\(authenticated\)/profile/
```

Expected: 0 errori e 0 warning.

- [ ] **Step 5.3: Esegui tutti i test del profilo**

```bash
cd apps/web
pnpm test src/app/\(authenticated\)/profile/ src/components/profile/
```

Expected: tutti i test PASS.

- [ ] **Step 5.4: Typecheck finale**

```bash
cd apps/web
pnpm typecheck
```

Expected: 0 errori.

- [ ] **Step 5.5: Commit finale**

```bash
git add apps/web/src/components/profile/index.ts
git commit -m "chore(profile): aggiorna exports index.ts"
```

---

## Checklist Self-Review

- [x] **C-1** (test rotti) → Task 1 ✅
- [x] **C-2** (AvatarUpload non integrato) → Task 4 ✅
- [x] **I-1** (stats useQuery) → Task 4 ✅
- [x] **I-2** (achievements inline) → Task 2 + 4 ✅
- [x] **I-3** (editing display name) → Task 3 + 4 ✅
- [x] **M-2** (avatar size dynamic con template literal Tailwind) → Task 4 usa `size={64}` come number prop, il componente usa `style={{width, height}}` internamente ✅

**Nota su bio:** Il backend (`UpdateProfileRequest`) non ha campo `bio` — non implementato per rispettare YAGNI.

**Nota su `AuthUser` vs `UserProfile`:** `useAuth()` restituisce `AuthUser` (senza avatarUrl). Il profile fetch (`api.auth.getProfile()`) restituisce `UserProfile` con avatarUrl. I due vengono usati in parallelo: `user` per email/role (già in cache auth), `profile` per avatarUrl/displayName più aggiornato.

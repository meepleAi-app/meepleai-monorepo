# Phase 3: Library & Game Detail — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the library page as a mobile-first game grid with segmented control, and the game detail page as a vertical scroll with hero section, Rules & AI section (one-tap PDF upload → auto agent), and collapsible info sections.

**Architecture:** Create `LibraryMobile` page replacing the tab-based `_content.tsx`, and `GameDetailMobile` page replacing the 3-zone Game Table layout. Reuse ALL existing hooks (`useLibrary`, `useLibraryGameDetail`, `useToggleLibraryFavorite`). Wrap existing `PdfUploadModal` logic into a `PdfUploadSheet` bottom sheet. Create `AiReadyBadge` for KB status display.

**Tech Stack:** Next.js 16, React 19, Tailwind CSS 4, TanStack Query (existing hooks), Phase 1 components

**Spec:** `docs/superpowers/specs/2026-03-28-user-pages-redesign-design.md` — Section 3

---

## File Structure

### New Files

| File | Responsibility |
|------|---------------|
| `src/app/(authenticated)/library/library-mobile.tsx` | Mobile-first library page with segmented control + game grid |
| `src/app/(authenticated)/library/games/[gameId]/game-detail-mobile.tsx` | Vertical scroll game detail with hero + Rules & AI section |
| `src/components/library/SegmentedControl.tsx` | Reusable segmented control (Collezione/Privati/Wishlist) |
| `src/components/library/SegmentedControl.test.tsx` | Tests |
| `src/components/library/LibraryFilterSheet.tsx` | Filters in a bottom sheet (wraps existing filter logic) |
| `src/components/library/PdfUploadSheet.tsx` | PDF upload as bottom sheet (camera/file) |
| `src/components/library/AiReadySection.tsx` | Rules & AI section for game detail (status + CTA) |
| `src/components/library/AiReadySection.test.tsx` | Tests |

### Modified Files

| File | Changes |
|------|---------|
| `src/app/(authenticated)/library/page.tsx` | Switch to LibraryMobile |
| `src/app/(authenticated)/library/games/[gameId]/page.tsx` | Switch to GameDetailMobile |

### All paths relative to `apps/web/`

---

## Task 1: Create SegmentedControl Component

**Files:**
- Create: `src/components/library/SegmentedControl.tsx`
- Create: `src/components/library/SegmentedControl.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/components/library/SegmentedControl.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SegmentedControl } from './SegmentedControl';

const segments = [
  { id: 'collection', label: 'Collezione' },
  { id: 'private', label: 'Privati' },
  { id: 'wishlist', label: 'Wishlist' },
];

describe('SegmentedControl', () => {
  it('renders all segments', () => {
    render(<SegmentedControl segments={segments} activeId="collection" onChange={() => {}} />);
    expect(screen.getByText('Collezione')).toBeInTheDocument();
    expect(screen.getByText('Privati')).toBeInTheDocument();
    expect(screen.getByText('Wishlist')).toBeInTheDocument();
  });

  it('highlights active segment', () => {
    render(<SegmentedControl segments={segments} activeId="private" onChange={() => {}} />);
    const btn = screen.getByText('Privati').closest('button');
    expect(btn).toHaveAttribute('aria-selected', 'true');
  });

  it('calls onChange when segment is clicked', () => {
    const onChange = vi.fn();
    render(<SegmentedControl segments={segments} activeId="collection" onChange={onChange} />);
    fireEvent.click(screen.getByText('Wishlist'));
    expect(onChange).toHaveBeenCalledWith('wishlist');
  });

  it('renders with glass styling', () => {
    const { container } = render(
      <SegmentedControl segments={segments} activeId="collection" onChange={() => {}} />
    );
    expect(container.firstChild).toHaveClass('glass');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/library/SegmentedControl.test.tsx`

- [ ] **Step 3: Write implementation**

Create `src/components/library/SegmentedControl.tsx`:

```tsx
'use client';

import React from 'react';
import { cn } from '@/lib/utils';

export interface Segment {
  id: string;
  label: string;
  count?: number;
}

export interface SegmentedControlProps {
  segments: Segment[];
  activeId: string;
  onChange: (id: string) => void;
  className?: string;
}

export function SegmentedControl({ segments, activeId, onChange, className }: SegmentedControlProps) {
  return (
    <div
      role="tablist"
      className={cn(
        'gaming-glass inline-flex w-full gap-1 rounded-lg p-1',
        className
      )}
    >
      {segments.map((seg) => {
        const active = seg.id === activeId;
        return (
          <button
            key={seg.id}
            type="button"
            role="tab"
            aria-selected={active}
            tabIndex={active ? 0 : -1}
            onClick={() => onChange(seg.id)}
            className={cn(
              'flex-1 rounded-md px-3 py-2 text-sm font-medium transition-all',
              active
                ? 'bg-white/10 text-[var(--gaming-text-primary)] shadow-sm'
                : 'text-[var(--gaming-text-secondary)] hover:text-[var(--gaming-text-primary)]'
            )}
          >
            {seg.label}
            {seg.count !== undefined && (
              <span className="ml-1 text-xs opacity-60">{seg.count}</span>
            )}
          </button>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 4: Run test, verify pass**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/library/SegmentedControl*
git commit -m "feat(library): add SegmentedControl component"
```

---

## Task 2: Create LibraryFilterSheet Component

**Files:**
- Create: `src/components/library/LibraryFilterSheet.tsx`

Wraps existing filter logic into a BottomSheet. Reuses filter concepts from `LibraryFilters.tsx`.

- [ ] **Step 1: Read existing LibraryFilters.tsx to understand filter props/state**

Read `src/components/library/LibraryFilters.tsx` to understand what filter state exists.

- [ ] **Step 2: Create LibraryFilterSheet**

```tsx
'use client';

import React from 'react';
import { BottomSheet } from '@/components/ui/overlays/BottomSheet';
import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { cn } from '@/lib/utils';

export interface LibraryFilters {
  search: string;
  state: string | null; // 'Owned' | 'Wishlist' | 'Nuovo' | 'InPrestito' | null (all)
  sortBy: 'addedAt' | 'title' | 'favorite';
  favoritesOnly: boolean;
}

const stateOptions = [
  { id: null, label: 'Tutti' },
  { id: 'Owned', label: 'Posseduti' },
  { id: 'Wishlist', label: 'Wishlist' },
  { id: 'Nuovo', label: 'Nuovi' },
  { id: 'InPrestito', label: 'In Prestito' },
] as const;

const sortOptions = [
  { id: 'addedAt' as const, label: 'Data aggiunta' },
  { id: 'title' as const, label: 'Nome' },
  { id: 'favorite' as const, label: 'Preferiti' },
];

export interface LibraryFilterSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  filters: LibraryFilters;
  onApply: (filters: LibraryFilters) => void;
}

export function LibraryFilterSheet({ open, onOpenChange, filters, onApply }: LibraryFilterSheetProps) {
  const [local, setLocal] = React.useState(filters);

  React.useEffect(() => {
    if (open) setLocal(filters);
  }, [open, filters]);

  const handleApply = () => {
    onApply(local);
    onOpenChange(false);
  };

  return (
    <BottomSheet open={open} onOpenChange={onOpenChange} title="Filtra">
      <div className="flex flex-col gap-6">
        {/* State filter */}
        <div>
          <p className="mb-2 text-xs font-medium uppercase text-[var(--gaming-text-secondary)]">
            Stato
          </p>
          <div className="flex flex-wrap gap-2">
            {stateOptions.map((opt) => (
              <button
                key={opt.id ?? 'all'}
                type="button"
                onClick={() => setLocal((f) => ({ ...f, state: opt.id }))}
                className={cn(
                  'rounded-full px-3 py-1.5 text-sm transition-colors',
                  local.state === opt.id
                    ? 'bg-amber-500/20 text-amber-400 border border-amber-500/40'
                    : 'bg-white/5 text-[var(--gaming-text-secondary)] border border-transparent'
                )}
              >
                {opt.label}
              </button>
            ))}
          </div>
        </div>

        {/* Sort */}
        <div>
          <p className="mb-2 text-xs font-medium uppercase text-[var(--gaming-text-secondary)]">
            Ordina per
          </p>
          <div className="flex flex-wrap gap-2">
            {sortOptions.map((opt) => (
              <button
                key={opt.id}
                type="button"
                onClick={() => setLocal((f) => ({ ...f, sortBy: opt.id }))}
                className={cn(
                  'rounded-full px-3 py-1.5 text-sm transition-colors',
                  local.sortBy === opt.id
                    ? 'bg-amber-500/20 text-amber-400 border border-amber-500/40'
                    : 'bg-white/5 text-[var(--gaming-text-secondary)] border border-transparent'
                )}
              >
                {opt.label}
              </button>
            ))}
          </div>
        </div>

        {/* Apply button */}
        <GradientButton fullWidth onClick={handleApply}>
          Applica Filtri
        </GradientButton>
      </div>
    </BottomSheet>
  );
}
```

- [ ] **Step 3: Verify compilation**

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/library/LibraryFilterSheet.tsx
git commit -m "feat(library): add LibraryFilterSheet bottom sheet"
```

---

## Task 3: Create AiReadySection Component

**Files:**
- Create: `src/components/library/AiReadySection.tsx`
- Create: `src/components/library/AiReadySection.test.tsx`

The "Rules & AI" section for game detail. Shows PDF upload CTA, processing status, or "AI Ready" with chat link.

- [ ] **Step 1: Read KbStatusBadge.tsx and KbStatusPanel.tsx to understand KB status data**

Understand the `usePdfProcessingStatus` hook and status states.

- [ ] **Step 2: Write the failing test**

Create `src/components/library/AiReadySection.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { AiReadySection } from './AiReadySection';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

describe('AiReadySection', () => {
  it('shows upload CTA when no PDF', () => {
    render(
      <AiReadySection gameId="123" hasCustomPdf={false} hasRagAccess={false} onUploadClick={() => {}} />
    );
    expect(screen.getByRole('button', { name: /carica regolamento/i })).toBeInTheDocument();
  });

  it('shows AI ready badge when RAG access is true', () => {
    render(
      <AiReadySection gameId="123" hasCustomPdf={true} hasRagAccess={true} onUploadClick={() => {}} />
    );
    expect(screen.getByText(/ai pronta/i)).toBeInTheDocument();
  });

  it('shows chat button when AI is ready', () => {
    render(
      <AiReadySection gameId="123" hasCustomPdf={true} hasRagAccess={true} onUploadClick={() => {}} />
    );
    expect(screen.getByRole('link', { name: /chiedi alle regole/i })).toBeInTheDocument();
  });

  it('shows processing state when PDF exists but no RAG', () => {
    render(
      <AiReadySection gameId="123" hasCustomPdf={true} hasRagAccess={false} onUploadClick={() => {}} />
    );
    expect(screen.getByText(/elaborazione/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Write implementation**

Create `src/components/library/AiReadySection.tsx`:

```tsx
'use client';

import React from 'react';
import Link from 'next/link';
import { GlassCard } from '@/components/ui/surfaces/GlassCard';
import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { Upload, MessageCircle, Loader2, Sparkles } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface AiReadySectionProps {
  gameId: string;
  hasCustomPdf: boolean;
  hasRagAccess: boolean;
  onUploadClick: () => void;
}

export function AiReadySection({ gameId, hasCustomPdf, hasRagAccess, onUploadClick }: AiReadySectionProps) {
  // No PDF uploaded
  if (!hasCustomPdf) {
    return (
      <GlassCard className="p-4">
        <div className="flex items-center gap-3 mb-3">
          <Sparkles className="h-5 w-5 text-purple-400" />
          <h3 className="text-sm font-semibold text-[var(--gaming-text-primary)]">
            Regole & AI
          </h3>
        </div>
        <p className="mb-3 text-sm text-[var(--gaming-text-secondary)]">
          Carica il regolamento per chiedere all&apos;AI come si gioca
        </p>
        <GradientButton fullWidth onClick={onUploadClick}>
          <Upload className="h-4 w-4" />
          Carica Regolamento
        </GradientButton>
      </GlassCard>
    );
  }

  // PDF uploaded, processing
  if (!hasRagAccess) {
    return (
      <GlassCard className="p-4">
        <div className="flex items-center gap-3 mb-2">
          <Loader2 className="h-5 w-5 animate-spin text-amber-400" />
          <h3 className="text-sm font-semibold text-[var(--gaming-text-primary)]">
            Regole & AI
          </h3>
        </div>
        <p className="text-sm text-[var(--gaming-text-secondary)]">
          Elaborazione del regolamento in corso...
        </p>
      </GlassCard>
    );
  }

  // AI Ready
  return (
    <GlassCard className="p-4">
      <div className="flex items-center gap-3 mb-3">
        <div className="flex h-6 w-6 items-center justify-center rounded-full bg-green-500/20">
          <Sparkles className="h-3.5 w-3.5 text-green-400" />
        </div>
        <h3 className="text-sm font-semibold text-[var(--gaming-text-primary)]">
          AI Pronta
        </h3>
      </div>
      <Link
        href={`/chat?gameId=${gameId}`}
        aria-label="Chiedi alle Regole"
        className={cn(
          'flex items-center justify-center gap-2 w-full',
          'rounded-lg bg-purple-500/20 border border-purple-500/30',
          'px-4 py-2.5 text-sm font-medium text-purple-300',
          'transition-colors hover:bg-purple-500/30'
        )}
      >
        <MessageCircle className="h-4 w-4" />
        Chiedi alle Regole
      </Link>
    </GlassCard>
  );
}
```

- [ ] **Step 4: Run tests, verify pass**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/library/AiReadySection*
git commit -m "feat(library): add AiReadySection with upload/processing/ready states"
```

---

## Task 4: Create LibraryMobile Page

**Files:**
- Create: `src/app/(authenticated)/library/library-mobile.tsx`
- Modify: `src/app/(authenticated)/library/page.tsx`

- [ ] **Step 1: Read current library page.tsx and _content.tsx**

Understand auth guards, metadata, how tabs currently work.

- [ ] **Step 2: Create library-mobile.tsx**

```tsx
'use client';

import React, { useState, useCallback } from 'react';
import { Search, SlidersHorizontal } from 'lucide-react';
import { cn } from '@/lib/utils';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { SegmentedControl } from '@/components/library/SegmentedControl';
import { LibraryFilterSheet, type LibraryFilters } from '@/components/library/LibraryFilterSheet';
import { FullScreenSearch } from '@/components/search/FullScreenSearch';
import { GamePreviewSheet } from '@/components/search/GamePreviewSheet';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { useLibrary, useLibraryStats } from '@/hooks/queries/useLibrary';
import type { BggGameResult } from '@/components/search/FullScreenSearch';

const segments = [
  { id: 'collection', label: 'Collezione' },
  { id: 'private', label: 'Privati' },
  { id: 'wishlist', label: 'Wishlist' },
];

const defaultFilters: LibraryFilters = {
  search: '',
  state: null,
  sortBy: 'addedAt',
  favoritesOnly: false,
};

export function LibraryMobile() {
  const [activeSegment, setActiveSegment] = useState('collection');
  const [filters, setFilters] = useState<LibraryFilters>(defaultFilters);
  const [filterSheetOpen, setFilterSheetOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [selectedGame, setSelectedGame] = useState<BggGameResult | null>(null);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');

  // Map segment to API params — read useLibrary params to adjust
  const { data, isLoading } = useLibrary({
    page: 1,
    pageSize: 50,
    search: searchQuery || undefined,
    sortBy: filters.sortBy,
    sortDescending: filters.sortBy === 'addedAt',
    state: filters.state ?? undefined,
    // Adjust params based on actual hook interface
  });

  const { data: stats } = useLibraryStats();

  const games = data?.items ?? [];

  const handleSelectGame = useCallback((game: BggGameResult) => {
    setSelectedGame(game);
    setPreviewOpen(true);
  }, []);

  return (
    <div className="min-h-screen bg-[var(--gaming-bg-base)]">
      <MobileHeader
        title="La Mia Libreria"
        subtitle={stats ? `${stats.totalGames ?? 0} giochi` : undefined}
        rightActions={
          <button
            type="button"
            onClick={() => setFilterSheetOpen(true)}
            aria-label="Filtri"
            className="flex h-9 w-9 items-center justify-center rounded-full text-[var(--gaming-text-secondary)] hover:bg-white/5"
          >
            <SlidersHorizontal className="h-5 w-5" />
          </button>
        }
      />

      <div className="flex flex-col gap-4 pb-20 pt-4">
        {/* Segmented control */}
        <div className="px-4">
          <SegmentedControl
            segments={segments}
            activeId={activeSegment}
            onChange={setActiveSegment}
          />
        </div>

        {/* Search bar */}
        <div className="px-4">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--gaming-text-secondary)]" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Cerca nella libreria..."
              className={cn(
                'h-10 w-full rounded-lg pl-10 pr-4',
                'bg-white/5 border border-[var(--gaming-border-glass)]',
                'text-sm text-[var(--gaming-text-primary)] placeholder:text-[var(--gaming-text-secondary)]',
                'focus:outline-none focus:border-amber-500/50'
              )}
            />
          </div>
        </div>

        {/* Game grid */}
        {isLoading ? (
          <div className="grid grid-cols-2 gap-3 px-4">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className="aspect-[3/4] animate-pulse rounded-lg bg-white/5" />
            ))}
          </div>
        ) : games.length === 0 ? (
          <div className="flex flex-col items-center gap-4 px-4 py-12 text-center">
            <p className="text-lg">🎲</p>
            <p className="text-sm text-[var(--gaming-text-secondary)]">
              Nessun gioco trovato. Cerca e aggiungi il tuo primo gioco!
            </p>
            <button
              type="button"
              onClick={() => setSearchOpen(true)}
              className="text-sm font-medium text-amber-400"
            >
              Cerca su BoardGameGeek
            </button>
          </div>
        ) : (
          <div className="grid grid-cols-2 gap-3 px-4">
            {games.map((entry) => (
              <MeepleCard
                key={entry.id}
                entity="game"
                variant="grid"
                title={entry.gameTitle}
                imageUrl={entry.gameImageUrl ?? entry.gameIconUrl}
                rating={entry.averageRating}
                ratingMax={10}
                detailHref={`/library/games/${entry.gameId}`}
              />
            ))}
          </div>
        )}
      </div>

      {/* FAB — handled by MobileBottomNav's + button, but also add search overlay */}
      <LibraryFilterSheet
        open={filterSheetOpen}
        onOpenChange={setFilterSheetOpen}
        filters={filters}
        onApply={setFilters}
      />

      <FullScreenSearch
        open={searchOpen}
        onClose={() => setSearchOpen(false)}
        onSelectGame={handleSelectGame}
      />

      <GamePreviewSheet
        open={previewOpen}
        game={selectedGame}
        onOpenChange={setPreviewOpen}
        onAdded={() => {
          setTimeout(() => {
            setPreviewOpen(false);
            setSelectedGame(null);
          }, 1500);
        }}
      />
    </div>
  );
}
```

**IMPORTANT:** After writing, read `useLibrary` and `useLibraryStats` hooks to verify actual param names and response field names. Adjust accordingly.

- [ ] **Step 3: Update page.tsx to use LibraryMobile**

Replace the main content in library `page.tsx` with `LibraryMobile`.

- [ ] **Step 4: Verify compilation**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/library/
git commit -m "feat(library): add mobile-first library page with grid and filters"
```

---

## Task 5: Create GameDetailMobile Page

**Files:**
- Create: `src/app/(authenticated)/library/games/[gameId]/game-detail-mobile.tsx`
- Create: `src/components/library/PdfUploadSheet.tsx`
- Modify: `src/app/(authenticated)/library/games/[gameId]/page.tsx`

- [ ] **Step 1: Read useLibraryGameDetail hook and PdfUploadModal**

Understand the data shape returned and the PDF upload flow.

- [ ] **Step 2: Create PdfUploadSheet (thin wrapper)**

Create `src/components/library/PdfUploadSheet.tsx`:

```tsx
'use client';

import React, { useRef } from 'react';
import { BottomSheet } from '@/components/ui/overlays/BottomSheet';
import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { Camera, FileText } from 'lucide-react';
import { api } from '@/lib/api';
import { useToast } from '@/hooks/useToast'; // verify actual path

export interface PdfUploadSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  gameId: string;
  onUploaded: () => void;
}

export function PdfUploadSheet({ open, onOpenChange, gameId, onUploaded }: PdfUploadSheetProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = React.useState(false);
  const { toast } = useToast(); // verify

  const handleFileSelect = async (file: File) => {
    if (!file || file.type !== 'application/pdf') {
      toast({ title: 'Errore', description: 'Seleziona un file PDF', variant: 'destructive' });
      return;
    }
    if (file.size > 50 * 1024 * 1024) {
      toast({ title: 'Errore', description: 'File troppo grande (max 50MB)', variant: 'destructive' });
      return;
    }

    setUploading(true);
    try {
      await api.pdf.uploadPdf(gameId, file);
      toast({ title: 'Regolamento caricato!', description: 'Elaborazione in corso...' });
      onUploaded();
      onOpenChange(false);
    } catch (error) {
      toast({ title: 'Errore upload', description: 'Riprova più tardi', variant: 'destructive' });
    } finally {
      setUploading(false);
    }
  };

  return (
    <BottomSheet open={open} onOpenChange={onOpenChange} title="Carica Regolamento">
      <div className="flex flex-col gap-3">
        <GradientButton
          fullWidth
          size="lg"
          loading={uploading}
          onClick={() => fileInputRef.current?.click()}
        >
          <FileText className="h-5 w-5" />
          Scegli File PDF
        </GradientButton>

        <p className="text-center text-xs text-[var(--gaming-text-secondary)]">
          PDF, max 50MB
        </p>

        <input
          ref={fileInputRef}
          type="file"
          accept="application/pdf"
          className="hidden"
          onChange={(e) => {
            const file = e.target.files?.[0];
            if (file) handleFileSelect(file);
          }}
        />
      </div>
    </BottomSheet>
  );
}
```

**IMPORTANT:** Verify `api.pdf.uploadPdf` actual call signature. It might be `api.pdf.uploadPdf(gameId, file, onProgress)` or different. Also verify toast hook import.

- [ ] **Step 3: Create game-detail-mobile.tsx**

Create `src/app/(authenticated)/library/games/[gameId]/game-detail-mobile.tsx`:

```tsx
'use client';

import React, { useState } from 'react';
import { useRouter } from 'next/navigation';
import { cn } from '@/lib/utils';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { GlassCard } from '@/components/ui/surfaces/GlassCard';
import { FavoriteToggle } from '@/components/library/FavoriteToggle';
import { AiReadySection } from '@/components/library/AiReadySection';
import { PdfUploadSheet } from '@/components/library/PdfUploadSheet';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import { MessageCircle, Share2, BookOpen, Users, Clock, Star } from 'lucide-react';

interface GameDetailMobileProps {
  gameId: string;
}

export function GameDetailMobile({ gameId }: GameDetailMobileProps) {
  const router = useRouter();
  const { data: game, isLoading, refetch } = useLibraryGameDetail(gameId);
  const [uploadSheetOpen, setUploadSheetOpen] = useState(false);

  if (isLoading) {
    return (
      <div className="min-h-screen bg-[var(--gaming-bg-base)]">
        <MobileHeader title="..." onBack={() => router.back()} />
        <div className="flex items-center justify-center py-20">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-amber-400 border-t-transparent" />
        </div>
      </div>
    );
  }

  if (!game) {
    return (
      <div className="min-h-screen bg-[var(--gaming-bg-base)]">
        <MobileHeader title="Gioco non trovato" onBack={() => router.back()} />
        <p className="px-4 py-12 text-center text-sm text-[var(--gaming-text-secondary)]">
          Questo gioco non è nella tua libreria
        </p>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-[var(--gaming-bg-base)]">
      <MobileHeader title={game.gameTitle} onBack={() => router.back()} />

      <div className="flex flex-col gap-4 pb-20 pt-4">
        {/* Hero section */}
        <div className="flex gap-4 px-4">
          {game.gameImageUrl ? (
            <img
              src={game.gameImageUrl}
              alt={game.gameTitle}
              className="h-36 w-28 shrink-0 rounded-lg object-cover shadow-lg"
            />
          ) : (
            <div className="flex h-36 w-28 shrink-0 items-center justify-center rounded-lg bg-white/5 text-4xl">
              🎲
            </div>
          )}
          <div className="flex flex-col justify-center gap-2">
            <h1 className="text-xl font-bold text-[var(--gaming-text-primary)]">{game.gameTitle}</h1>
            {game.gamePublisher && (
              <p className="text-sm text-[var(--gaming-text-secondary)]">{game.gamePublisher}</p>
            )}
            {/* ManaPips - players, time, complexity */}
            <div className="flex flex-wrap gap-3 text-xs text-[var(--gaming-text-secondary)]">
              {game.minPlayers && (
                <span className="flex items-center gap-1">
                  <Users className="h-3.5 w-3.5" />
                  {game.minPlayers}{game.maxPlayers && game.maxPlayers !== game.minPlayers ? `-${game.maxPlayers}` : ''}
                </span>
              )}
              {game.playingTimeMinutes && (
                <span className="flex items-center gap-1">
                  <Clock className="h-3.5 w-3.5" />
                  {game.playingTimeMinutes} min
                </span>
              )}
              {game.averageRating && (
                <span className="flex items-center gap-1">
                  <Star className="h-3.5 w-3.5 text-amber-400" />
                  {Number(game.averageRating).toFixed(1)}
                </span>
              )}
            </div>
          </div>
        </div>

        {/* Action bar */}
        <div className="flex items-center justify-around border-y border-[var(--gaming-border-glass)] px-4 py-2">
          <FavoriteToggle
            gameId={gameId}
            isFavorite={game.isFavorite}
            gameTitle={game.gameTitle}
          />
          <a
            href={`/chat?gameId=${gameId}`}
            className="flex flex-col items-center gap-1 text-[var(--gaming-text-secondary)]"
          >
            <MessageCircle className="h-5 w-5" />
            <span className="text-[10px]">Chat AI</span>
          </a>
          <button
            type="button"
            className="flex flex-col items-center gap-1 text-[var(--gaming-text-secondary)]"
          >
            <BookOpen className="h-5 w-5" />
            <span className="text-[10px]">Regole</span>
          </button>
          <button
            type="button"
            className="flex flex-col items-center gap-1 text-[var(--gaming-text-secondary)]"
          >
            <Share2 className="h-5 w-5" />
            <span className="text-[10px]">Condividi</span>
          </button>
        </div>

        {/* Rules & AI section */}
        <div className="px-4">
          <AiReadySection
            gameId={gameId}
            hasCustomPdf={game.hasCustomPdf}
            hasRagAccess={game.hasRagAccess}
            onUploadClick={() => setUploadSheetOpen(true)}
          />
        </div>

        {/* Recent sessions */}
        {game.recentSessions && game.recentSessions.length > 0 && (
          <div className="px-4">
            <GlassCard className="p-4">
              <h3 className="mb-3 text-sm font-semibold text-[var(--gaming-text-primary)]">
                Le Tue Sessioni
              </h3>
              <div className="flex flex-col gap-2">
                {game.recentSessions.slice(0, 3).map((session) => (
                  <div
                    key={session.id}
                    className="flex items-center justify-between rounded-lg bg-white/5 px-3 py-2"
                  >
                    <span className="text-sm text-[var(--gaming-text-primary)]">
                      {new Date(session.playedAt).toLocaleDateString('it-IT')}
                    </span>
                    <span className="text-xs text-[var(--gaming-text-secondary)]">
                      {session.durationFormatted}
                    </span>
                  </div>
                ))}
              </div>
            </GlassCard>
          </div>
        )}

        {/* Info section */}
        {game.description && (
          <div className="px-4">
            <GlassCard className="p-4">
              <h3 className="mb-2 text-sm font-semibold text-[var(--gaming-text-primary)]">Info</h3>
              <p className="text-sm leading-relaxed text-[var(--gaming-text-secondary)]">
                {game.description}
              </p>
            </GlassCard>
          </div>
        )}
      </div>

      {/* PDF Upload Sheet */}
      <PdfUploadSheet
        open={uploadSheetOpen}
        onOpenChange={setUploadSheetOpen}
        gameId={gameId}
        onUploaded={() => refetch()}
      />
    </div>
  );
}
```

**IMPORTANT:** Read actual `useLibraryGameDetail` return to verify all field names. Adjust accordingly.

- [ ] **Step 4: Update game detail page.tsx**

Replace main content with `GameDetailMobile`, passing `gameId` from params.

- [ ] **Step 5: Verify compilation**

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/library/ apps/web/src/components/library/PdfUploadSheet.tsx
git commit -m "feat(library): add mobile game detail with hero, AI section, PDF upload"
```

---

## Task 6: Integration Verification

- [ ] **Step 1: Run all new tests**

Run: `cd apps/web && pnpm vitest run src/components/library/SegmentedControl.test.tsx src/components/library/AiReadySection.test.tsx`

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && npx tsc --noEmit`

- [ ] **Step 3: Run lint**

Run: `cd apps/web && pnpm lint`

- [ ] **Step 4: Commit any fixes**

---

## Summary

| Task | Component | New Tests | Status |
|------|-----------|-----------|--------|
| 1 | SegmentedControl | 4 | ☐ |
| 2 | LibraryFilterSheet | 0 (visual) | ☐ |
| 3 | AiReadySection | 4 | ☐ |
| 4 | LibraryMobile page + wiring | 0 (integration) | ☐ |
| 5 | GameDetailMobile + PdfUploadSheet + wiring | 0 (integration) | ☐ |
| 6 | Integration verification | — | ☐ |

**Total new tests: 8**
**Total new files: 8**
**Total modified files: 2**

**Key reuse**: `useLibrary`, `useLibraryStats`, `useLibraryGameDetail`, `FavoriteToggle`, `MeepleCard`, `BottomSheet`, `GlassCard`, `GradientButton`, `MobileHeader`, `FullScreenSearch`, `GamePreviewSheet`.

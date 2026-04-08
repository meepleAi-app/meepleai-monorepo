'use client';

import { Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { SectionBlock } from '@/components/ui/SectionBlock';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import { cn } from '@/lib/utils';

import { MeepleLibraryGameCard } from './MeepleLibraryGameCard';

// ── CTA card — "Crea gioco" at the end of the custom games section ──────────

function CreateGameCtaCard() {
  const router = useRouter();

  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-2',
        'w-full sm:w-[140px] flex-shrink-0 rounded-xl',
        'bg-[#161b22] border border-dashed border-[#30363d]',
        'min-h-[80px] sm:min-h-[160px]',
        'cursor-pointer transition-all duration-200',
        'hover:border-[#58a6ff] hover:bg-[#1c2128]',
        'focus:outline-none focus:ring-1 focus:ring-[#58a6ff]'
      )}
      onClick={() => router.push('/library/private/add')}
      onKeyDown={e => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          router.push('/library/private/add');
        }
      }}
      role="button"
      tabIndex={0}
      aria-label="Crea un gioco personalizzato"
      data-testid="create-game-cta"
    >
      <Plus className="w-5 h-5 text-[#58a6ff]" aria-hidden="true" />
      <span className="text-[10px] font-medium text-[#58a6ff] text-center px-2">Crea gioco</span>
    </div>
  );
}

// ── Game card renderer ──────────────────────────────────────────────────────
// Delegates to MeepleLibraryGameCard adapter which provides navItems with
// KB/Agent/Chat/Sessions drawers wired automatically.

function LibraryGameCard({
  entry,
  variant,
}: {
  entry: UserLibraryEntry;
  variant: 'grid' | 'list';
}) {
  return (
    <MeepleLibraryGameCard
      game={entry}
      variant={variant}
      onConfigureAgent={() => {}}
      onUploadPdf={() => {}}
      onEditNotes={() => {}}
      onRemove={() => {}}
    />
  );
}

// ── Game grid/list container ────────────────────────────────────────────────

function GameListContainer({
  items,
  effectiveView,
  emptyMessage,
}: {
  items: UserLibraryEntry[];
  effectiveView: 'grid' | 'list';
  emptyMessage: string;
}) {
  if (items.length === 0) {
    return <p className="text-sm text-muted-foreground py-4">{emptyMessage}</p>;
  }

  return (
    <div
      className={cn(
        effectiveView === 'grid'
          ? 'grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3'
          : 'flex flex-col gap-2'
      )}
    >
      {items.map(entry => (
        <LibraryGameCard key={entry.id} entry={entry} variant={effectiveView} />
      ))}
    </div>
  );
}

// ── Main composite component ─────────────────────────────────────────────────

export interface LibraryGameGridProps {
  filteredCatalog: UserLibraryEntry[];
  filteredCustom: UserLibraryEntry[];
  effectiveView: 'grid' | 'list';
  searchQuery: string;
}

/**
 * LibraryGameGrid — renders two sections:
 *   1. "Dal Catalogo": shared catalog games
 *   2. "Giochi Personalizzati": private/custom games + CreateGameCtaCard
 */
export function LibraryGameGrid({
  filteredCatalog,
  filteredCustom,
  effectiveView,
  searchQuery,
}: LibraryGameGridProps) {
  const query = searchQuery.toLowerCase().trim();

  return (
    <>
      {/* Section 1: Shared catalog games */}
      {(filteredCatalog.length > 0 || !query) && (
        <SectionBlock icon={'\ud83d\udcda'} title="Dal Catalogo">
          <GameListContainer
            items={filteredCatalog}
            effectiveView={effectiveView}
            emptyMessage="Nessun gioco del catalogo corrisponde alla ricerca."
          />
        </SectionBlock>
      )}

      {/* Section 2: Private/custom games */}
      {(filteredCustom.length > 0 || !query) && (
        <SectionBlock icon={'\ud83c\udfae'} title="Giochi Personalizzati">
          {filteredCustom.length === 0 && !query ? (
            <CreateGameCtaCard />
          ) : (
            <>
              <GameListContainer
                items={filteredCustom}
                effectiveView={effectiveView}
                emptyMessage="Nessun gioco personalizzato corrisponde alla ricerca."
              />
              {filteredCustom.length > 0 && (
                <div className="mt-3">
                  <CreateGameCtaCard />
                </div>
              )}
            </>
          )}
        </SectionBlock>
      )}
    </>
  );
}

/**
 * PersonalLibraryPage Component
 *
 * Vetrina layout for the user's personal library.
 * Splits games into two sections:
 *   1. Shared catalog games (sharedGameId exists / isPrivateGame = false)
 *   2. Private/custom games (privateGameId exists / isPrivateGame = true)
 *
 * Each section uses TavoloSection + ShelfRow + ShelfCard for a horizontal
 * browse experience consistent with the MeepleAI dashboard design system.
 */

'use client';

import { useMemo, useState } from 'react';

import { BookOpen, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { TavoloSection } from '@/components/dashboard-v2/tavolo';
import { EmptyState } from '@/components/empty-state/EmptyState';
import { ShelfCard, ShelfRow } from '@/components/library';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { cn } from '@/lib/utils';

import { LibraryToolbar } from './LibraryToolbar';

// ============================================================================
// CTA card — "Crea gioco" at the end of the custom games row
// ============================================================================

function CreateGameCtaCard() {
  const router = useRouter();

  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-2',
        'w-[140px] flex-shrink-0 rounded-xl',
        'bg-[#161b22] border border-dashed border-[#30363d]',
        'min-h-[160px]',
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

// ============================================================================
// Main component
// ============================================================================

export interface PersonalLibraryPageProps {
  /** Additional CSS classes for the root element */
  className?: string;
}

/**
 * PersonalLibraryPage — horizontal vetrina layout for the user's game library.
 *
 * Renders two TavoloSection rows:
 * - "Dal Catalogo": shared catalog games added to the library
 * - "Giochi Personalizzati": private/custom games created by the user
 *
 * @example
 * ```tsx
 * <PersonalLibraryPage />
 * ```
 */
export function PersonalLibraryPage({ className }: PersonalLibraryPageProps) {
  const [searchQuery, setSearchQuery] = useState('');
  const { data, isLoading } = useLibrary();

  // Split items into catalog vs custom games
  const { catalogGames, customGames } = useMemo(() => {
    const items = data?.items ?? [];
    const catalog = items.filter(entry => !entry.isPrivateGame);
    const custom = items.filter(entry => entry.isPrivateGame);
    return { catalogGames: catalog, customGames: custom };
  }, [data]);

  // Client-side search filter (applied to both lists)
  const query = searchQuery.toLowerCase().trim();
  const filteredCatalog = query
    ? catalogGames.filter(g => g.gameTitle.toLowerCase().includes(query))
    : catalogGames;
  const filteredCustom = query
    ? customGames.filter(g => g.gameTitle.toLowerCase().includes(query))
    : customGames;

  const totalCount = data?.totalCount ?? 0;

  // Loading state
  if (isLoading) {
    return (
      <div className={cn('space-y-8', className)} data-testid="personal-library-page">
        <div className="h-10 w-full max-w-sm rounded-md bg-[#161b22] animate-pulse" />
        <div className="space-y-3">
          <div className="h-5 w-40 rounded bg-[#21262d] animate-pulse" />
          <div className="flex gap-3">
            {Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="w-[140px] h-[160px] rounded-xl bg-[#21262d] animate-pulse" />
            ))}
          </div>
        </div>
      </div>
    );
  }

  // Full empty state — no games at all
  if (totalCount === 0 && !isLoading) {
    return (
      <div className={cn('py-8', className)} data-testid="personal-library-page">
        <EmptyState
          title="La tua libreria è vuota"
          description="Aggiungi giochi dalla sezione Catalogo Condiviso o crea giochi personalizzati."
          icon={BookOpen}
          variant="noData"
          data-testid="library-empty-state"
        />
      </div>
    );
  }

  return (
    <div className={cn('space-y-8', className)} data-testid="personal-library-page">
      {/* Toolbar: search + count */}
      <LibraryToolbar
        totalCount={totalCount}
        searchQuery={searchQuery}
        onSearchChange={setSearchQuery}
      />

      {/* Section 1: Shared catalog games */}
      {(filteredCatalog.length > 0 || !query) && (
        <TavoloSection icon="📚" title="Dal Catalogo">
          {filteredCatalog.length === 0 ? (
            <p className="text-sm text-[#8b949e] py-4">
              Nessun gioco del catalogo corrisponde alla ricerca.
            </p>
          ) : (
            <ShelfRow>
              {filteredCatalog.map(entry => (
                <ShelfCard
                  key={entry.id}
                  title={entry.gameTitle}
                  subtitle={entry.gamePublisher ?? ''}
                  imageUrl={entry.gameImageUrl ?? undefined}
                  coverIcon={entry.gameImageUrl ? undefined : '🎲'}
                  inLibrary
                  manaPips={[
                    { type: 'kb', active: entry.hasKb },
                    { type: 'agent', active: entry.agentIsOwned },
                  ]}
                  stateLabel={
                    entry.currentState !== 'Owned'
                      ? {
                          text: entry.currentState,
                          variant:
                            entry.currentState === 'Nuovo'
                              ? 'success'
                              : entry.currentState === 'InPrestito'
                                ? 'warning'
                                : 'info',
                        }
                      : undefined
                  }
                  data-testid={`shelf-card-catalog-${entry.id}`}
                />
              ))}
            </ShelfRow>
          )}
        </TavoloSection>
      )}

      {/* Section 2: Private/custom games */}
      {(filteredCustom.length > 0 || !query) && (
        <TavoloSection icon="🎮" title="Giochi Personalizzati">
          {filteredCustom.length === 0 && !query ? (
            <ShelfRow>
              {/* Even with no games, always show the CTA */}
              <CreateGameCtaCard />
            </ShelfRow>
          ) : filteredCustom.length === 0 ? (
            <p className="text-sm text-[#8b949e] py-4">
              Nessun gioco personalizzato corrisponde alla ricerca.
            </p>
          ) : (
            <ShelfRow>
              {filteredCustom.map(entry => (
                <ShelfCard
                  key={entry.id}
                  title={entry.gameTitle}
                  subtitle={entry.gamePublisher ?? 'Gioco personalizzato'}
                  imageUrl={entry.gameImageUrl ?? undefined}
                  coverIcon={entry.gameImageUrl ? undefined : '🎮'}
                  inLibrary
                  manaPips={[
                    { type: 'kb', active: entry.hasKb },
                    { type: 'agent', active: entry.agentIsOwned },
                  ]}
                  data-testid={`shelf-card-custom-${entry.id}`}
                />
              ))}
              {/* CTA card always at the end */}
              <CreateGameCtaCard />
            </ShelfRow>
          )}
        </TavoloSection>
      )}
    </div>
  );
}

export default PersonalLibraryPage;

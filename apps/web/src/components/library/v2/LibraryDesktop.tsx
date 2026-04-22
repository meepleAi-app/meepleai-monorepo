'use client';
import { useState, useMemo } from 'react';

import { EntityCard } from '@/components/ui/v2/entity-card/entity-card';

import { GameDrawerContent, type GameDrawerGame } from './GameDrawerContent';
import { LibraryFilterTabs, type LibraryFilter } from './LibraryFilterTabs';

export interface LibraryDesktopItem extends GameDrawerGame {
  readonly owned: boolean;
  readonly wishlist: boolean;
  readonly imageUrl?: string;
}

export interface LibraryDesktopProps {
  readonly items: ReadonlyArray<LibraryDesktopItem>;
}

export function LibraryDesktop({ items }: LibraryDesktopProps): React.JSX.Element {
  const [filter, setFilter] = useState<LibraryFilter>('all');
  const [selected, setSelected] = useState<string | null>(null);

  const filtered = useMemo(() => {
    if (filter === 'owned') return items.filter(i => i.owned);
    if (filter === 'wishlist') return items.filter(i => i.wishlist);
    return items;
  }, [items, filter]);

  const counts = useMemo(
    () => ({
      all: items.length,
      owned: items.filter(i => i.owned).length,
      wishlist: items.filter(i => i.wishlist).length,
    }),
    [items]
  );

  const selectedItem = items.find(i => i.id === selected) ?? null;

  return (
    <div className="grid h-full grid-cols-[1fr_2fr] gap-4">
      <aside className="flex flex-col gap-3 overflow-y-auto border-r border-border pr-4">
        <LibraryFilterTabs value={filter} onChange={setFilter} counts={counts} />
        <div className="flex flex-col gap-2">
          {filtered.map(g => (
            <EntityCard
              key={g.id}
              entity="game"
              onClick={() => setSelected(g.id)}
              ariaLabel={`Apri ${g.title}`}
            >
              <div className="flex flex-col gap-1">
                <span className="text-sm font-semibold text-foreground">{g.title}</span>
                {g.publisher ? (
                  <span className="text-xs text-muted-foreground">{g.publisher}</span>
                ) : null}
              </div>
            </EntityCard>
          ))}
        </div>
      </aside>
      <section className="overflow-y-auto">
        {selectedItem ? (
          <GameDrawerContent game={selectedItem} />
        ) : (
          <p className="py-12 text-center text-sm text-muted-foreground">
            Seleziona un gioco per vedere i dettagli.
          </p>
        )}
      </section>
    </div>
  );
}

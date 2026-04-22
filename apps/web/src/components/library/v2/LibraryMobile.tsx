'use client';
import { useState, useMemo } from 'react';

import { EntityCard } from '@/components/ui/v2/entity-card/entity-card';

import { LibraryFilterTabs, type LibraryFilter } from './LibraryFilterTabs';

export interface LibraryGameItem {
  readonly id: string;
  readonly title: string;
  readonly publisher?: string;
  readonly imageUrl?: string;
  readonly owned: boolean;
  readonly wishlist: boolean;
}

export interface LibraryMobileProps {
  readonly items: ReadonlyArray<LibraryGameItem>;
  readonly onSelect: (id: string) => void;
}

export function LibraryMobile({ items, onSelect }: LibraryMobileProps): React.JSX.Element {
  const [filter, setFilter] = useState<LibraryFilter>('all');

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

  return (
    <div className="flex flex-col gap-3">
      <LibraryFilterTabs value={filter} onChange={setFilter} counts={counts} />
      {filtered.length === 0 ? (
        <p className="py-8 text-center text-sm text-muted-foreground">
          Nessun gioco in questa vista.
        </p>
      ) : (
        <div className="grid grid-cols-2 gap-3">
          {filtered.map(g => (
            <EntityCard
              key={g.id}
              entity="game"
              onClick={() => onSelect(g.id)}
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
      )}
    </div>
  );
}

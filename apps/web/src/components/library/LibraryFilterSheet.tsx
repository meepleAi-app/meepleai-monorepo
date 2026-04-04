'use client';

import React from 'react';

import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { BottomSheet } from '@/components/ui/overlays/BottomSheet';
import { cn } from '@/lib/utils';

export interface LibraryFilters {
  search: string;
  state: string | null;
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

export function LibraryFilterSheet({
  open,
  onOpenChange,
  filters,
  onApply,
}: LibraryFilterSheetProps) {
  const [local, setLocal] = React.useState(filters);

  React.useEffect(() => {
    if (open) setLocal(filters);
  }, [open, filters]);

  return (
    <BottomSheet open={open} onOpenChange={onOpenChange} title="Filtra">
      <div className="flex flex-col gap-6">
        <div>
          <p className="mb-2 text-xs font-medium uppercase text-[var(--gaming-text-secondary)]">
            Stato
          </p>
          <div className="flex flex-wrap gap-2">
            {stateOptions.map(opt => (
              <button
                key={opt.id ?? 'all'}
                type="button"
                onClick={() => setLocal(f => ({ ...f, state: opt.id }))}
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
        <div>
          <p className="mb-2 text-xs font-medium uppercase text-[var(--gaming-text-secondary)]">
            Ordina per
          </p>
          <div className="flex flex-wrap gap-2">
            {sortOptions.map(opt => (
              <button
                key={opt.id}
                type="button"
                onClick={() => setLocal(f => ({ ...f, sortBy: opt.id }))}
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
        <GradientButton
          fullWidth
          onClick={() => {
            onApply(local);
            onOpenChange(false);
          }}
        >
          Applica Filtri
        </GradientButton>
      </div>
    </BottomSheet>
  );
}

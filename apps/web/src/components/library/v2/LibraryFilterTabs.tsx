export type LibraryFilter = 'all' | 'owned' | 'wishlist';

export interface LibraryFilterTabsProps {
  readonly value: LibraryFilter;
  readonly onChange: (next: LibraryFilter) => void;
  readonly counts: Record<LibraryFilter, number>;
}

const TABS: ReadonlyArray<{ id: LibraryFilter; label: string }> = [
  { id: 'all', label: 'Tutti' },
  { id: 'owned', label: 'Posseduti' },
  { id: 'wishlist', label: 'Wishlist' },
];

export function LibraryFilterTabs({
  value,
  onChange,
  counts,
}: LibraryFilterTabsProps): React.JSX.Element {
  return (
    <div role="tablist" className="flex gap-1 border-b border-border">
      {TABS.map(tab => {
        const isActive = value === tab.id;
        return (
          <button
            key={tab.id}
            role="tab"
            type="button"
            aria-selected={isActive}
            onClick={() => onChange(tab.id)}
            className={`px-3 py-2 text-sm font-medium transition border-b-2 ${
              isActive
                ? 'border-entity-game text-foreground'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            }`}
          >
            {tab.label} <span className="ml-1 text-xs opacity-70">({counts[tab.id]})</span>
          </button>
        );
      })}
    </div>
  );
}

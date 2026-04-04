import type { ComponentEntry } from '@/config/component-registry';

import { ComponentCard } from './ComponentCard';

interface LibraryGridProps {
  entries: ComponentEntry[];
}

export function LibraryGrid({ entries }: LibraryGridProps) {
  if (entries.length === 0) {
    return (
      <div className="flex min-h-[200px] flex-col items-center justify-center rounded-xl border border-dashed border-border/50 p-8 text-center">
        <p className="font-quicksand text-base font-semibold text-muted-foreground">
          No components found
        </p>
        <p className="mt-1 font-nunito text-sm text-muted-foreground/70">
          Try adjusting your filters or search query.
        </p>
      </div>
    );
  }

  return (
    <div
      className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4"
      role="list"
      aria-label="Component library"
    >
      {entries.map(entry => (
        <div key={entry.id} role="listitem">
          <ComponentCard entry={entry} />
        </div>
      ))}
    </div>
  );
}

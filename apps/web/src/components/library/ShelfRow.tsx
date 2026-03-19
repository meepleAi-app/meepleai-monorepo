/**
 * ShelfRow Component
 *
 * Horizontal scroll container for ShelfCard items.
 * Uses thin custom scrollbar styling consistent with the MeepleAI dark theme.
 */

import type { ReactNode } from 'react';

export interface ShelfRowProps {
  /** ShelfCard children to arrange horizontally */
  children: ReactNode;
}

/**
 * ShelfRow — horizontal overflow container for library shelf displays.
 *
 * @example
 * ```tsx
 * <ShelfRow>
 *   {games.map(g => <ShelfCard key={g.id} title={g.title} subtitle={g.publisher} />)}
 * </ShelfRow>
 * ```
 */
export function ShelfRow({ children }: ShelfRowProps) {
  return (
    <div
      className="flex gap-3 overflow-x-auto pb-3 scrollbar-thin scrollbar-track-transparent scrollbar-thumb-[#30363d]"
      data-testid="shelf-row"
    >
      {children}
    </div>
  );
}

export default ShelfRow;

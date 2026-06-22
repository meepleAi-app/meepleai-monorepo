'use client';

import type { JSX } from 'react';

import { Pencil } from 'lucide-react';

interface EditCoverOverlayProps {
  onEditClick: () => void;
  hasCustomCover: boolean;
}

/**
 * Edit icon overlay for hero cover (top-right).
 * Visible always on mobile, on hover on desktop (controlled by parent class).
 * Issue #1824 L3.
 */
export function EditCoverOverlay({
  onEditClick,
  hasCustomCover,
}: EditCoverOverlayProps): JSX.Element {
  const label = hasCustomCover ? 'Cambia copertina personalizzata' : 'Modifica copertina';

  return (
    <button
      type="button"
      onClick={onEditClick}
      aria-label={label}
      tabIndex={0}
      className="absolute right-2 top-2 z-10 inline-flex h-9 w-9 items-center justify-center rounded-full bg-background/80 text-foreground shadow-sm backdrop-blur-sm transition-opacity hover:bg-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring md:opacity-0 md:group-hover:opacity-100"
    >
      <Pencil className="h-4 w-4" aria-hidden="true" />
    </button>
  );
}

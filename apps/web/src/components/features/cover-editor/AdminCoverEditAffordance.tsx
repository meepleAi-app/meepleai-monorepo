/**
 * AdminCoverEditAffordance (Epic #3470 — Slice 1d-c).
 *
 * The shared, self-positioning edit trigger injected into the card / hero cover
 * slots on the PUBLIC shared-games surfaces (D3). Role-gated via useAdminRole:
 * invisible to non-admins, so the `(public)` audience contract stays intact (#2118).
 * Progressive enhancement only — the server re-authorizes every mutation.
 *
 * The button positions itself top-right of the (relative) cover slot and reveals on
 * hover/focus (mockup #1824 pattern); it opens the AdminCoverSourceDialog overlay
 * with no route/nav change.
 */

'use client';

import { useState } from 'react';

import { Pencil } from 'lucide-react';

import { useAdminRole } from '@/hooks/useAdminRole';

import { AdminCoverSourceDialog } from './AdminCoverSourceDialog';

export interface AdminCoverEditAffordanceProps {
  gameId: string;
  title?: string;
  className?: string;
}

export function AdminCoverEditAffordance({
  gameId,
  title,
  className,
}: AdminCoverEditAffordanceProps): React.JSX.Element | null {
  const { isEditorOrAbove } = useAdminRole();
  const [open, setOpen] = useState(false);

  if (!isEditorOrAbove) return null;

  return (
    <>
      <button
        type="button"
        aria-label="Modifica sorgente copertina"
        onClick={() => setOpen(true)}
        className={`absolute right-2 top-2 z-20 flex h-8 w-8 items-center justify-center rounded-md border border-border/50 bg-background/85 text-foreground shadow-sm backdrop-blur-md transition-opacity hover:bg-background focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring md:opacity-0 md:group-hover:opacity-100 ${
          className ?? ''
        }`}
      >
        <Pencil className="h-4 w-4" aria-hidden="true" />
      </button>
      <AdminCoverSourceDialog
        gameId={gameId}
        title={title}
        open={open}
        onClose={() => setOpen(false)}
      />
    </>
  );
}

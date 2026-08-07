/**
 * AdminCoverEditAffordance (Epic #3470 — Slice 1d-c).
 *
 * The shared, self-positioning edit trigger injected into the card / hero cover
 * slots on the PUBLIC shared-games surfaces (D3). Role-gated via useAdminRole:
 * invisible to non-admins, so the `(public)` audience contract stays intact (#2118).
 * Progressive enhancement only — the server re-authorizes every mutation.
 *
 * The button positions itself top-right of the (relative) cover slot and stays visible
 * at rest — no hover-only reveal (#3611: hover discretion hid the control from admins
 * too, not just non-admins). Resting tone is attenuated via semantic color tokens, never
 * `opacity`, to keep AA contrast on the button and its optional `needsAttention` outline.
 * It opens the AdminCoverSourceDialog overlay with no route/nav change.
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
  /**
   * #3611 — la cover sottostante è un placeholder: l'azione qui serve davvero.
   * Rende la matita piena da subito e disegna un contorno tratteggiato sul riquadro,
   * senza richiedere modifiche ai contenitori (GridCard / Cover / hero).
   */
  needsAttention?: boolean;
  /** #3611 — apre il dialog al mount (deep-link `?cover=edit` dalla vista cover-gap). */
  defaultOpen?: boolean;
  /** #3611 — invocata quando il dialog si chiude, per ripulire l'URL del deep-link. */
  onDialogClose?: () => void;
}

export function AdminCoverEditAffordance({
  gameId,
  title,
  className,
  needsAttention = false,
  defaultOpen,
  onDialogClose,
}: AdminCoverEditAffordanceProps): React.JSX.Element | null {
  const { isEditorOrAbove } = useAdminRole();
  const [open, setOpen] = useState(defaultOpen ?? false);

  if (!isEditorOrAbove) return null;

  // L'attenuazione a riposo NON usa `opacity`: abbasserebbe il contrasto di testo e bordo
  // sotto la soglia AA, e il gate di accessibilità è bloccante. Si attenua con i token.
  const restingTone = needsAttention
    ? 'border-border-strong bg-background text-foreground'
    : 'border-border/60 bg-background/80 text-muted-foreground hover:text-foreground hover:bg-background';

  return (
    <>
      {needsAttention && (
        <span
          data-testid="cover-needs-attention"
          aria-hidden="true"
          className="pointer-events-none absolute inset-0 z-10 rounded-md border-2 border-dashed border-border-strong"
        />
      )}
      <button
        type="button"
        aria-label="Modifica sorgente copertina"
        onClick={() => setOpen(true)}
        className={`absolute right-2 top-2 z-20 flex h-8 w-8 items-center justify-center rounded-md border shadow-sm backdrop-blur-md transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${restingTone} ${
          className ?? ''
        }`}
      >
        <Pencil className="h-4 w-4" aria-hidden="true" />
      </button>
      <AdminCoverSourceDialog
        gameId={gameId}
        title={title}
        open={open}
        onClose={() => {
          setOpen(false);
          onDialogClose?.();
        }}
      />
    </>
  );
}

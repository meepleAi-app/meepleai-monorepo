/**
 * CitationOwnershipUpsell — card visualizzata nel tab PDF del CitationModal
 * quando l'utente NON possiede il PDF citato (non l'ha caricato).
 *
 * Spec: docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md §3.3
 * Mockup: admin-mockups/design_files/sp4-citation-pdf-viewer.html (stato 3)
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface CitationOwnershipUpsellProps {
  readonly gameId?: string;
  readonly className?: string;
}

export function CitationOwnershipUpsell({
  gameId,
  className,
}: CitationOwnershipUpsellProps): ReactElement {
  const uploadHref = gameId ? `/upload?gameId=${encodeURIComponent(gameId)}` : '/upload';
  return (
    <div
      data-slot="citation-ownership-upsell"
      className={clsx(
        'flex flex-col items-center gap-3 rounded-lg border-2 border-dashed p-6 text-center',
        'border-[hsl(var(--c-warning)/0.5)] bg-[hsl(var(--c-warning)/0.04)]',
        className
      )}
    >
      <span aria-hidden="true" className="text-5xl leading-none">
        🔒
      </span>
      <h4 className="text-lg font-bold text-[hsl(var(--c-warning))]">
        PDF originale protetto da copyright
      </h4>
      <p className="max-w-md text-sm leading-relaxed text-muted-foreground">
        Per visualizzare la pagina del manuale, devi caricare la tua copia personale del PDF che
        possiedi.
      </p>
      <a
        href={uploadHref}
        className={clsx(
          'mt-2 inline-flex items-center gap-1.5 rounded-full px-5 py-3',
          'bg-[hsl(var(--c-chat))] text-white text-base font-bold',
          'hover:opacity-90 transition-opacity',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-chat))]'
        )}
      >
        📤 Carica il mio PDF
      </a>
      <p className="mt-3 max-w-md font-mono text-[10px] text-muted-foreground">
        ⚙️ Anti-duplicazione: l'app rifiuta upload di PDF già indicizzati con hash identico — non
        sprecherai banda se è lo stesso file di un altro utente.
      </p>
    </div>
  );
}

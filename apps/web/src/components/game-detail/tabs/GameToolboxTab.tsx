'use client';

import { cn } from '@/lib/utils';

import type { GameTabProps } from './types';

/**
 * Toolbox tab — placeholder describing the toolbox feature.
 * The legacy /toolbox route now redirects to this tab, so a full-screen
 * link is intentionally NOT provided to avoid redirect loops.
 */
export function GameToolboxTab({ variant, isNotInLibrary }: GameTabProps) {
  const containerClass = cn('flex flex-col', variant === 'desktop' ? 'gap-4 p-6' : 'gap-3 p-4');

  if (isNotInLibrary) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-toolbox" className={containerClass}>
        <p className="text-sm text-muted-foreground">
          Aggiungi il gioco alla libreria per usare il toolbox.
        </p>
      </div>
    );
  }

  return (
    <div role="tabpanel" aria-labelledby="game-tab-toolbox" className={containerClass}>
      <h3
        className={cn(
          'font-heading font-bold text-foreground',
          variant === 'desktop' ? 'text-lg' : 'text-base'
        )}
      >
        Toolbox
      </h3>
      <p className="text-sm text-muted-foreground">
        Strumenti rapidi per il gioco: dadi, timer, punteggi, note e altro ancora.
      </p>
      <p className="text-xs italic text-muted-foreground">
        Integrazione completa del toolbox in arrivo.
      </p>
    </div>
  );
}

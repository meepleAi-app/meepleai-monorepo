'use client';

import { cn } from '@/lib/utils';

import type { GameTabProps } from './types';

/**
 * AI Chat tab — placeholder that describes the feature and invites the user
 * to interact. A future follow-up will embed the full agent chat UI here
 * rather than linking to the standalone route.
 */
export function GameAiChatTab({ variant, isNotInLibrary }: GameTabProps) {
  const containerClass = cn('flex flex-col', variant === 'desktop' ? 'gap-4 p-6' : 'gap-3 p-4');

  if (isNotInLibrary) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-aiChat" className={containerClass}>
        <p className="text-sm text-muted-foreground">
          Aggiungi il gioco alla libreria per chattare con l&apos;AI sulle sue regole.
        </p>
      </div>
    );
  }

  return (
    <div role="tabpanel" aria-labelledby="game-tab-aiChat" className={containerClass}>
      <h3
        className={cn(
          'font-heading font-bold text-foreground',
          variant === 'desktop' ? 'text-lg' : 'text-base'
        )}
      >
        AI Chat
      </h3>
      <p className="text-sm text-muted-foreground">
        Chiedi qualsiasi cosa sulle regole di questo gioco. L&apos;AI ti risponderà in base alla
        knowledge base indicizzata.
      </p>
      <p className="text-xs italic text-muted-foreground">
        Integrazione completa della chat con il gioco in arrivo.
      </p>
    </div>
  );
}

'use client';

import { cn } from '@/lib/utils';

import type { GameTabProps } from './types';

/**
 * House Rules tab — placeholder for user-authored house rules per game.
 * Future follow-up will wire a dedicated query and CRUD UI.
 */
export function GameHouseRulesTab({ variant, isNotInLibrary }: GameTabProps) {
  const containerClass = cn('flex flex-col', variant === 'desktop' ? 'gap-4 p-6' : 'gap-3 p-4');

  if (isNotInLibrary) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-houseRules" className={containerClass}>
        <p className="text-sm text-muted-foreground">
          Aggiungi il gioco alla libreria per gestire le regole della casa.
        </p>
      </div>
    );
  }

  return (
    <div role="tabpanel" aria-labelledby="game-tab-houseRules" className={containerClass}>
      <h3
        className={cn(
          'font-heading font-bold text-foreground',
          variant === 'desktop' ? 'text-lg' : 'text-base'
        )}
      >
        Regole della casa
      </h3>
      <p className="text-sm text-muted-foreground">
        Aggiungi le varianti e le regole personalizzate che usi con il tuo gruppo di gioco.
      </p>
      <p className="text-xs italic text-muted-foreground">Gestione delle house rules in arrivo.</p>
    </div>
  );
}

/**
 * GameDetailOwnershipCta — #4155.
 *
 * Il cancello RAG (#443) apre il manuale di un gioco solo a chi dichiara di possederlo, perché i
 * regolamenti sono protetti da copyright (ADR-059). Il dettaglio di un gioco in libreria senza
 * accesso al manuale offre qui la dichiarazione, invece di aspettare il primo diniego in chat.
 */

'use client';

import { useState } from 'react';

import { OwnershipDeclarationDialog } from '@/components/library/OwnershipDeclarationDialog';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import type { OwnershipResult } from '@/lib/api/schemas/ownership.schemas';

export interface GameDetailOwnershipCtaProps {
  gameId: string;
  gameTitle: string;
  onOwnershipDeclared: (result: OwnershipResult) => void;
}

export function GameDetailOwnershipCta({
  gameId,
  gameTitle,
  onOwnershipDeclared,
}: GameDetailOwnershipCtaProps): React.JSX.Element {
  const { t } = useTranslation();
  const [dialogOpen, setDialogOpen] = useState(false);

  return (
    <div
      data-testid="game-detail-ownership-cta"
      className="mx-4 mt-3 rounded-lg border border-border bg-muted/30 p-3 text-sm sm:mx-6"
    >
      <p className="text-foreground">{t('pages.gameDetail.ownership.message', { gameTitle })}</p>
      <Button type="button" size="sm" className="mt-2" onClick={() => setDialogOpen(true)}>
        {t('pages.gameDetail.ownership.cta', { gameTitle })}
      </Button>
      <OwnershipDeclarationDialog
        gameId={gameId}
        gameName={gameTitle}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        onOwnershipDeclared={onOwnershipDeclared}
      />
    </div>
  );
}

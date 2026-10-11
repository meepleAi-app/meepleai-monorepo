/**
 * RagOwnershipRequiredNotice — #4155.
 *
 * Il backend ha respinto la domanda con `RAG_OWNERSHIP_REQUIRED`: il cancello RAG (#443) apre i
 * manuali di un gioco solo a chi dichiara di possederlo, perché i regolamenti sono protetti da
 * copyright (ADR-059). Non è un guasto, quindi non usa il banner d'errore: spiega il motivo e
 * offre la dichiarazione per il gioco del thread.
 */

'use client';

import React, { useState } from 'react';

import { OwnershipDeclarationDialog } from '@/components/library/OwnershipDeclarationDialog';
import { Button } from '@/components/ui/primitives/button';
import type { OwnershipResult } from '@/lib/api/schemas/ownership.schemas';

/** Il codice dell'evento Error con cui il backend nega la domanda (#4155). */
export const RAG_OWNERSHIP_REQUIRED = 'RAG_OWNERSHIP_REQUIRED';

export interface RagOwnershipRequiredNoticeProps {
  gameId: string;
  /** Il titolo del gioco, se la chat l'ha già caricato. */
  gameName?: string;
  onOwnershipDeclared: (result: OwnershipResult) => void;
}

export function RagOwnershipRequiredNotice({
  gameId,
  gameName,
  onOwnershipDeclared,
}: RagOwnershipRequiredNoticeProps): React.JSX.Element {
  const [dialogOpen, setDialogOpen] = useState(false);
  const label = gameName ?? 'questo gioco';

  return (
    <div
      data-testid="rag-ownership-required"
      className="mx-4 mt-2 rounded-lg border border-border bg-muted p-3 text-sm text-foreground"
    >
      <p>
        Per rispondere citando il manuale di {label} devi dichiarare di possederne una copia: i
        regolamenti sono protetti da copyright, e la chat li consulta solo per chi possiede il
        gioco.
      </p>
      <Button type="button" size="sm" className="mt-2" onClick={() => setDialogOpen(true)}>
        Dichiara di possedere {label}
      </Button>
      <OwnershipDeclarationDialog
        gameId={gameId}
        gameName={label}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        onOwnershipDeclared={onOwnershipDeclared}
      />
    </div>
  );
}

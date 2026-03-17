/**
 * DeclareOwnershipButton Component
 *
 * Renders only when game state is 'Nuovo'. Opens ownership declaration
 * dialog and chains to confirmation dialog on success.
 */

'use client';

import React, { useState } from 'react';

import { ShieldCheck } from 'lucide-react';

import { Button } from '@/components/ui/button';
import type { OwnershipResult } from '@/lib/api/schemas/ownership.schemas';

import { OwnershipConfirmationDialog } from './OwnershipConfirmationDialog';
import { OwnershipDeclarationDialog } from './OwnershipDeclarationDialog';

export interface DeclareOwnershipButtonProps {
  gameId: string;
  gameName: string;
  sharedGameId?: string;
  gameState: string;
  onOwnershipDeclared?: (result: OwnershipResult) => void;
}

export function DeclareOwnershipButton({
  gameId,
  gameName,
  sharedGameId,
  gameState,
  onOwnershipDeclared,
}: DeclareOwnershipButtonProps) {
  const [declarationOpen, setDeclarationOpen] = useState(false);
  const [confirmationOpen, setConfirmationOpen] = useState(false);
  const [ownershipResult, setOwnershipResult] = useState<OwnershipResult | null>(null);

  // Only render for games in 'Nuovo' state
  if (gameState !== 'Nuovo') {
    return null;
  }

  const handleOwnershipDeclared = (result: OwnershipResult) => {
    setOwnershipResult(result);
    setDeclarationOpen(false);
    setConfirmationOpen(true);
    onOwnershipDeclared?.(result);
  };

  return (
    <>
      <Button
        variant="outline"
        size="sm"
        onClick={() => setDeclarationOpen(true)}
        className="border-amber-200 bg-amber-50 text-amber-900 hover:bg-amber-100"
        data-testid="declare-ownership-button"
      >
        <ShieldCheck className="mr-2 h-4 w-4" />
        Dichiara Possesso
      </Button>

      <OwnershipDeclarationDialog
        gameId={gameId}
        gameName={gameName}
        open={declarationOpen}
        onOpenChange={setDeclarationOpen}
        onOwnershipDeclared={handleOwnershipDeclared}
      />

      {ownershipResult && (
        <OwnershipConfirmationDialog
          gameId={gameId}
          gameName={gameName}
          sharedGameId={sharedGameId}
          ownershipResult={ownershipResult}
          open={confirmationOpen}
          onOpenChange={setConfirmationOpen}
        />
      )}
    </>
  );
}

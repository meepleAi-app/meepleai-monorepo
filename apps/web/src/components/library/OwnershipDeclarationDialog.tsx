/**
 * OwnershipDeclarationDialog Component
 *
 * Confirmation dialog for declaring game ownership.
 * Explains benefits of ownership (tutor AI, sessions, lending)
 * and requires explicit confirmation checkbox.
 */

'use client';

import React, { useState } from 'react';

import { BookOpen, Gamepad2, HandCoins, Loader2 } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/overlays/alert-dialog-primitives';
import { api } from '@/lib/api';
import type { OwnershipResult } from '@/lib/api/schemas/ownership.schemas';

export interface OwnershipDeclarationDialogProps {
  gameId: string;
  gameName: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onOwnershipDeclared: (result: OwnershipResult) => void;
}

export function OwnershipDeclarationDialog({
  gameId,
  gameName,
  open,
  onOpenChange,
  onOwnershipDeclared,
}: OwnershipDeclarationDialogProps) {
  const [confirmed, setConfirmed] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const handleConfirm = async () => {
    if (isLoading || !confirmed) return;

    setIsLoading(true);
    try {
      const result = await api.library.declareOwnership(gameId);
      onOwnershipDeclared(result);
      onOpenChange(false);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Impossibile dichiarare il possesso. Riprova.'
      );
    } finally {
      setIsLoading(false);
    }
  };

  const handleCancel = () => {
    if (isLoading) return;
    setConfirmed(false);
    onOpenChange(false);
  };

  return (
    <AlertDialog
      open={open}
      onOpenChange={v => {
        if (!isLoading) onOpenChange(v);
      }}
    >
      <AlertDialogContent className="bg-white/70 backdrop-blur-md">
        <AlertDialogHeader>
          <AlertDialogTitle className="font-quicksand text-lg">
            Possiedi {gameName}?
          </AlertDialogTitle>
          <AlertDialogDescription asChild>
            <div className="space-y-4 text-sm text-muted-foreground">
              <p>Dichiarando il possesso avrai accesso a:</p>
              <ul className="space-y-3">
                <li className="flex items-start gap-3">
                  <BookOpen className="mt-0.5 h-5 w-5 shrink-0 text-amber-600" />
                  <span>
                    <strong className="text-foreground">Tutor AI personalizzato</strong> — un
                    assistente che conosce le regole del tuo gioco
                  </span>
                </li>
                <li className="flex items-start gap-3">
                  <Gamepad2 className="mt-0.5 h-5 w-5 shrink-0 text-amber-600" />
                  <span>
                    <strong className="text-foreground">Sessioni di gioco</strong> — traccia
                    partite, punteggi e statistiche
                  </span>
                </li>
                <li className="flex items-start gap-3">
                  <HandCoins className="mt-0.5 h-5 w-5 shrink-0 text-amber-600" />
                  <span>
                    <strong className="text-foreground">Prestiti</strong> — gestisci i prestiti dei
                    tuoi giochi agli amici
                  </span>
                </li>
              </ul>

              <label className="flex items-center gap-2 rounded-md border border-amber-200 bg-amber-50 p-3 cursor-pointer">
                <input
                  type="checkbox"
                  checked={confirmed}
                  onChange={e => setConfirmed(e.target.checked)}
                  disabled={isLoading}
                  className="h-4 w-4 rounded border-amber-300 text-amber-600 focus:ring-amber-500"
                  data-testid="ownership-checkbox"
                />
                <span className="text-sm text-amber-900">Confermo di possedere questo gioco</span>
              </label>

              <details className="text-xs">
                <summary className="cursor-pointer text-muted-foreground hover:text-foreground">
                  Perch\u00e9 lo chiediamo?
                </summary>
                <p className="mt-2 text-muted-foreground">
                  La dichiarazione di possesso ci aiuta a offrirti funzionalit\u00e0 personalizzate
                  e a rispettare le licenze dei contenuti editoriali. Dichiarando il possesso
                  confermi di avere una copia fisica o digitale del gioco.
                </p>
              </details>
            </div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel onClick={handleCancel} disabled={isLoading}>
            Non ancora
          </AlertDialogCancel>
          <AlertDialogAction
            onClick={handleConfirm}
            disabled={!confirmed || isLoading}
            className="bg-amber-600 text-white hover:bg-amber-700"
          >
            {isLoading ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Conferma in corso...
              </>
            ) : (
              'Conferma Possesso'
            )}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

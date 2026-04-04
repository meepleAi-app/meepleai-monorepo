/**
 * OwnershipConfirmationDialog Component
 *
 * Post-ownership confirmation dialog showing RAG access status.
 * Two variants:
 * - KB available: shows KB card count + quick create / customize buttons
 * - KB unavailable: informational message + close
 */

'use client';

import React, { useState } from 'react';

import { BookOpen, Loader2, Settings, Sparkles, Zap } from 'lucide-react';
import { useRouter } from 'next/navigation';

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

export interface OwnershipConfirmationDialogProps {
  gameId: string;
  gameName: string;
  sharedGameId?: string;
  ownershipResult: OwnershipResult;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function OwnershipConfirmationDialog({
  gameId,
  gameName,
  sharedGameId,
  ownershipResult,
  open,
  onOpenChange,
}: OwnershipConfirmationDialogProps) {
  const router = useRouter();
  const [isCreating, setIsCreating] = useState(false);
  const hasKb = ownershipResult.kbCardCount > 0;

  const handleQuickCreate = async () => {
    if (isCreating) return;

    setIsCreating(true);
    try {
      const result = await api.agents.quickCreateTutor(gameId, sharedGameId);
      onOpenChange(false);
      router.push(`/chat/${result.chatThreadId}`);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Impossibile creare il tutor. Riprova.');
    } finally {
      setIsCreating(false);
    }
  };

  const handleCustomize = () => {
    onOpenChange(false);
    router.push(`/chat/agents/create?gameId=${gameId}&step=2`);
  };

  return (
    <AlertDialog
      open={open}
      onOpenChange={v => {
        if (!isCreating) onOpenChange(v);
      }}
    >
      <AlertDialogContent className="bg-white/70 backdrop-blur-md">
        <AlertDialogHeader>
          <AlertDialogTitle className="font-quicksand text-lg">
            {hasKb ? (
              <>
                <Sparkles className="mr-2 inline h-5 w-5 text-amber-500" />
                Possesso confermato!
              </>
            ) : (
              'Possesso confermato'
            )}
          </AlertDialogTitle>
          <AlertDialogDescription asChild>
            <div className="space-y-4 text-sm text-muted-foreground">
              {hasKb ? (
                <>
                  <p>
                    Hai ora accesso al materiale di{' '}
                    <strong className="text-foreground">{gameName}</strong>.
                  </p>
                  <div className="flex flex-wrap gap-2" data-testid="kb-card-chips">
                    <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-3 py-1 text-xs font-medium text-amber-900">
                      <BookOpen className="h-3 w-3" />
                      {ownershipResult.kbCardCount}{' '}
                      {ownershipResult.kbCardCount === 1 ? 'scheda KB' : 'schede KB'}
                    </span>
                    {ownershipResult.isRagPublic && (
                      <span className="inline-flex items-center gap-1 rounded-full bg-blue-100 px-3 py-1 text-xs font-medium text-blue-900">
                        Accesso pubblico
                      </span>
                    )}
                  </div>
                  <p>Puoi creare subito un tutor AI oppure personalizzare la configurazione.</p>
                </>
              ) : (
                <>
                  <p>
                    Il possesso di <strong className="text-foreground">{gameName}</strong> \u00e8
                    stato registrato.
                  </p>
                  <div className="rounded-md border border-muted bg-muted/50 p-3">
                    <p className="text-muted-foreground">
                      Tutor non ancora disponibile &mdash; il materiale per questo gioco non \u00e8
                      ancora stato indicizzato. Riceverai una notifica quando sar\u00e0 pronto.
                    </p>
                  </div>
                </>
              )}
            </div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          {hasKb ? (
            <>
              <AlertDialogCancel onClick={handleCustomize} disabled={isCreating}>
                <Settings className="mr-2 h-4 w-4" />
                Personalizza
              </AlertDialogCancel>
              <AlertDialogAction
                onClick={handleQuickCreate}
                disabled={isCreating}
                className="bg-amber-600 text-white hover:bg-amber-700"
              >
                {isCreating ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Creazione...
                  </>
                ) : (
                  <>
                    <Zap className="mr-2 h-4 w-4" />
                    Crea Tutor veloce
                  </>
                )}
              </AlertDialogAction>
            </>
          ) : (
            <AlertDialogAction onClick={() => onOpenChange(false)}>Chiudi</AlertDialogAction>
          )}
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

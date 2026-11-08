/**
 * OwnershipConfirmationDialog Component
 *
 * Post-ownership confirmation dialog showing RAG access status.
 * Two variants:
 * - KB available: shows KB card count + a CTA to ask a question
 * - KB unavailable: informational message + close
 *
 * Issue #4138: the "quick create tutor" / "customize" pair is gone. Ownership
 * grants KB access; it no longer creates a per-game agent.
 */

'use client';

import React from 'react';

import { BookOpen, Sparkles, Zap } from 'lucide-react';
import { useRouter } from 'next/navigation';

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
import type { OwnershipResult } from '@/lib/api/schemas/ownership.schemas';

export interface OwnershipConfirmationDialogProps {
  gameId: string;
  gameName: string;
  ownershipResult: OwnershipResult;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function OwnershipConfirmationDialog({
  gameId,
  gameName,
  ownershipResult,
  open,
  onOpenChange,
}: OwnershipConfirmationDialogProps) {
  const router = useRouter();
  const hasKb = ownershipResult.kbCardCount > 0;

  /**
   * Issue #4138: declaring ownership no longer creates anything.
   *
   * It used to call `quickCreateTutor` and then navigate to `/agents/{id}`,
   * because each user got their own per-game agent. With one system-wide agent
   * there is nothing to create: declaring ownership is what grants access to
   * the game's knowledge base (`CanAccessRagAsync` rule 3 keys off
   * `OwnershipDeclaredAt`), so the useful next move is to ask a question.
   *
   * Destination is the canonical per-game chat — `/library/{gameId}?tab=aiChat`.
   * It is not invented here: `/library/{gameId}/agent` already 307-redirects
   * there, and `aiChat` is a member of `GameTabId`. Checking that mattered,
   * because #4118 is a family of legacy redirects pointing at tab ids that do
   * not exist, which silently open Info instead.
   */
  const handleAskQuestion = () => {
    onOpenChange(false);
    router.push(`/library/${gameId}?tab=aiChat`);
  };

  // The dialog no longer performs an in-flight mutation, so there is no reason
  // to block dismissal: the `isCreating` guard went with `quickCreateTutor`.
  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent className="bg-card/70 backdrop-blur-md">
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
                  <p>Puoi gi&agrave; fare domande sulle regole di questo gioco.</p>
                </>
              ) : (
                <>
                  <p>
                    {/* The literal `\u00e8` that stood here rendered as those six
                        characters on screen, not as "\u00e8". Pre-existing, unrelated
                        to #4138, fixed in passing because it is the same sentence. */}
                    Il possesso di <strong className="text-foreground">{gameName}</strong> &egrave;
                    stato registrato.
                  </p>
                  <div className="rounded-md border border-muted bg-muted/50 p-3">
                    <p className="text-muted-foreground">
                      Non puoi ancora fare domande &mdash; il materiale per questo gioco non
                      &egrave; ancora stato indicizzato. Riceverai una notifica quando sar&agrave;
                      pronto.
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
              <AlertDialogCancel>Pi&ugrave; tardi</AlertDialogCancel>
              <AlertDialogAction
                onClick={handleAskQuestion}
                className="bg-amber-600 text-white hover:bg-amber-700"
                data-testid="ownership-ask-question"
              >
                <Zap className="mr-2 h-4 w-4" />
                Fai una domanda
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

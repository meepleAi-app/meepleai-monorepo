'use client';

import { useQuery } from '@tanstack/react-query';

import { GameChatTabV2 } from '@/components/v2/game-chat';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

import type { GameTabProps } from './types';

/**
 * AI Chat tab — V2 (G1+G5).
 *
 * Sostituisce il vecchio summary panel V1 con un chat-in-game inline diretto:
 * l'utente Alpha durante una serata di gioco apre il tab e può chattare
 * subito, senza aprire drawer separati.
 *
 * Stati:
 *   - isNotInLibrary → empty state (aggiungi alla libreria)
 *   - no KB → empty state (carica PDF)
 *   - KB ready/processing → <GameChatTabV2/> inline
 *
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md
 * Issue: #915
 */
export function GameAiChatTab({ gameId, variant, isNotInLibrary }: GameTabProps) {
  const containerClass = cn('flex flex-col', variant === 'desktop' ? 'gap-4 p-6' : 'gap-3 p-4');

  // Game title (per header del chat)
  const { data: gameDetail } = useQuery({
    queryKey: ['shared-game-detail', gameId],
    queryFn: () => api.sharedGames.getById(gameId),
    enabled: !!gameId && !isNotInLibrary,
    staleTime: 5 * 60_000,
  });

  // KB status (per decidere se montare la chat)
  const { data: kbDocs } = useQuery({
    queryKey: ['game-kb-docs', gameId],
    queryFn: () => api.knowledgeBase.getGameDocuments(gameId),
    enabled: !!gameId && !isNotInLibrary,
    staleTime: 2 * 60_000,
  });

  // ---- Stato 1: gioco non in libreria ----
  if (isNotInLibrary) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-aiChat" className={containerClass}>
        <p className="text-sm text-muted-foreground">
          Aggiungi il gioco alla libreria per chattare con l&apos;AI sulle sue regole.
        </p>
      </div>
    );
  }

  const indexedCount = kbDocs?.filter(d => d.status === 'indexed').length ?? 0;
  const processingCount = kbDocs?.filter(d => d.status === 'processing').length ?? 0;

  // ---- Stato 2: no KB ----
  if (indexedCount === 0 && processingCount === 0) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-aiChat" className={containerClass}>
        <p className="text-sm text-muted-foreground">
          Carica un PDF delle regole per abilitare la chat AI.
        </p>
      </div>
    );
  }

  // ---- Stato 3: KB ready or processing → chat V2 inline ----
  return (
    <div role="tabpanel" aria-labelledby="game-tab-aiChat" className="flex h-full flex-col">
      <GameChatTabV2
        gameId={gameId}
        gameTitle={gameDetail?.title ?? 'Gioco'}
        gameIcon="🎲"
      />
    </div>
  );
}

'use client';

/**
 * AdminSharedGameCardContainer
 *
 * React Query container that fetches all data for a SharedGame admin
 * ExtraCard panel and renders SharedGameExtraMeepleCard.
 * Handles PDF upload dialog and AgentBuilderModal.
 */

import { useState } from 'react';

import { useQuery, useQueryClient } from '@tanstack/react-query';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { api } from '@/lib/api';
import type { SharedGameDetailData } from '@/components/ui/data-display/extra-meeple-card/types';

import { AgentBuilderModal } from './AgentBuilderModal';
import { PdfUploadSection } from './PdfUploadSection';
import { SharedGameExtraMeepleCard } from './SharedGameExtraMeepleCard';

// ============================================================================
// Props
// ============================================================================

interface AdminSharedGameCardContainerProps {
  gameId: string;
  onClose?: () => void;
}

// ============================================================================
// Component
// ============================================================================

export function AdminSharedGameCardContainer({ gameId, onClose: _onClose }: AdminSharedGameCardContainerProps) {
  const queryClient = useQueryClient();
  const [uploadOpen, setUploadOpen] = useState(false);
  const [agentBuilderOpen, setAgentBuilderOpen] = useState(false);

  // ── Game detail ───────────────────────────────────────────────────────────
  const { data: game, isLoading: isLoadingGame } = useQuery({
    queryKey: ['admin-shared-game-card', gameId],
    queryFn: () => api.sharedGames.getById(gameId),
    enabled: !!gameId,
  });

  // ── Documents (polling while any doc is not yet indexed/failed) ───────────
  const { data: docs } = useQuery({
    queryKey: ['admin-shared-game-card-docs', gameId],
    queryFn: () => api.sharedGames.getDocuments(gameId),
    enabled: !!gameId,
    refetchInterval: 5000,
  });

  // ── KB Cards (polling while any card is pending/processing) ───────────────
  const { data: kbCards } = useQuery({
    queryKey: ['admin-shared-game-card-kb', gameId],
    queryFn: () => api.sharedGames.getKbCards(gameId),
    enabled: !!gameId,
    refetchInterval: 5000,
  });

  // ── Linked Agent ──────────────────────────────────────────────────────────
  const { data: agent } = useQuery({
    queryKey: ['admin-shared-game-card-agent', gameId],
    queryFn: () => api.sharedGames.getLinkedAgent(gameId),
    enabled: !!gameId,
  });

  // ── Assemble view model ───────────────────────────────────────────────────
  const detailData: SharedGameDetailData | null = game
    ? {
        id: game.id,
        title: game.title,
        imageUrl: game.imageUrl || undefined,
        publisher: game.publishers?.[0]?.name,
        yearPublished: game.yearPublished,
        minPlayers: game.minPlayers,
        maxPlayers: game.maxPlayers,
        playTimeMinutes: game.playingTimeMinutes,
        description: game.description,
        averageRating: game.averageRating ?? undefined,
        totalPlays: undefined,
        faqCount: game.faqs?.length,
        rulesDocumentCount: docs?.length,
        status: game.status,
        documents: (docs ?? []).map((doc) => ({
          id: doc.id,
          pdfDocumentId: doc.pdfDocumentId,
          documentType: doc.documentType,
          version: doc.version,
          isActive: doc.isActive,
          tags: doc.tags,
          createdAt: doc.createdAt,
        })),
        kbCards: (kbCards ?? []).map((card) => ({
          id: card.id,
          pdfDocumentId: card.pdfDocumentId,
          fileName: card.fileName,
          indexingStatus: card.indexingStatus,
          chunkCount: card.chunkCount,
          indexedAt: card.indexedAt,
          documentType: card.documentType,
          version: card.version,
          isActive: card.isActive,
        })),
        linkedAgent: agent
          ? { id: agent.id, name: agent.name, isActive: agent.isActive }
          : null,
      }
    : null;

  return (
    <>
      <SharedGameExtraMeepleCard
        data={detailData ?? ({} as SharedGameDetailData)}
        loading={isLoadingGame || !detailData}
        onUploadPdf={() => setUploadOpen(true)}
        onCreateAgent={() => setAgentBuilderOpen(true)}
      />

      {/* PDF Upload Dialog */}
      <Dialog open={uploadOpen} onOpenChange={setUploadOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Carica PDF</DialogTitle>
            <DialogDescription>
              Carica un documento PDF per {game?.title ?? 'il gioco'}.
            </DialogDescription>
          </DialogHeader>
          <PdfUploadSection
            gameId={gameId}
            onPdfUploaded={() => {
              void queryClient.invalidateQueries({ queryKey: ['admin-shared-game-card-docs', gameId] });
              void queryClient.invalidateQueries({ queryKey: ['admin-shared-game-card-kb', gameId] });
              setUploadOpen(false);
            }}
          />
        </DialogContent>
      </Dialog>

      {/* Agent Builder Modal */}
      {game && (
        <AgentBuilderModal
          open={agentBuilderOpen}
          onClose={() => setAgentBuilderOpen(false)}
          sharedGameContext={{
            gameId: game.id,
            gameTitle: game.title,
            gameDescription: game.description,
          }}
          onSuccess={() => {
            void queryClient.invalidateQueries({ queryKey: ['admin-shared-game-card-agent', gameId] });
          }}
        />
      )}
    </>
  );
}

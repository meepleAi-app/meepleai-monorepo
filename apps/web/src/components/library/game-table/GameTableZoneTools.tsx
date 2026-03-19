/**
 * GameTableZoneTools — Tools & management zone for the Game Table
 *
 * Renders toolkit link, notes preview, related entities, ownership controls,
 * and remove-from-library action. Each item is a dark card row.
 *
 * Issue #3513 — Game Table Detail
 */

'use client';

import React, { useState } from 'react';

import { Wrench, Pencil, Trash2 } from 'lucide-react';
import Link from 'next/link';

import { DeclareOwnershipButton } from '@/components/library/DeclareOwnershipButton';
import { EditNotesModal } from '@/components/library/EditNotesModal';
import { RagAccessBadge } from '@/components/library/RagAccessBadge';
import { RemoveGameDialog } from '@/components/library/RemoveGameDialog';
import { RelatedEntitiesSection } from '@/components/ui/data-display/entity-link/related-entities-section';
import { Button } from '@/components/ui/primitives/button';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

// ============================================================================
// Types
// ============================================================================

export interface GameTableZoneToolsProps {
  gameDetail: LibraryGameDetail;
  gameId: string;
}

// ============================================================================
// Styling constants
// ============================================================================

const CARD_ROW = 'bg-[#21262d] rounded-lg p-3 border border-[#30363d]';

// ============================================================================
// Component
// ============================================================================

export function GameTableZoneTools({
  gameDetail,
  gameId,
}: GameTableZoneToolsProps): React.ReactNode {
  const [isNotesOpen, setIsNotesOpen] = useState(false);
  const [isRemoveOpen, setIsRemoveOpen] = useState(false);

  const truncatedNotes =
    gameDetail.notes && gameDetail.notes.length > 120
      ? `${gameDetail.notes.slice(0, 120)}...`
      : gameDetail.notes;

  return (
    <div className="space-y-3">
      {/* Toolkit link */}
      <Link
        href={`/library/games/${gameId}/toolkit`}
        className={`${CARD_ROW} flex items-center gap-3 text-[#e6edf3] hover:border-amber-500/50 transition-colors`}
        data-testid="toolkit-link"
      >
        <Wrench className="h-5 w-5 text-amber-400 shrink-0" />
        <span className="font-quicksand font-semibold">Toolkit</span>
      </Link>

      {/* Notes preview */}
      <div className={`${CARD_ROW}`} data-testid="notes-section">
        <div className="flex items-center justify-between mb-1">
          <span className="text-sm font-quicksand font-semibold text-[#e6edf3]">Note</span>
          <button
            onClick={() => setIsNotesOpen(true)}
            className="p-1 rounded hover:bg-[#30363d] text-[#8b949e] hover:text-[#e6edf3] transition-colors"
            aria-label="Modifica note"
            data-testid="edit-notes-btn"
          >
            <Pencil className="h-4 w-4" />
          </button>
        </div>
        <p className="text-sm text-[#8b949e] font-nunito" data-testid="notes-preview">
          {truncatedNotes || 'Nessuna nota'}
        </p>
      </div>

      {/* Related entities */}
      <div className={CARD_ROW} data-testid="related-entities-section">
        <RelatedEntitiesSection entityType="Game" entityId={gameId} />
      </div>

      {/* Ownership + RAG badge */}
      <div
        className={`${CARD_ROW} flex items-center gap-3 flex-wrap`}
        data-testid="ownership-section"
      >
        <DeclareOwnershipButton
          gameId={gameId}
          gameName={gameDetail.gameTitle}
          gameState={gameDetail.currentState}
        />
        <RagAccessBadge hasRagAccess={gameDetail.hasRagAccess} isRagPublic={false} />
      </div>

      {/* Remove from library */}
      <Button
        variant="destructive"
        className="w-full bg-red-900/30 border border-red-800/50 hover:bg-red-900/50 text-red-400"
        onClick={() => setIsRemoveOpen(true)}
        data-testid="remove-game-btn"
      >
        <Trash2 className="h-4 w-4 mr-2" />
        Rimuovi dalla libreria
      </Button>

      {/* Modals */}
      <EditNotesModal
        isOpen={isNotesOpen}
        onClose={() => setIsNotesOpen(false)}
        gameId={gameId}
        gameTitle={gameDetail.gameTitle}
        currentNotes={gameDetail.notes}
      />
      <RemoveGameDialog
        isOpen={isRemoveOpen}
        onClose={() => setIsRemoveOpen(false)}
        gameId={gameId}
        gameTitle={gameDetail.gameTitle}
      />
    </div>
  );
}

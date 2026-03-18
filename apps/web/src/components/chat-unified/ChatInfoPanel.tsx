/**
 * ChatInfoPanel - Right sidebar panel for ChatThreadView (desktop only)
 *
 * Shows:
 * - Game info (linked game title)
 * - Citations from assistant messages
 * - Suggested follow-up questions
 */

'use client';

import React from 'react';

import type { Citation } from '@/types';

import { CitationBadge } from './CitationBadge';

// ============================================================================
// Types
// ============================================================================

export interface ChatInfoPanelProps {
  game: { id: string; title: string } | null;
  citations: Citation[];
  suggestedQuestions: string[];
  onQuestionClick: (question: string) => void;
}

// ============================================================================
// Component
// ============================================================================

export function ChatInfoPanel({
  game,
  citations,
  suggestedQuestions,
  onQuestionClick,
}: ChatInfoPanelProps) {
  return (
    <div className="hidden lg:flex w-[340px] flex-shrink-0 flex-col border-l border-border/50 bg-card/50 p-4 overflow-y-auto">
      {game && (
        <div className="mb-4">
          <h4 className="text-sm font-semibold font-quicksand mb-1">{game.title}</h4>
          <p className="text-xs text-muted-foreground">Gioco collegato</p>
        </div>
      )}
      {citations.length > 0 && (
        <div className="mb-4">
          <h4 className="text-sm font-semibold font-quicksand mb-2">
            Citazioni ({citations.length})
          </h4>
          <div className="flex flex-wrap gap-1">
            {citations.slice(0, 20).map((c, i) => (
              <CitationBadge key={`panel-${c.documentId}-${c.pageNumber}-${i}`} citation={c} />
            ))}
          </div>
        </div>
      )}
      {suggestedQuestions.length > 0 && (
        <div>
          <h4 className="text-sm font-semibold font-quicksand mb-2">Domande suggerite</h4>
          <div className="space-y-1">
            {suggestedQuestions.map((q, i) => (
              <button
                key={i}
                onClick={() => onQuestionClick(q)}
                className="block w-full text-left text-xs px-2 py-1.5 rounded hover:bg-muted/50 transition-colors text-muted-foreground hover:text-foreground"
              >
                {q}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

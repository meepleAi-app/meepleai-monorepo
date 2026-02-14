/**
 * ChatInfoPanel - Stub (will be replaced by PR #4374)
 *
 * Minimal placeholder so ChatThreadView can compile on this branch.
 * The full implementation lives on feature/issue-4365-chat-info-panel.
 */

'use client';

import React from 'react';

export interface ChatInfoPanelProps {
  game?: { id: string; title: string } | null;
  agent?: { id: string; name: string; type?: string } | null;
  citations?: any[];
  suggestedQuestions?: string[];
  onCitationClick?: (citation: any) => void;
  onQuestionClick?: (question: string) => void;
  isStreaming?: boolean;
}

export function ChatInfoPanel({
  citations = [],
  suggestedQuestions = [],
}: ChatInfoPanelProps) {
  return (
    <aside
      className="hidden lg:flex w-80 flex-col border-l border-border/50 bg-background/95"
      data-testid="chat-info-panel"
    >
      <div className="p-4 text-sm text-muted-foreground">
        <span data-testid="citation-count">{citations.length}</span>
        <span data-testid="question-count">{suggestedQuestions.length}</span>
      </div>
    </aside>
  );
}

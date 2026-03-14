'use client';

import React, { useEffect } from 'react';

import { Loader2, AlertCircle } from 'lucide-react';

import { useToolboxStore } from '@/lib/stores/toolboxStore';

import { SharedContextBar } from './SharedContextBar';
import { ToolCard } from './ToolCard';
import { ToolCardDeck } from './ToolCardDeck';

interface FreeformToolboxProps {
  toolboxId: string;
  className?: string;
  'data-testid'?: string;
}

/**
 * Main freeform mode layout container for the Game Toolbox.
 * Renders SharedContextBar (sticky) + a collapsible list of ToolCards.
 * Epic #412 — Game Toolbox.
 */
export function FreeformToolbox({
  toolboxId,
  className = '',
  'data-testid': testId,
}: FreeformToolboxProps) {
  const toolbox = useToolboxStore(s => s.toolbox);
  const isLoading = useToolboxStore(s => s.isLoading);
  const error = useToolboxStore(s => s.error);
  const expandedTools = useToolboxStore(s => s.expandedTools);
  const loadToolbox = useToolboxStore(s => s.loadToolbox);
  const toggleTool = useToolboxStore(s => s.toggleTool);

  useEffect(() => {
    if (!toolbox || toolbox.id !== toolboxId) {
      loadToolbox(toolboxId);
    }
  }, [toolboxId, toolbox, loadToolbox]);

  if (isLoading) {
    return (
      <div
        className="flex min-h-48 items-center justify-center"
        data-testid={testId ?? 'freeform-toolbox-loading'}
      >
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (error) {
    return (
      <div
        className="flex min-h-48 flex-col items-center justify-center gap-2 text-destructive"
        data-testid={testId ?? 'freeform-toolbox-error'}
      >
        <AlertCircle className="h-8 w-8" />
        <p className="text-sm">{error}</p>
      </div>
    );
  }

  if (!toolbox) {
    return (
      <div
        className="flex min-h-48 items-center justify-center text-muted-foreground"
        data-testid={testId ?? 'freeform-toolbox-empty'}
      >
        <p className="text-sm">No toolbox loaded</p>
      </div>
    );
  }

  const sortedTools = [...toolbox.tools].sort((a, b) => a.displayOrder - b.displayOrder);

  return (
    <div className={`flex flex-col ${className}`} data-testid={testId ?? 'freeform-toolbox'}>
      <SharedContextBar sharedContext={toolbox.sharedContext} />

      <div className="space-y-2 p-4">
        {sortedTools.map(tool => {
          const isExpanded = expandedTools.has(tool.id);

          return (
            <ToolCard
              key={tool.id}
              tool={tool}
              isExpanded={isExpanded}
              onToggle={() => toggleTool(tool.id)}
              isLocked={tool.isLocked}
            >
              {tool.type === 'CardDeck' && <ToolCardDeck toolboxId={toolbox.id} deckId={tool.id} />}
            </ToolCard>
          );
        })}
      </div>
    </div>
  );
}

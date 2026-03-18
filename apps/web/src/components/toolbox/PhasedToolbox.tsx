'use client';

import React, { useEffect, useMemo } from 'react';

import { Loader2, AlertCircle } from 'lucide-react';

import { useToolboxStore } from '@/lib/stores/toolboxStore';

import { PhaseTimeline } from './PhaseTimeline';
import { SharedContextBar } from './SharedContextBar';
import { ToolCard } from './ToolCard';
import { ToolCardDeck } from './ToolCardDeck';

interface PhasedToolboxProps {
  toolboxId: string;
  className?: string;
  'data-testid'?: string;
}

/**
 * Phased mode layout for the Game Toolbox.
 * Wraps PhaseTimeline + SharedContextBar + phase-aware ToolCards.
 * A tool is locked if its ID is NOT in currentPhase.activeToolIds.
 * Epic #412 — Game Toolbox.
 */
export function PhasedToolbox({
  toolboxId,
  className = '',
  'data-testid': testId,
}: PhasedToolboxProps) {
  const toolbox = useToolboxStore(s => s.toolbox);
  const currentPhase = useToolboxStore(s => s.currentPhase);
  const isLoading = useToolboxStore(s => s.isLoading);
  const error = useToolboxStore(s => s.error);
  const expandedTools = useToolboxStore(s => s.expandedTools);
  const loadToolbox = useToolboxStore(s => s.loadToolbox);
  const toggleTool = useToolboxStore(s => s.toggleTool);
  const advancePhase = useToolboxStore(s => s.advancePhase);

  useEffect(() => {
    if (!toolbox || toolbox.id !== toolboxId) {
      loadToolbox(toolboxId);
    }
  }, [toolboxId, toolbox, loadToolbox]);

  // Compute the set of active tool IDs for the current phase
  const activeToolIds = useMemo(() => {
    if (!currentPhase) return new Set<string>();
    return new Set<string>(currentPhase.activeToolIds);
  }, [currentPhase]);

  if (isLoading) {
    return (
      <div
        className="flex min-h-48 items-center justify-center"
        data-testid={testId ?? 'phased-toolbox-loading'}
      >
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (error) {
    return (
      <div
        className="flex min-h-48 flex-col items-center justify-center gap-2 text-destructive"
        data-testid={testId ?? 'phased-toolbox-error'}
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
        data-testid={testId ?? 'phased-toolbox-empty'}
      >
        <p className="text-sm">No toolbox loaded</p>
      </div>
    );
  }

  const sortedTools = [...toolbox.tools].sort((a, b) => a.order - b.order);

  return (
    <div className={`flex flex-col ${className}`} data-testid={testId ?? 'phased-toolbox'}>
      {/* Phase stepper */}
      <PhaseTimeline
        phases={toolbox.phases}
        currentPhaseId={toolbox.currentPhaseId}
        onAdvance={advancePhase}
      />

      {/* Shared context */}
      <SharedContextBar sharedContext={toolbox.sharedContext} />

      {/* Tool cards with phase-aware locking */}
      <div className="space-y-2 p-4">
        {sortedTools.map(tool => {
          const isExpanded = expandedTools.has(tool.id);
          // A tool is locked if it's not in the current phase's active tool list,
          // OR if there is no current phase (all locked as fallback).
          const isLocked = !activeToolIds.has(tool.id);

          return (
            <ToolCard
              key={tool.id}
              tool={tool}
              isExpanded={isExpanded}
              onToggle={() => toggleTool(tool.id)}
              isLocked={isLocked}
            >
              {tool.type === 'CardDeck' && <ToolCardDeck toolboxId={toolbox.id} deckId={tool.id} />}
            </ToolCard>
          );
        })}
      </div>
    </div>
  );
}

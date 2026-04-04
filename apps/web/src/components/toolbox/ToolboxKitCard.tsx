'use client';

import React from 'react';

import { Users, Wrench } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { ToolboxDto } from '@/lib/api/schemas/toolbox.schemas';

import { ToolPreviewChips } from './ToolPreviewChips';

interface ToolboxKitCardProps {
  toolbox: ToolboxDto;
  onClick?: () => void;
  className?: string;
  'data-testid'?: string;
}

/**
 * Toolbox displayed as a MeepleCard (entity="toolkit").
 * Shows name, mode badge, tool count, player count, and tool preview chips.
 * Epic #412 — Game Toolbox.
 */
export function ToolboxKitCard({
  toolbox,
  onClick,
  className = '',
  'data-testid': testId,
}: ToolboxKitCardProps) {
  const toolNames = toolbox.tools
    .slice(0, 3)
    .map((t: { type: string }) => t.type)
    .join(', ');
  const toolSummary =
    toolbox.tools.length > 3 ? `${toolNames} +${toolbox.tools.length - 3}` : toolNames;

  return (
    <div className={className} data-testid={testId ?? `toolbox-kit-card-${toolbox.id}`}>
      <MeepleCard
        entity="toolkit"
        variant="grid"
        title={toolbox.name}
        subtitle={toolSummary || `${toolbox.tools.length} tools`}
        badge={toolbox.mode}
        metadata={[
          { icon: Users, value: `${toolbox.sharedContext.players.length} players` },
          { icon: Wrench, value: `${toolbox.tools.length} tools` },
        ]}
        onClick={onClick}
      />
      {toolbox.tools.length > 0 && (
        <div className="mt-1 px-1">
          <ToolPreviewChips tools={toolbox.tools} maxVisible={3} />
        </div>
      )}
    </div>
  );
}

import React from 'react';

import type { ToolboxToolDto } from '@/lib/api/schemas/toolbox.schemas';

interface ToolPreviewChipsProps {
  tools: ToolboxToolDto[];
  maxVisible?: number;
  className?: string;
  'data-testid'?: string;
}

const TOOL_TYPE_ICONS: Record<string, string> = {
  DiceRoller: '\uD83C\uDFB2',
  ScoreTracker: '\uD83D\uDCCA',
  TurnManager: '\uD83D\uDD04',
  ResourceManager: '\uD83D\uDCE6',
  Notes: '\uD83D\uDCDD',
  Whiteboard: '\uD83C\uDFA8',
  CardDeck: '\uD83C\uDCCF',
};

/**
 * Small preview chips showing tool icons in a row.
 * Used on Toolbox Kit Card front to preview available tools.
 * Epic #412 — Game Toolbox.
 */
export function ToolPreviewChips({
  tools,
  maxVisible = 3,
  className = '',
  'data-testid': testId,
}: ToolPreviewChipsProps) {
  const visible = tools.slice(0, maxVisible);
  const overflow = tools.length - maxVisible;

  return (
    <div
      className={`flex items-center gap-1.5 ${className}`}
      data-testid={testId ?? 'tool-preview-chips'}
    >
      {visible.map(tool => {
        const icon = TOOL_TYPE_ICONS[tool.type] ?? '\uD83D\uDD27';
        // Abbreviate name: take first 8 chars
        const shortName = tool.type.length > 8 ? `${tool.type.slice(0, 8)}...` : tool.type;
        return (
          <span
            key={tool.id}
            className="inline-flex items-center gap-1 rounded-full border bg-muted/50 px-2 py-0.5 text-xs"
            title={tool.type}
          >
            <span aria-hidden="true">{icon}</span>
            <span>{shortName}</span>
          </span>
        );
      })}
      {overflow > 0 && (
        <span className="inline-flex items-center rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
          +{overflow}
        </span>
      )}
    </div>
  );
}

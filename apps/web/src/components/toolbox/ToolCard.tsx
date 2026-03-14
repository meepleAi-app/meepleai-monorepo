'use client';

import React from 'react';

import { ChevronDown, Lock } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import type { ToolboxToolDto } from '@/lib/api/schemas/toolbox.schemas';
import { cn } from '@/lib/utils';

interface ToolCardProps {
  tool: ToolboxToolDto;
  isExpanded: boolean;
  onToggle: () => void;
  isLocked?: boolean;
  children?: React.ReactNode;
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
 * Individual tool card component with expand/collapse and locked state.
 * Epic #412 — Game Toolbox.
 */
export function ToolCard({
  tool,
  isExpanded,
  onToggle,
  isLocked = false,
  children,
  className = '',
  'data-testid': testId,
}: ToolCardProps) {
  const icon = TOOL_TYPE_ICONS[tool.type] ?? '\uD83D\uDD27';

  return (
    <Card
      className={cn('transition-all', isLocked && 'pointer-events-none opacity-50', className)}
      data-testid={testId ?? `tool-card-${tool.id}`}
    >
      <CardHeader
        className="flex cursor-pointer flex-row items-center justify-between space-y-0 py-3"
        onClick={isLocked ? undefined : onToggle}
        role="button"
        tabIndex={isLocked ? -1 : 0}
        aria-expanded={isExpanded}
        aria-label={`${isLocked ? 'Locked: ' : ''}${tool.type}`}
        onKeyDown={e => {
          if (!isLocked && (e.key === 'Enter' || e.key === ' ')) {
            e.preventDefault();
            onToggle();
          }
        }}
      >
        <CardTitle className="flex items-center gap-2 text-sm font-medium">
          <span aria-hidden="true">{icon}</span>
          <span>{tool.type}</span>
        </CardTitle>
        <div className="flex items-center gap-1">
          {isLocked ? (
            <Lock className="h-4 w-4 text-muted-foreground" />
          ) : (
            <ChevronDown
              className={cn(
                'h-4 w-4 text-muted-foreground transition-transform',
                isExpanded && 'rotate-180'
              )}
            />
          )}
        </div>
      </CardHeader>
      {isExpanded && !isLocked && <CardContent className="pt-0">{children}</CardContent>}
    </Card>
  );
}

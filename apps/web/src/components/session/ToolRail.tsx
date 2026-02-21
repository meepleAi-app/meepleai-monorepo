'use client';

/**
 * ToolRail — vertical icon navigation rail for the Session Tool Layout
 * Epic #4968: Game Session Toolkit v2
 */

import React from 'react';

import { Dices, PenLine, RotateCcw, Trophy } from 'lucide-react';

import { cn } from '@/lib/utils';

// ── Public types ──────────────────────────────────────────────────────────────

export interface ToolItem {
  id: string;
  label: string;
  shortLabel: string;
  icon: React.ReactNode;
  type: 'built-in' | 'custom';
}

// ── Built-in tools ────────────────────────────────────────────────────────────

const BASE_TOOLS: ToolItem[] = [
  {
    id: 'scoreboard',
    label: 'Scoreboard',
    shortLabel: 'Score',
    icon: <Trophy className="w-5 h-5" aria-hidden="true" />,
    type: 'built-in',
  },
  {
    id: 'turn-order',
    label: 'Turn Order',
    shortLabel: 'Turn',
    icon: <RotateCcw className="w-5 h-5" aria-hidden="true" />,
    type: 'built-in',
  },
  {
    id: 'dice',
    label: 'Dice Roller',
    shortLabel: 'Dice',
    icon: <Dices className="w-5 h-5" aria-hidden="true" />,
    type: 'built-in',
  },
  {
    id: 'whiteboard',
    label: 'Whiteboard',
    shortLabel: 'Board',
    icon: <PenLine className="w-5 h-5" aria-hidden="true" />,
    type: 'built-in',
  },
];

// ── Component ─────────────────────────────────────────────────────────────────

interface ToolRailProps {
  activeTool: string;
  onToolSelect: (id: string) => void;
  customTools?: ToolItem[];
}

export function ToolRail({
  activeTool,
  onToolSelect,
  customTools = [],
}: ToolRailProps) {
  const renderButton = (tool: ToolItem) => (
    <button
      key={tool.id}
      onClick={() => onToolSelect(tool.id)}
      title={tool.label}
      aria-label={tool.label}
      aria-pressed={activeTool === tool.id}
      className={cn(
        'flex flex-col items-center gap-1 w-full px-1 py-2.5 rounded-lg text-[10px] font-semibold transition-colors',
        activeTool === tool.id
          ? 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300'
          : 'text-slate-500 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 hover:text-slate-700 dark:hover:text-slate-200',
      )}
    >
      {tool.icon}
      <span>{tool.shortLabel}</span>
    </button>
  );

  return (
    <nav
      aria-label="Session tools"
      className="flex flex-col gap-1 p-2 w-16 shrink-0 border-r border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900"
    >
      {BASE_TOOLS.map(renderButton)}

      {customTools.length > 0 && (
        <>
          <div
            className="my-1 border-t border-slate-200 dark:border-slate-700"
            aria-hidden="true"
          />
          {customTools.map(renderButton)}
        </>
      )}
    </nav>
  );
}

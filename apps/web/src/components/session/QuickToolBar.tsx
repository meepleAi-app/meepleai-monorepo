'use client';

import React from 'react';

import { Dice5, Circle, Timer, Hash, Layers } from 'lucide-react';

import { cn } from '@/lib/utils';

export type ToolId = 'dadi' | 'moneta' | 'timer' | 'contatore' | 'carte';

interface Tool {
  id: ToolId;
  label: string;
  icon: React.ElementType;
}

const TOOLS: Tool[] = [
  { id: 'dadi', label: 'Dadi', icon: Dice5 },
  { id: 'moneta', label: 'Moneta', icon: Circle },
  { id: 'timer', label: 'Timer', icon: Timer },
  { id: 'contatore', label: 'Contatore', icon: Hash },
  { id: 'carte', label: 'Carte', icon: Layers },
];

export interface QuickToolBarProps {
  activeTool: ToolId | null;
  onSelectTool: (tool: ToolId) => void;
  className?: string;
}

export function QuickToolBar({ activeTool, onSelectTool, className }: QuickToolBarProps) {
  return (
    <div
      role="toolbar"
      aria-label="Strumenti rapidi"
      className={cn('flex gap-2 overflow-x-auto py-2 scrollbar-hide', className)}
    >
      {TOOLS.map(({ id, label, icon: Icon }) => {
        const isActive = activeTool === id;
        return (
          <button
            key={id}
            aria-label={label}
            aria-pressed={isActive}
            onClick={() => onSelectTool(id)}
            className={cn(
              'flex min-w-[64px] flex-col items-center gap-1 rounded-xl px-3 py-2 text-xs font-medium transition-colors',
              isActive
                ? 'bg-amber-500 text-white shadow-md'
                : 'bg-white/10 text-[var(--gaming-text-secondary,#ccc)] hover:bg-white/20'
            )}
          >
            <Icon className="h-5 w-5" aria-hidden="true" />
            <span>{label}</span>
          </button>
        );
      })}
    </div>
  );
}

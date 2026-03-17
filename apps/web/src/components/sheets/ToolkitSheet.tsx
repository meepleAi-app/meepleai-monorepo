'use client';

import React, { useState } from 'react';

import { ArrowLeft, X } from 'lucide-react';

import { Counter } from '@/components/toolkit/Counter';
import { DiceRoller } from '@/components/toolkit/DiceRoller';
import { Randomizer } from '@/components/toolkit/Randomizer';
import { Scoreboard } from '@/components/toolkit/Scoreboard';
import { Timer } from '@/components/toolkit/Timer';
import { Button } from '@/components/ui/primitives/button';

export interface ToolkitSheetProps {
  isOpen: boolean;
  onClose: () => void;
}

type ToolId = 'dice' | 'timer' | 'counter' | 'scoreboard' | 'randomizer';

interface ToolDefinition {
  id: ToolId;
  icon: string;
  name: string;
  desc: string;
  component: React.ComponentType;
}

const TOOLS: ToolDefinition[] = [
  {
    id: 'dice',
    icon: '🎲',
    name: 'Dado',
    desc: 'Lancia dadi configurabili',
    component: DiceRoller,
  },
  {
    id: 'timer',
    icon: '⏱️',
    name: 'Timer',
    desc: 'Countdown e cronometro',
    component: Timer,
  },
  {
    id: 'counter',
    icon: '🔢',
    name: 'Contatore',
    desc: 'Contatori multipli',
    component: Counter,
  },
  {
    id: 'scoreboard',
    icon: '📊',
    name: 'Segnapunti',
    desc: 'Tabellone per N giocatori',
    component: Scoreboard,
  },
  {
    id: 'randomizer',
    icon: '🎰',
    name: 'Randomizer',
    desc: 'Selezione casuale da lista',
    component: Randomizer,
  },
];

/**
 * ToolkitSheet — slide-over sheet with 5 board game utility tools.
 * Shows a tool grid initially; clicking a tool renders it inline.
 */
export function ToolkitSheet({ isOpen, onClose }: ToolkitSheetProps) {
  const [activeTool, setActiveTool] = useState<ToolId | null>(null);

  if (!isOpen) return null;

  const tool = activeTool ? TOOLS.find(t => t.id === activeTool) : null;
  const ActiveComponent = tool?.component ?? null;

  return (
    <>
      {/* Overlay */}
      <div
        className="fixed inset-0 z-50 bg-black/60 transition-opacity"
        onClick={onClose}
        aria-hidden="true"
        data-testid="toolkit-sheet-overlay"
      />

      {/* Sheet panel */}
      <div
        className="fixed inset-y-0 right-0 z-50 flex w-full flex-col bg-white shadow-xl sm:max-w-md"
        role="dialog"
        aria-modal="true"
        aria-label="Toolkit"
        data-testid="toolkit-sheet"
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3">
          <div className="flex items-center gap-2">
            {activeTool && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setActiveTool(null)}
                aria-label="Back to toolkit grid"
                className="h-8 w-8 p-0"
                data-testid="toolkit-back-button"
              >
                <ArrowLeft className="h-4 w-4" />
              </Button>
            )}
            <h2 className="text-base font-semibold text-slate-900">
              {tool ? `${tool.icon} ${tool.name}` : '🧰 Toolkit'}
            </h2>
          </div>

          <Button
            variant="ghost"
            size="sm"
            onClick={onClose}
            aria-label="Close toolkit"
            className="h-8 w-8 p-0"
            data-testid="toolkit-close-button"
          >
            <X className="h-4 w-4" />
          </Button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-4">
          {!activeTool ? (
            /* Tool grid */
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3" data-testid="toolkit-grid">
              {TOOLS.map(t => (
                <button
                  key={t.id}
                  onClick={() => setActiveTool(t.id)}
                  className="flex flex-col items-center gap-2 rounded-xl border border-slate-200 bg-white p-4 text-center shadow-sm transition-all hover:border-amber-300 hover:bg-amber-50 hover:shadow-md focus:outline-none focus:ring-2 focus:ring-amber-400"
                  aria-label={`Open ${t.name}`}
                  data-testid={`toolkit-tool-${t.id}`}
                >
                  <span className="text-3xl" role="img" aria-hidden="true">
                    {t.icon}
                  </span>
                  <span className="text-sm font-semibold text-slate-800">{t.name}</span>
                  <span className="text-xs text-slate-500 leading-snug">{t.desc}</span>
                </button>
              ))}
            </div>
          ) : ActiveComponent ? (
            /* Active tool */
            <div data-testid={`toolkit-active-${activeTool}`}>
              <ActiveComponent />
            </div>
          ) : null}
        </div>
      </div>
    </>
  );
}

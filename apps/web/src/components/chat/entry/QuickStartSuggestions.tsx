/**
 * QuickStartSuggestions — Context-aware prompt suggestions
 *
 * Shows clickable suggestion pills. When a game is selected,
 * suggestions are game-specific; otherwise, generic rule suggestions.
 */

'use client';

import React, { useMemo } from 'react';

import { Sparkles, Zap } from 'lucide-react';

import { cn } from '@/lib/utils';

import { getQuickStartSuggestions } from './constants';

import type { PromptType } from './types';

export interface QuickStartSuggestionsProps {
  gameName?: string;
  onSelect: (message: string, promptType?: PromptType) => void;
  disabled?: boolean;
  className?: string;
}

export function QuickStartSuggestions({
  gameName,
  onSelect,
  disabled = false,
  className,
}: QuickStartSuggestionsProps) {
  const suggestions = useMemo(() => getQuickStartSuggestions(gameName), [gameName]);

  return (
    <section
      className={cn(
        'p-6 rounded-2xl bg-white/70 dark:bg-card/70 backdrop-blur-md border border-border/50',
        className
      )}
      data-testid="quick-start-section"
    >
      <div className="flex items-center gap-2 mb-4">
        <Sparkles className="h-5 w-5 text-amber-600 dark:text-amber-400" />
        <h2 className="text-lg font-semibold font-quicksand text-foreground">Quick Start</h2>
      </div>

      <div className="flex flex-wrap gap-2">
        {suggestions.map(s => (
          <button
            key={s.label}
            onClick={() => onSelect(s.message, s.promptType)}
            disabled={disabled}
            className={cn(
              'px-4 py-2 rounded-full text-sm font-nunito transition-all duration-200',
              'border border-border/50',
              disabled
                ? 'bg-muted text-muted-foreground/50 cursor-not-allowed'
                : 'bg-white/70 dark:bg-card/70 backdrop-blur-md text-foreground hover:bg-amber-50 dark:hover:bg-amber-500/10 hover:border-amber-300 cursor-pointer'
            )}
            data-testid={`quick-start-${s.label.replace(/\s/g, '-').toLowerCase()}`}
          >
            <Zap className="inline h-3.5 w-3.5 mr-1.5 text-amber-500" />
            {s.label}
          </button>
        ))}
      </div>
    </section>
  );
}

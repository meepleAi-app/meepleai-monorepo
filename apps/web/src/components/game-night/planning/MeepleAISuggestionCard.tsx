'use client';

import { Bot } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';

// The featured variant is a fixed 400px wide, but this card sits in the planning layout's 320px left column
// (and in a narrower one on phones): uncapped it spills over the right column and past the viewport edge.
const FIT_COLUMN = 'max-w-full';

interface Suggestion {
  gameTitle: string;
  reason: string;
}

interface MeepleAISuggestionCardProps {
  suggestions: Suggestion[];
}

export function MeepleAISuggestionCard({ suggestions }: MeepleAISuggestionCardProps) {
  if (suggestions.length === 0) {
    return (
      <MeepleCard
        entity="agent"
        variant="featured"
        title="Suggerimenti AI"
        subtitle="Aggiungi giocatori per suggerimenti"
        badge="AI"
        headingLevel={2}
        className={FIT_COLUMN}
        data-testid="ai-suggestion-card"
      />
    );
  }

  return (
    <div data-testid="ai-suggestion-card">
      <MeepleCard
        entity="agent"
        variant="featured"
        title="Suggerimenti AI"
        badge="AI"
        headingLevel={2}
        className={FIT_COLUMN}
      />
      <div className="space-y-2 px-4 pb-4 -mt-2 rounded-b-2xl border border-t-0 border-border/50 bg-card">
        {suggestions.map(s => (
          <div key={s.gameTitle} className="flex items-start gap-2">
            <Bot className="h-4 w-4 text-cyan-500 mt-0.5 shrink-0" />
            <div>
              <span className="text-sm font-medium">{s.gameTitle}</span>
              <p className="text-xs text-muted-foreground">{s.reason}</p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

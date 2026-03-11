'use client';

import { Bot, Sparkles } from 'lucide-react';

interface Suggestion {
  gameTitle: string;
  reason: string;
}

interface AISuggestionCardProps {
  suggestions: Suggestion[];
}

export function AISuggestionCard({ suggestions }: AISuggestionCardProps) {
  return (
    <div className="rounded-xl border border-purple-500/20 bg-gradient-to-br from-purple-500/5 to-primary/5 p-4">
      <div className="flex items-center gap-2 mb-3">
        <Sparkles className="h-4 w-4 text-purple-500" />
        <h3 className="text-sm font-semibold">Suggerimenti AI</h3>
      </div>
      {suggestions.length === 0 ? (
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Bot className="h-4 w-4" />
          <span>Nessun suggerimento — aggiungi giocatori per consigli personalizzati</span>
        </div>
      ) : (
        <div className="space-y-2">
          {suggestions.map(s => (
            <div key={s.gameTitle} className="flex items-start gap-2">
              <Bot className="h-4 w-4 text-purple-500 mt-0.5 shrink-0" />
              <div>
                <span className="text-sm font-medium">{s.gameTitle}</span>
                <p className="text-xs text-muted-foreground">{s.reason}</p>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

'use client';
import { type JSX, useState } from 'react';

export interface DrawerIdleLabels {
  welcomeTitle: string;
  welcomeBody: string;
  suggestionsLabel: string;
  placeholder: string;
  sendLabel: string;
}

export function DrawerIdle({
  suggestions,
  onAsk,
  labels,
}: {
  suggestions: readonly string[];
  onAsk: (query: string) => void;
  labels: DrawerIdleLabels;
}): JSX.Element {
  const [draft, setDraft] = useState('');
  return (
    <div data-testid="drawer-state-idle" className="flex-1 p-4 flex flex-col justify-between">
      <div>
        <div className="p-5 rounded-md bg-entity-kb/5 border border-entity-kb/20 text-center mb-4">
          <div className="text-3xl mb-2">👋</div>
          <div className="font-display font-bold text-sm mb-1">{labels.welcomeTitle}</div>
          <div className="text-xs text-muted-foreground leading-relaxed">{labels.welcomeBody}</div>
        </div>
        <div className="font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground mb-2">
          {labels.suggestionsLabel}
        </div>
        {suggestions.map(s => (
          <button
            key={s}
            type="button"
            onClick={() => onAsk(s)}
            className="flex items-center gap-2 w-full p-2.5 mb-2 rounded-md bg-muted border border-border hover:bg-entity-kb/5 text-left text-xs text-foreground"
          >
            <span className="text-entity-kb shrink-0">↪</span>
            <span className="flex-1">{s}</span>
          </button>
        ))}
      </div>
      <form
        onSubmit={e => {
          e.preventDefault();
          if (draft.trim()) {
            onAsk(draft);
            setDraft('');
          }
        }}
        className="bg-muted rounded-md border border-entity-kb/20 p-2 flex items-center gap-2"
      >
        <input
          value={draft}
          onChange={e => setDraft(e.target.value)}
          placeholder={labels.placeholder}
          className="flex-1 bg-transparent border-none outline-none font-body text-sm text-foreground"
        />
        <button
          type="submit"
          aria-label={labels.sendLabel}
          className="w-8 h-8 rounded-sm bg-entity-kb text-white text-sm"
        >
          ↑
        </button>
      </form>
    </div>
  );
}

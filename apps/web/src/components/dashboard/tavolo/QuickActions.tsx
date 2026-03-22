'use client';

interface QuickActionsProps {
  onAddScore: () => void;
  onAskAi: () => void;
}

export function QuickActions({ onAddScore, onAskAi }: QuickActionsProps) {
  return (
    <div data-testid="quick-actions" className="flex gap-2">
      {/* Primary: Add score */}
      <button
        type="button"
        onClick={onAddScore}
        className="flex-1 rounded-xl bg-[hsl(25,95%,38%)] px-4 py-2.5 font-quicksand text-sm font-bold text-white transition-all hover:bg-[hsl(25,95%,33%)] active:scale-95"
      >
        Aggiungi Punteggio
      </button>

      {/* Secondary: Ask AI */}
      <button
        type="button"
        onClick={onAskAi}
        aria-label="Chiedi all'AI"
        className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-border/30 bg-card/60 text-lg backdrop-blur-sm transition-all hover:shadow-md active:scale-95"
      >
        🤖
      </button>
    </div>
  );
}

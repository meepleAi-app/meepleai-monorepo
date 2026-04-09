'use client';

import type { ChatGameContext } from '@/lib/stores/chat-panel-store';

interface ChatContextSwitcherProps {
  gameContext: ChatGameContext | null;
  onPickGame: () => void;
}

export function ChatContextSwitcher({ gameContext, onPickGame }: ChatContextSwitcherProps) {
  return (
    <div className="flex flex-shrink-0 flex-wrap items-center gap-2.5 border-b border-[var(--nh-border-default)] bg-[var(--nh-bg-base)] px-5 py-3">
      <span className="mr-0.5 text-[0.7rem] font-extrabold uppercase tracking-wider text-[var(--nh-text-muted)]">
        Contesto
      </span>
      {gameContext ? (
        <button
          type="button"
          onClick={onPickGame}
          className="flex items-center gap-2.5 rounded-full border border-[hsla(25,95%,45%,0.35)] bg-[hsla(25,95%,45%,0.08)] py-1.5 pl-1.5 pr-3.5 shadow-[var(--shadow-warm-sm)] transition-all hover:-translate-y-px hover:shadow-[var(--shadow-warm-md)]"
        >
          <span
            aria-hidden
            className="flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-[7px] text-base"
            style={{
              background: 'linear-gradient(135deg, hsl(25 75% 78%), hsl(25 80% 55%))',
            }}
          >
            🎲
          </span>
          <div className="text-left">
            <div className="font-quicksand text-[0.82rem] font-extrabold leading-none text-[var(--nh-text-primary)]">
              {gameContext.name}
            </div>
            <div className="mt-1 text-[0.66rem] font-semibold text-[var(--nh-text-muted)]">
              {gameContext.year ? `${gameContext.year} · ` : ''}
              {gameContext.pdfCount} PDF · KB{' '}
              {gameContext.kbStatus === 'ready' ? 'pronta' : gameContext.kbStatus}
            </div>
          </div>
          <span aria-hidden className="text-xs text-[var(--nh-text-muted)]">
            ▾
          </span>
        </button>
      ) : (
        <button
          type="button"
          onClick={onPickGame}
          className="rounded-full border border-dashed border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] px-4 py-2 font-nunito text-[0.78rem] font-semibold text-[var(--nh-text-muted)] hover:bg-white hover:shadow-[var(--shadow-warm-sm)]"
        >
          Seleziona gioco
        </button>
      )}
    </div>
  );
}

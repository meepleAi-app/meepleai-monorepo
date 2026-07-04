'use client';

import { useEffect, type JSX } from 'react';

export interface BlockedLiveSessionModalProps {
  readonly open: boolean;
  readonly onClose: () => void;
  /** Jump to the session that is already live ("Aprila per continuare"). */
  readonly onJumpToLive?: () => void;
}

/**
 * #2633 WS1 DEC-10 / DEC-13: shown when starting a game returns the max-1-live 409
 * (`MAX_LIVE_SESSIONS_EXCEEDED`). Honest copy — WS1 cannot complete a game, so it points the
 * organizer at the running session rather than promising a resolution it does not offer.
 */
export function BlockedLiveSessionModal({
  open,
  onClose,
  onJumpToLive,
}: BlockedLiveSessionModalProps): JSX.Element | null {
  // Global Escape: closes regardless of focus (mirrors NightLiveClientView's transition modal).
  useEffect(() => {
    if (!open) return undefined;
    const handle = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handle);
    return () => document.removeEventListener('keydown', handle);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 backdrop-blur-sm"
      style={{ background: 'rgba(0,0,0,0.45)' }}
      role="presentation"
      onClick={onClose}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="blocked-live-session-title"
        onClick={e => e.stopPropagation()}
        className="w-full max-w-sm rounded-xl border border-border bg-card p-5 text-center shadow-lg"
      >
        <h2
          id="blocked-live-session-title"
          className="font-display text-lg font-extrabold text-foreground"
        >
          C&apos;è già una partita live
        </h2>
        <p className="mt-2 font-mono text-sm text-muted-foreground">
          Puoi avere una sola partita live per serata. Aprila per continuare.
        </p>
        <div className="mt-4 flex items-center justify-center gap-2">
          {onJumpToLive ? (
            <button
              type="button"
              onClick={onJumpToLive}
              className="rounded-md border border-entity-session/30 bg-entity-session/10 px-4 py-2 font-display text-[13px] font-extrabold text-entity-session hover:bg-entity-session/15"
            >
              Apri la partita live
            </button>
          ) : null}
          <button
            type="button"
            onClick={onClose}
            className="rounded-md border border-border bg-card px-4 py-2 font-display text-[13px] font-extrabold text-foreground hover:bg-muted"
          >
            Chiudi
          </button>
        </div>
      </div>
    </div>
  );
}

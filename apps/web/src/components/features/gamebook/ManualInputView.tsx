'use client';

import { useEffect, useMemo, useRef, useState, type ReactElement } from 'react';

import { useGameBooks } from '@/hooks/useGameBooks';
import { trackEvent } from '@/lib/analytics/track-event';
import { GameBookRole, hasRole, type GameRef } from '@/lib/api/gamebook';
import { useTranslateTextSSE } from '@/lib/gamebook/hooks/useTranslateTextSSE';
import { LANG_CODES_ORDER, LANG_LABELS_IT, type SourceLangCode } from '@/lib/gamebook/lang-codes';
import { getLastUsedLang, setLastUsedLang } from '@/lib/gamebook/last-used-lang';

import { TranslationPane } from './TranslationPane';

const MAX_CHARS = 2000;
const WARN_THRESHOLD = 1800;

export interface ManualInputViewProps {
  campaignId: string;
  gameRef: GameRef;
}

export function ManualInputView({ campaignId, gameRef }: ManualInputViewProps): ReactElement {
  const { data: books } = useGameBooks(gameRef);
  const narrativeBooks = useMemo(
    () => (books ?? []).filter(b => hasRole(b.roles, GameBookRole.Narrative)),
    [books]
  );
  const bookId = narrativeBooks[0]?.id;
  const bookName = narrativeBooks[0]?.displayName ?? '';

  const [text, setText] = useState('');
  const [sourceLang, setSourceLang] = useState<SourceLangCode>('IT');
  const [langOpen, setLangOpen] = useState(false);
  const sse = useTranslateTextSSE();

  // Init from localStorage post-mount (SSR-safe)
  useEffect(() => {
    setSourceLang(getLastUsedLang());
  }, []);

  const len = text.length;
  const overLimit = len > MAX_CHARS;
  const warn = len >= WARN_THRESHOLD && !overLimit;
  // M2 review fix (#1560): include `!sse.isComplete` guard per plan T4 spec —
  // disables re-submit after success until user modifies text.
  const canSubmit = len > 0 && !overLimit && !!bookId && !sse.isComplete;

  // M2 review fix (#1560): emit DEC-FE-M-10 lifecycle analytics events.
  // `manual_completed` on SSE final chunk, `manual_failed` on SSE error.
  const submitStartRef = useRef<number | null>(null);
  const completedFiredRef = useRef(false);
  const failedFiredRef = useRef(false);

  useEffect(() => {
    if (!bookId) return;
    if (sse.isComplete && !completedFiredRef.current) {
      completedFiredRef.current = true;
      const durationMs = submitStartRef.current ? Date.now() - submitStartRef.current : null;
      trackEvent('translate.manual_completed', {
        campaignId,
        sourceLang,
        gameBookId: bookId,
        durationMs,
      });
    }
    if (sse.error && !failedFiredRef.current) {
      failedFiredRef.current = true;
      trackEvent('translate.manual_failed', {
        campaignId,
        sourceLang,
        gameBookId: bookId,
        errorCode: sse.error,
      });
    }
  }, [sse.isComplete, sse.error, campaignId, sourceLang, bookId]);

  const counterClass = `manual-char-counter${overLimit ? ' over' : warn ? ' warn' : ''}`;

  const phase: 'idle' | 'translating' | 'translated' = sse.isComplete
    ? 'translated'
    : sse.partialText.length > 0
      ? 'translating'
      : 'idle';

  const handleSubmit = () => {
    if (!canSubmit || !bookId) return;
    // M2 review fix: reset lifecycle event guards for re-translate scenarios
    completedFiredRef.current = false;
    failedFiredRef.current = false;
    submitStartRef.current = Date.now();
    trackEvent('translate.manual_submit', {
      campaignId,
      textLength: len,
      sourceLang,
      gameBookId: bookId,
    });
    void sse.start(campaignId, text, sourceLang, bookId);
    setLastUsedLang(sourceLang);
  };

  return (
    <div className="manual-input-shell flex flex-col">
      <header className="manual-input-head sticky top-0 z-10 flex flex-col gap-2 border-b border-border bg-background p-4">
        <div className="title-row flex items-center gap-2">
          <h2 className="flex-1 text-lg font-bold">Inserimento manuale</h2>
          <span className="badge-manual rounded-full bg-[var(--c-agent)]/15 px-2 py-0.5 font-mono text-[9px] font-bold uppercase tracking-wider text-[var(--c-agent)]">
            MANUAL
          </span>
        </div>
        <div className="meta-row relative flex flex-wrap items-center gap-2 font-mono text-xs text-muted-foreground">
          <button
            type="button"
            aria-label="Lingua sorgente del paragrafo"
            aria-expanded={langOpen}
            aria-haspopup="listbox"
            onClick={() => setLangOpen(o => !o)}
            className="manual-lang-dropdown inline-flex items-center gap-2 rounded-full border border-border bg-card px-2 py-1 font-mono text-xs text-foreground"
          >
            <span className="flag" aria-hidden>
              🌐
            </span>
            <span>{LANG_LABELS_IT[sourceLang]}</span>
            <span className="chevron text-[10px] text-muted-foreground" aria-hidden>
              ▼
            </span>
          </button>
          {langOpen && (
            <ul
              role="listbox"
              className="absolute left-0 top-full z-20 mt-1 rounded-md border border-border bg-card shadow-lg"
            >
              {LANG_CODES_ORDER.map(code => (
                <li
                  key={code}
                  role="option"
                  aria-selected={code === sourceLang}
                  tabIndex={0}
                  onClick={() => {
                    setSourceLang(code);
                    setLangOpen(false);
                  }}
                  onKeyDown={e => {
                    if (e.key === 'Enter') {
                      setSourceLang(code);
                      setLangOpen(false);
                    }
                  }}
                  className="cursor-pointer px-3 py-2 text-sm hover:bg-muted"
                >
                  {LANG_LABELS_IT[code]}
                </li>
              ))}
            </ul>
          )}
          {bookName && (
            <span
              className="book-ref-chip inline-flex items-center gap-1 rounded-sm bg-[var(--c-game)]/10 px-2 py-1 font-mono text-[10px] font-bold text-[var(--c-game)]"
              aria-label="Libro corrente"
            >
              📖 {bookName}{' '}
              <span className="lock text-[9px] opacity-70" aria-hidden>
                🔒
              </span>
            </span>
          )}
        </div>
      </header>

      <div className="manual-input-body flex flex-1 flex-col gap-3 p-4">
        <div className="manual-textarea-shell relative rounded-md border border-border bg-card focus-within:border-[var(--c-kb)] focus-within:ring-2 focus-within:ring-[var(--c-kb)]/15">
          <textarea
            aria-label="Inserisci il testo del paragrafo"
            value={text}
            onChange={e => setText(e.target.value)}
            placeholder="Digita il paragrafo qui..."
            className="manual-textarea w-full min-h-[200px] resize-y border-0 bg-transparent p-3 pb-7 font-serif text-base leading-snug text-foreground outline-none placeholder:italic placeholder:text-muted-foreground"
          />
          <span
            aria-live="polite"
            className={`${counterClass} absolute bottom-2 right-3 rounded-sm px-2 py-0.5 font-mono text-[10px] tabular-nums ${overLimit ? 'bg-destructive/10 text-destructive' : warn ? 'bg-muted text-[var(--c-warning,orange)]' : 'bg-muted text-muted-foreground'}`}
          >
            {len}/{MAX_CHARS}
          </span>
        </div>
        <p className="manual-hint rounded-md border border-dashed border-[var(--c-kb)]/25 bg-[var(--c-kb)]/[0.06] p-2 px-3 font-mono text-xs text-muted-foreground">
          <strong className="text-[var(--c-kb)]">💡 Skip OCR</strong> — diretto a step 3
          (traduzione)
        </p>
      </div>

      <footer className="manual-input-foot flex flex-col gap-2 border-t border-border bg-muted p-4">
        <button
          type="button"
          onClick={handleSubmit}
          disabled={!canSubmit}
          aria-disabled={!canSubmit}
          className="manual-cta-primary flex items-center justify-center gap-2 rounded-md bg-[var(--c-kb)] px-5 py-3 font-bold text-white disabled:cursor-not-allowed disabled:opacity-55"
          data-testid="manual-cta-submit"
        >
          Traduci{' '}
          <span className="arrow" aria-hidden>
            →
          </span>
        </button>
        <p className="manual-flow-note m-0 text-center font-mono text-[10px] text-muted-foreground">
          <strong className="text-[var(--c-success,green)]">✓</strong> Salta scatto + OCR, tre passi
          in uno
        </p>
      </footer>

      {(phase === 'translating' || phase === 'translated') && (
        <div className="p-4">
          <TranslationPane
            partialText={sse.partialText}
            isComplete={sse.isComplete}
            appliedTerms={sse.appliedTerms}
            sourceTextEn={text}
            error={sse.error}
          />
        </div>
      )}
    </div>
  );
}

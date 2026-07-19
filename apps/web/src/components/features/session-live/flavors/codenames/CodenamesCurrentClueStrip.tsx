'use client';

import { type ReactElement, useState } from 'react';

import type { CodenamesClue, CodenamesTeam } from './codenames-state';

export interface CodenamesCurrentClueStripProps {
  readonly clue: CodenamesClue | null;
  readonly currentTeam: CodenamesTeam;
  readonly editable: boolean;
  readonly onSetClue?: (word: string, number: number) => void;
  readonly onClearClue?: () => void;
  readonly onSwitchTeam?: () => void;
  readonly labels: {
    noClue: string;
    wordPlaceholder: string;
    numberAria: string;
    giveClue: string;
    endTurn: string;
  };
}

export function CodenamesCurrentClueStrip({
  clue,
  editable,
  onSetClue,
  onSwitchTeam,
  labels,
}: CodenamesCurrentClueStripProps): ReactElement {
  const [word, setWord] = useState('');
  const [num, setNum] = useState(1);

  return (
    <div
      data-slot="codenames-clue"
      className="flex flex-wrap items-center gap-2 rounded-lg border border-border bg-card p-2"
    >
      {clue != null ? (
        <span data-slot="codenames-clue-active" className="text-sm font-bold text-foreground">
          {clue.word} : <span className="tabular-nums">{clue.number}</span>
        </span>
      ) : (
        <span className="text-xs text-muted-foreground">{labels.noClue}</span>
      )}

      {editable && (
        <span className="ml-auto flex items-center gap-1">
          <input
            type="text"
            aria-label={labels.wordPlaceholder}
            placeholder={labels.wordPlaceholder}
            value={word}
            onChange={e => setWord(e.target.value)}
            className="w-24 rounded border border-border bg-background px-2 py-1 text-xs text-foreground"
          />
          <input
            type="number"
            min={0}
            aria-label={labels.numberAria}
            value={num}
            onChange={e => setNum(Number(e.target.value))}
            className="w-14 rounded border border-border bg-background px-2 py-1 text-xs tabular-nums text-foreground"
          />
          <button
            type="button"
            onClick={() => onSetClue?.(word.trim(), num)}
            className="rounded-md border border-border bg-background px-2 py-1 text-xs font-semibold text-foreground hover:bg-muted"
          >
            {labels.giveClue}
          </button>
          <button
            type="button"
            onClick={() => onSwitchTeam?.()}
            className="rounded-md border border-border bg-background px-2 py-1 text-xs font-semibold text-foreground hover:bg-muted"
          >
            {labels.endTurn}
          </button>
        </span>
      )}
    </div>
  );
}

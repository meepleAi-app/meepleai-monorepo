'use client';

import type { ReactElement } from 'react';

import type { GamebookSegment } from '@/lib/api/gamebook-photos';

export interface SegmentPickerProps {
  segments: ReadonlyArray<GamebookSegment>;
  onPick: (paragraphNumber: number) => void;
  disabled?: boolean;
}

export function SegmentPicker({ segments, onPick, disabled }: SegmentPickerProps): ReactElement {
  if (segments.length === 0) {
    return (
      <p className="text-sm text-muted-foreground" data-testid="segment-picker-empty">
        Nessun paragrafo riconosciuto.
      </p>
    );
  }
  return (
    <ul className="grid gap-2" data-testid="segment-picker">
      {segments.map(s => (
        <li
          key={s.paragraphNumber}
          className="flex items-start gap-3 rounded-md border border-[var(--c-game)]/20 bg-background p-3"
        >
          <span className="font-mono text-sm text-[var(--c-game)] min-w-[3ch]">
            §{s.paragraphNumber}
          </span>
          <p className="flex-1 text-sm text-muted-foreground line-clamp-3">{s.sourceText}</p>
          <button
            type="button"
            onClick={() => onPick(s.paragraphNumber)}
            disabled={disabled}
            className="rounded-md bg-[var(--c-game)] px-3 py-1.5 text-xs font-medium text-white disabled:opacity-50"
            data-testid={`segment-picker-translate-${s.paragraphNumber}`}
          >
            Traduci
          </button>
        </li>
      ))}
    </ul>
  );
}

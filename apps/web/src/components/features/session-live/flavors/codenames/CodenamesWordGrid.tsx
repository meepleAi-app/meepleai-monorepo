'use client';

import { type ReactElement } from 'react';

import { codenamesKeyColor } from './codenames-palette';

import type { CodenamesCell } from './codenames-state';

export interface CodenamesWordGridProps {
  readonly board: CodenamesCell[];
  readonly editable: boolean;
  readonly perspective: 'operative' | 'spymaster';
  readonly onRevealCell?: (index: number) => void;
  readonly revealAriaTemplate: string;
}

export function CodenamesWordGrid({
  board,
  editable,
  perspective,
  onRevealCell,
  revealAriaTemplate,
}: CodenamesWordGridProps): ReactElement {
  return (
    <div
      data-slot="codenames-board"
      role="group"
      aria-label="Codenames"
      className="grid grid-cols-5 gap-1"
    >
      {board.map((cell, i) => {
        // Show the key colour when the cell is revealed, OR (for the spymaster view) always.
        const showKey = cell.revealed || perspective === 'spymaster';
        const bg = showKey ? codenamesKeyColor(cell.key) : undefined;
        const aria = revealAriaTemplate.replace('{word}', cell.word);
        const common = {
          'data-slot': 'codenames-cell',
          'data-index': String(i),
          ...(showKey ? { 'data-key': cell.key } : {}),
          'data-revealed': cell.revealed ? 'true' : 'false',
          className: [
            'flex min-h-10 items-center justify-center rounded p-1 text-center text-[11px] font-semibold',
            showKey ? '' : 'bg-card text-foreground',
            cell.revealed ? 'opacity-90 ring-2 ring-border-strong' : '',
          ].join(' '),
          style: bg ? { backgroundColor: bg, color: 'hsl(0, 0%, 100%)' } : undefined,
        };
        const content = <span className="truncate">{cell.word}</span>;

        if (editable && !cell.revealed) {
          return (
            <button
              key={i}
              type="button"
              aria-label={aria}
              onClick={() => onRevealCell?.(i)}
              {...common}
            >
              {content}
            </button>
          );
        }
        return (
          <div key={i} {...common}>
            {content}
          </div>
        );
      })}
    </div>
  );
}

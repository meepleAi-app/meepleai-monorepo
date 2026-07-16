'use client';

import { type ReactElement } from 'react';

import { catanTerrainColor } from './catan-palette';

import type { CatanGameState, CatanHex } from './catan-state';

const R = 34; // hex circumradius (px)
const COL_STEP = 1.5 * R;
const ROW_STEP = Math.sqrt(3) * R;
const MAX_H = 5;
const PAD = 8;

/** Flat-top hex vertices centred at (cx, cy). */
function hexPoints(cx: number, cy: number): string {
  const pts: string[] = [];
  for (let i = 0; i < 6; i++) {
    const a = (Math.PI / 180) * (60 * i);
    pts.push(`${(cx + R * Math.cos(a)).toFixed(2)},${(cy + R * Math.sin(a)).toFixed(2)}`);
  }
  return pts.join(' ');
}

function center(hex: CatanHex, colHeight: number): { cx: number; cy: number } {
  const cx = PAD + R + hex.col * COL_STEP;
  const yOffset = ((MAX_H - colHeight) / 2 + hex.row) * ROW_STEP + ROW_STEP / 2;
  return { cx, cy: PAD + yOffset };
}

const HOT = new Set([6, 8]);

export interface CatanHexBoardProps {
  readonly board: CatanGameState['board'];
  readonly editable: boolean;
  readonly onMoveRobber?: (hexId: string) => void;
  readonly hexAriaTemplate: string; // "{terrain} {number}"
  readonly robberLabel: string;
}

export function CatanHexBoard({
  board,
  editable,
  onMoveRobber,
  hexAriaTemplate,
  robberLabel,
}: CatanHexBoardProps): ReactElement {
  const colHeights = [0, 1, 2, 3, 4].map(c => board.hexes.filter(h => h.col === c).length);
  const width = PAD * 2 + R + 4 * COL_STEP + R;
  const height = PAD * 2 + MAX_H * ROW_STEP;

  return (
    <svg
      data-slot="catan-board"
      viewBox={`0 0 ${width.toFixed(0)} ${height.toFixed(0)}`}
      className="h-auto w-full max-w-md"
      role="img"
      aria-label="Catan board"
    >
      {board.hexes.map(hex => {
        const { cx, cy } = center(hex, colHeights[hex.col] ?? MAX_H);
        const isRobber = hex.id === board.robberHexId;
        const aria = hexAriaTemplate
          .replace('{terrain}', hex.terrain)
          .replace('{number}', hex.number == null ? '' : String(hex.number))
          .trim();
        const tile = (
          <>
            <polygon
              points={hexPoints(cx, cy)}
              fill={catanTerrainColor(hex.terrain)}
              stroke="hsl(0,0%,100%)"
              strokeWidth={1.5}
            />
            {hex.number != null && (
              <text
                x={cx}
                y={cy + 4}
                textAnchor="middle"
                className={HOT.has(hex.number) ? 'catan-hot' : undefined}
                style={{
                  fontWeight: 800,
                  fontSize: 15,
                  fill: HOT.has(hex.number) ? 'hsl(0,72%,42%)' : 'hsl(0,0%,15%)',
                }}
              >
                {hex.number}
              </text>
            )}
            {isRobber && (
              <circle
                data-slot="catan-robber"
                data-hex={hex.id}
                cx={cx}
                cy={cy - 12}
                r={7}
                fill="hsl(0,0%,12%)"
                stroke="hsl(0,0%,100%)"
                strokeWidth={1.5}
              >
                <title>{robberLabel}</title>
              </circle>
            )}
          </>
        );

        if (editable) {
          return (
            <g
              key={hex.id}
              data-slot="catan-hex"
              data-hex={hex.id}
              role="button"
              tabIndex={0}
              aria-label={aria}
              style={{ cursor: 'pointer' }}
              onClick={() => onMoveRobber?.(hex.id)}
              onKeyDown={e => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault();
                  onMoveRobber?.(hex.id);
                }
              }}
            >
              {tile}
            </g>
          );
        }
        return (
          <g key={hex.id} data-slot="catan-hex" data-hex={hex.id} aria-label={aria}>
            {tile}
          </g>
        );
      })}
    </svg>
  );
}

'use client';

import { type ReactElement } from 'react';

import type { LiveSessionPlayerDto } from '@/lib/api/schemas/live-sessions.schemas';

import { catanPieceColor } from './catan-palette';
import { CATAN_PIECE_TOTALS, type CatanPiece, type CatanPlayerState } from './catan-state';

export interface CatanPlayerCardLabels {
  readonly vpLabel: string;
  readonly handLabel: string;
  readonly devLabel: string;
  readonly settlementsLabel: string;
  readonly citiesLabel: string;
  readonly roadsLabel: string;
  readonly longestRoadLabel: string;
  readonly largestArmyLabel: string;
  readonly incAriaTemplate: string; // "{field} +1"
  readonly decAriaTemplate: string; // "{field} -1"
}

export interface CatanPlayerCardProps {
  readonly player: LiveSessionPlayerDto;
  readonly state: CatanPlayerState;
  readonly vp: number;
  readonly editable: boolean;
  readonly onBumpBuilt?: (piece: CatanPiece, delta: 1 | -1) => void;
  readonly onSetDev?: (delta: 1 | -1) => void;
  readonly onSetHand?: (delta: 1 | -1) => void;
  readonly onToggleBadge?: (badge: 'longestRoad' | 'largestArmy') => void;
  readonly labels: CatanPlayerCardLabels;
}

function Stepper({
  label,
  value,
  editable,
  incAria,
  decAria,
  onDelta,
}: {
  label: string;
  value: string;
  editable: boolean;
  incAria: string;
  decAria: string;
  onDelta?: (delta: 1 | -1) => void;
}): ReactElement {
  return (
    <div className="flex items-center justify-between gap-1 text-xs">
      <span className="text-muted-foreground">{label}</span>
      <span className="flex items-center gap-1">
        {editable && (
          <button
            type="button"
            aria-label={decAria}
            onClick={() => onDelta?.(-1)}
            className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted"
          >
            −
          </button>
        )}
        <span className="min-w-4 text-center font-semibold tabular-nums text-foreground">
          {value}
        </span>
        {editable && (
          <button
            type="button"
            aria-label={incAria}
            onClick={() => onDelta?.(1)}
            className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted"
          >
            +
          </button>
        )}
      </span>
    </div>
  );
}

export function CatanPlayerCard({
  player,
  state,
  vp,
  editable,
  onBumpBuilt,
  onSetDev,
  onSetHand,
  onToggleBadge,
  labels,
}: CatanPlayerCardProps): ReactElement {
  const inc = (field: string) => labels.incAriaTemplate.replace('{field}', field);
  const dec = (field: string) => labels.decAriaTemplate.replace('{field}', field);
  const remaining = (piece: CatanPiece) => CATAN_PIECE_TOTALS[piece] - state.built[piece];

  const badgeBtn = (
    badge: 'longestRoad' | 'largestArmy',
    label: string,
    held: boolean
  ): ReactElement => {
    const cls = [
      'rounded px-1.5 py-0.5 text-[11px] font-semibold',
      held ? 'bg-entity-session/20 text-entity-session' : 'bg-muted text-muted-foreground',
    ].join(' ');
    return editable ? (
      <button
        type="button"
        aria-label={label}
        aria-pressed={held}
        onClick={() => onToggleBadge?.(badge)}
        className={cls}
      >
        {label}
      </button>
    ) : (
      <span className={cls} aria-hidden={!held}>
        {label}
      </span>
    );
  };

  return (
    <div
      data-slot="catan-player-card"
      data-active={player.isActive ? 'true' : 'false'}
      className={[
        'flex flex-col gap-1.5 rounded-lg border p-2',
        player.isActive ? 'border-entity-session/40 bg-entity-session/8' : 'border-border bg-card',
      ].join(' ')}
    >
      <div className="flex items-center gap-2">
        <span
          aria-hidden="true"
          className="h-3.5 w-3.5 shrink-0 rounded-full border border-border-strong"
          style={{ backgroundColor: catanPieceColor(player.color) }}
        />
        <span className="min-w-0 flex-1 truncate text-xs font-semibold text-foreground">
          {player.displayName}
        </span>
        <span className="text-[10px] uppercase text-muted-foreground">{labels.vpLabel}</span>
        <span className="text-sm font-bold tabular-nums text-foreground">{vp}</span>
      </div>

      <Stepper
        label={labels.handLabel}
        value={String(state.handSize)}
        editable={editable}
        incAria={inc(labels.handLabel)}
        decAria={dec(labels.handLabel)}
        onDelta={onSetHand}
      />
      <Stepper
        label={labels.settlementsLabel}
        value={`${state.built.settlements}/${remaining('settlements')}`}
        editable={editable}
        incAria={inc(labels.settlementsLabel)}
        decAria={dec(labels.settlementsLabel)}
        onDelta={d => onBumpBuilt?.('settlements', d)}
      />
      <Stepper
        label={labels.citiesLabel}
        value={`${state.built.cities}/${remaining('cities')}`}
        editable={editable}
        incAria={inc(labels.citiesLabel)}
        decAria={dec(labels.citiesLabel)}
        onDelta={d => onBumpBuilt?.('cities', d)}
      />
      <Stepper
        label={labels.roadsLabel}
        value={`${state.built.roads}/${remaining('roads')}`}
        editable={editable}
        incAria={inc(labels.roadsLabel)}
        decAria={dec(labels.roadsLabel)}
        onDelta={d => onBumpBuilt?.('roads', d)}
      />
      <Stepper
        label={labels.devLabel}
        value={String(state.devCount)}
        editable={editable}
        incAria={inc(labels.devLabel)}
        decAria={dec(labels.devLabel)}
        onDelta={onSetDev}
      />

      <div className="flex gap-1">
        {badgeBtn('longestRoad', labels.longestRoadLabel, state.badges.longestRoad)}
        {badgeBtn('largestArmy', labels.largestArmyLabel, state.badges.largestArmy)}
      </div>
    </div>
  );
}

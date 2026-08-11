'use client';

import { type ReactElement } from 'react';

import { useIntl } from 'react-intl';

import { useTranslation } from '@/hooks/useTranslation';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import { hasRequiredRole, type ParticipantRole } from '@/lib/session-live/participant-role';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import { emptyCatanPlayerState, parseCatanGameState } from './catan-state';
import { CatanDiceControl } from './CatanDiceControl';
import { CatanHexBoard } from './CatanHexBoard';
import { CatanPlayerCard, type CatanPlayerCardLabels } from './CatanPlayerCard';
import { useCatanStateEditor } from './use-catan-state-editor';

/** Internal-only — no longer a component prop; the flavor self-builds it via i18n. */
interface CatanLiveFlavorLabels extends CatanPlayerCardLabels {
  readonly panelAriaLabel: string;
  readonly roundTemplate: string; // "Round {n}"
  readonly activePlayerTemplate: string; // "Turno di {name}"
  readonly phaseTemplate: string; // "Fase: {name}"
  readonly initBoardCta: string;
  readonly viewerWaiting: string;
  readonly hexAriaTemplate: string; // "{terrain} {number}"
  readonly robberLabel: string;
  readonly diceLastLabel: string;
  readonly diceHistoryLabel: string;
  readonly rollAriaTemplate: string; // "Registra tiro {n}"
}

export interface CatanLiveFlavorProps {
  readonly session: LiveSessionDto;
  readonly viewerRole: ParticipantRole;
  readonly sessionId: string;
  readonly className?: string;
  readonly livePoints?: ReadonlyMap<string, number> | null;
  readonly phaseName?: string | null;
}

export function CatanLiveFlavor({
  session,
  viewerRole,
  sessionId,
  className,
  livePoints,
  phaseName,
}: CatanLiveFlavorProps): ReactElement {
  const { t } = useTranslation();
  const intl = useIntl();
  const isHost = hasRequiredRole(viewerRole, 'Host');
  const playerIds = session.players.map(p => p.id);
  const editor = useCatanStateEditor(sessionId, playerIds);

  // Placeholder-bearing templates ({n}/{name}/{score}) are read RAW from
  // intl.messages so react-intl does NOT ICU-interpolate them — the flavor
  // component does the runtime .replace. Same pattern as the toolkitRenderer
  // aria templates. Non-placeholder labels use t() normally.
  const labels: CatanLiveFlavorLabels = {
    panelAriaLabel: t('pages.sessionLive.flavor.catan.panelAriaLabel'),
    roundTemplate:
      (intl.messages['pages.sessionLive.flavor.catan.roundTemplate'] as string) ?? 'Round {n}',
    activePlayerTemplate:
      (intl.messages['pages.sessionLive.flavor.catan.activePlayerTemplate'] as string) ??
      'Turno di {name}',
    phaseTemplate:
      (intl.messages['pages.sessionLive.flavor.catan.phaseTemplate'] as string) ?? 'Fase: {name}',
    initBoardCta: t('pages.sessionLive.flavor.catan.initBoardCta'),
    viewerWaiting: t('pages.sessionLive.flavor.catan.viewerWaiting'),
    hexAriaTemplate:
      (intl.messages['pages.sessionLive.flavor.catan.hexAriaTemplate'] as string) ??
      '{terrain} {number}',
    robberLabel: t('pages.sessionLive.flavor.catan.robberLabel'),
    diceLastLabel: t('pages.sessionLive.flavor.catan.diceLastLabel'),
    diceHistoryLabel: t('pages.sessionLive.flavor.catan.diceHistoryLabel'),
    rollAriaTemplate:
      (intl.messages['pages.sessionLive.flavor.catan.rollAriaTemplate'] as string) ??
      'Registra tiro {n}',
    vpLabel: t('pages.sessionLive.flavor.catan.vpLabel'),
    handLabel: t('pages.sessionLive.flavor.catan.handLabel'),
    devLabel: t('pages.sessionLive.flavor.catan.devLabel'),
    settlementsLabel: t('pages.sessionLive.flavor.catan.settlementsLabel'),
    citiesLabel: t('pages.sessionLive.flavor.catan.citiesLabel'),
    roadsLabel: t('pages.sessionLive.flavor.catan.roadsLabel'),
    longestRoadLabel: t('pages.sessionLive.flavor.catan.longestRoadLabel'),
    largestArmyLabel: t('pages.sessionLive.flavor.catan.largestArmyLabel'),
    incAriaTemplate:
      (intl.messages['pages.sessionLive.flavor.catan.incAriaTemplate'] as string) ?? '{field} +1',
    decAriaTemplate:
      (intl.messages['pages.sessionLive.flavor.catan.decAriaTemplate'] as string) ?? '{field} -1',
  };

  const rawGameState = useLiveSessionStore(s => s.gameState);
  // Parse defensively; a non-catan gameState (or none) → empty view.
  const parsed = parseCatanGameState(rawGameState);

  const scoreOf = (playerId: string): number =>
    livePoints?.get(playerId) ?? session.players.find(p => p.id === playerId)?.totalScore ?? 0;

  // ── Empty state ────────────────────────────────────────────────────────────
  if (parsed == null) {
    return (
      <section
        aria-label={labels.panelAriaLabel}
        data-slot="catan-flavor-empty"
        className={`flex flex-col items-start gap-3 ${className ?? ''}`.trim()}
      >
        {isHost ? (
          <button
            type="button"
            onClick={editor.initializeState}
            className="rounded-lg border border-entity-session/40 bg-entity-session/10 px-3 py-2 text-sm font-semibold text-entity-session hover:bg-entity-session/20"
          >
            {labels.initBoardCta}
          </button>
        ) : (
          <p role="status" aria-live="polite" className="text-xs text-muted-foreground">
            {labels.viewerWaiting}
          </p>
        )}
      </section>
    );
  }

  const activePlayer = session.players.find(p => p.id === session.currentTurnPlayerId) ?? null;
  const subHeader = [
    activePlayer ? labels.activePlayerTemplate.replace('{name}', activePlayer.displayName) : null,
    phaseName ? labels.phaseTemplate.replace('{name}', phaseName) : null,
  ].filter((s): s is string => s != null);

  return (
    <section
      aria-label={labels.panelAriaLabel}
      data-slot="catan-flavor-live"
      className={`flex flex-col gap-4 ${className ?? ''}`.trim()}
    >
      <header data-slot="catan-flavor-turn" aria-live="polite" className="flex flex-col gap-0.5">
        <span className="text-sm font-semibold text-foreground">
          {labels.roundTemplate.replace('{n}', String(session.currentTurnIndex + 1))}
        </span>
        {subHeader.length > 0 && (
          <span className="text-xs text-muted-foreground">{subHeader.join(' · ')}</span>
        )}
      </header>

      <CatanHexBoard
        board={parsed.board}
        editable={isHost}
        onMoveRobber={editor.moveRobber}
        hexAriaTemplate={labels.hexAriaTemplate}
        robberLabel={labels.robberLabel}
      />

      <CatanDiceControl
        dice={parsed.dice}
        editable={isHost}
        onRoll={editor.setDiceRoll}
        lastLabel={labels.diceLastLabel}
        historyLabel={labels.diceHistoryLabel}
        rollAriaTemplate={labels.rollAriaTemplate}
      />

      <div data-slot="catan-flavor-players" className="grid grid-cols-1 gap-2 sm:grid-cols-2">
        {session.players.map(player => (
          <CatanPlayerCard
            key={player.id}
            player={player}
            state={parsed.players[player.id] ?? emptyCatanPlayerState()}
            vp={scoreOf(player.id)}
            editable={isHost}
            onBumpBuilt={(piece, delta) => editor.bumpBuilt(player.id, piece, delta)}
            onSetDev={delta => editor.setDevCount(player.id, delta)}
            onSetHand={delta => editor.setHandSize(player.id, delta)}
            onToggleBadge={badge => editor.toggleBadge(player.id, badge)}
            labels={labels}
          />
        ))}
      </div>

      {isHost && (
        <button
          type="button"
          onClick={editor.regenerateBoard}
          className="self-start text-xs text-muted-foreground underline hover:text-foreground"
        >
          {labels.initBoardCta}
        </button>
      )}
    </section>
  );
}

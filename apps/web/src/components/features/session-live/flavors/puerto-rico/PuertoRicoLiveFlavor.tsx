'use client';

import { type ReactElement } from 'react';

import { useIntl } from 'react-intl';

import { useTranslation } from '@/hooks/useTranslation';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import { hasRequiredRole, type ParticipantRole } from '@/lib/session-live/participant-role';

import { emptyPuertoRicoPlayerState } from './puerto-rico-state';
import { PuertoRicoColonistShipPanel } from './PuertoRicoColonistShipPanel';
import { PuertoRicoGalleonsPanel } from './PuertoRicoGalleonsPanel';
import { PuertoRicoPlayerMatSummary } from './PuertoRicoPlayerMatSummary';
import { PuertoRicoTradingHousePanel } from './PuertoRicoTradingHousePanel';
import { usePuertoRicoStateEditor } from './use-puerto-rico-state-editor';

export interface PuertoRicoLiveFlavorProps {
  readonly session: LiveSessionDto;
  readonly viewerRole: ParticipantRole;
  readonly sessionId: string;
  readonly className?: string;
  readonly livePoints?: ReadonlyMap<string, number> | null;
  readonly phaseName?: string | null;
}

const K = 'pages.sessionLive.flavor.puerto-rico';

export function PuertoRicoLiveFlavor({
  session,
  viewerRole,
  sessionId,
  className,
  livePoints,
}: PuertoRicoLiveFlavorProps): ReactElement {
  const { t } = useTranslation();
  const intl = useIntl();
  const isHost = hasRequiredRole(viewerRole, 'Host');
  const playerIds = session.players.map(p => p.id);
  const editor = usePuertoRicoStateEditor(sessionId, playerIds);
  const state = editor.state;

  const tmpl = (id: string, fallback: string) =>
    (intl.messages[`${K}.${id}`] as string) ?? fallback;
  const scoreOf = (playerId: string): number =>
    livePoints?.get(playerId) ?? session.players.find(p => p.id === playerId)?.totalScore ?? 0;
  const sorted = [...session.players].sort((a, b) => scoreOf(b.id) - scoreOf(a.id));

  const matLabels = {
    doubloonsLabel: t(`${K}.doubloons`),
    colonistsLabel: t(`${K}.colonists`),
    plantationsLabel: t(`${K}.plantations`),
    quarriesLabel: t(`${K}.quarries`),
    buildingsLabel: t(`${K}.buildings`),
    incAria: tmpl('incAria', '{field} +1'),
    decAria: tmpl('decAria', '{field} -1'),
  };

  return (
    <section
      aria-label={t(`${K}.panelAriaLabel`)}
      data-slot="pr-flavor-live"
      className={`flex flex-col gap-4 ${className ?? ''}`.trim()}
    >
      {/* Leaderboard (ungated — from scoring) */}
      <div data-slot="pr-leaderboard" className="flex flex-col gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {t(`${K}.leaderboardHeading`)}
        </h3>
        <ul role="list" className="flex flex-col gap-1">
          {sorted.map((player, idx) => (
            <li
              key={player.id}
              data-slot="pr-leaderboard-row"
              className={[
                'flex items-center gap-2 rounded-lg px-2 py-1.5',
                idx === 0
                  ? 'border border-entity-session/40 bg-entity-session/10'
                  : 'border border-transparent bg-card',
              ].join(' ')}
            >
              <span className="min-w-0 flex-1 truncate text-xs font-medium text-foreground">
                {player.displayName}
                {idx === 0 && <span aria-hidden="true"> 🏆</span>}
              </span>
              <span className="shrink-0 tabular-nums text-sm font-bold text-foreground">
                {scoreOf(player.id)}
              </span>
            </li>
          ))}
        </ul>
      </div>

      {state != null ? (
        <>
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
            <PuertoRicoGalleonsPanel
              galleons={state.galleons}
              editable={isHost}
              onSetGood={editor.setGalleonGood}
              onBumpLoaded={editor.bumpGalleonLoaded}
              labels={{
                heading: t(`${K}.galleonsHeading`),
                emptyGood: t(`${K}.emptyGood`),
                goodAria: tmpl('goodAria', 'Ship {n} good'),
                loadedAria: tmpl('loadAria', 'Load ship {n}'),
                unloadAria: tmpl('unloadAria', 'Unload ship {n}'),
                capTemplate: tmpl('capTemplate', '{loaded}/{cap}'),
              }}
            />
            <PuertoRicoTradingHousePanel
              slots={state.tradingHouse.slots}
              editable={isHost}
              onSetSlot={editor.setTradingSlot}
              labels={{
                heading: t(`${K}.tradingHeading`),
                emptyGood: t(`${K}.emptyGood`),
                slotAria: tmpl('slotAria', 'Slot {n}'),
              }}
            />
            <PuertoRicoColonistShipPanel
              colonistShip={state.colonistShip}
              editable={isHost}
              onBump={editor.bumpColonistShip}
              labels={{
                heading: t(`${K}.colonistShipHeading`),
                onShipLabel: t(`${K}.onShip`),
                supplyLabel: t(`${K}.supply`),
                incAria: tmpl('incAria', '{field} +1'),
                decAria: tmpl('decAria', '{field} -1'),
              }}
            />
          </div>

          <div data-slot="pr-players" className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            {session.players.map(player => (
              <PuertoRicoPlayerMatSummary
                key={player.id}
                player={player}
                state={state.players[player.id] ?? emptyPuertoRicoPlayerState()}
                editable={isHost}
                onBumpCounter={(field, delta) => editor.bumpPlayerCounter(player.id, field, delta)}
                onBumpGood={(good, delta) => editor.bumpPlayerGood(player.id, good, delta)}
                labels={matLabels}
              />
            ))}
          </div>

          {isHost && (
            <button
              type="button"
              onClick={editor.initializeState}
              className="self-start text-xs text-muted-foreground underline hover:text-foreground"
            >
              {t(`${K}.resetCta`)}
            </button>
          )}
        </>
      ) : isHost ? (
        <button
          type="button"
          data-slot="pr-init"
          onClick={editor.initializeState}
          className="self-start rounded-lg border border-entity-session/40 bg-entity-session/10 px-3 py-2 text-sm font-semibold text-entity-session hover:bg-entity-session/20"
        >
          {t(`${K}.initBoardCta`)}
        </button>
      ) : (
        <p role="status" aria-live="polite" className="text-xs text-muted-foreground">
          {t(`${K}.viewerWaiting`)}
        </p>
      )}
    </section>
  );
}

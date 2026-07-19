'use client';

import { type ReactElement } from 'react';

import { useIntl } from 'react-intl';

import { useTranslation } from '@/hooks/useTranslation';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import { hasRequiredRole, type ParticipantRole } from '@/lib/session-live/participant-role';

import { PowerGridPlantMarketPanel } from './PowerGridPlantMarketPanel';
import { PowerGridResourceMarketPanel } from './PowerGridResourceMarketPanel';
import { usePowerGridStateEditor } from './use-power-grid-state-editor';

export interface PowerGridLiveFlavorProps {
  readonly session: LiveSessionDto;
  readonly viewerRole: ParticipantRole;
  readonly sessionId: string;
  readonly className?: string;
  readonly livePoints?: ReadonlyMap<string, number> | null;
  readonly phaseName?: string | null;
}

const K = 'pages.sessionLive.flavor.power-grid';

export function PowerGridLiveFlavor({
  session,
  viewerRole,
  sessionId,
  className,
  livePoints,
}: PowerGridLiveFlavorProps): ReactElement {
  const { t } = useTranslation();
  const intl = useIntl();
  const isHost = hasRequiredRole(viewerRole, 'Host');
  const editor = usePowerGridStateEditor(sessionId);
  const state = editor.state;

  const tmpl = (id: string, fallback: string) =>
    (intl.messages[`${K}.${id}`] as string) ?? fallback;
  const scoreOf = (playerId: string): number =>
    livePoints?.get(playerId) ?? session.players.find(p => p.id === playerId)?.totalScore ?? 0;
  const sorted = [...session.players].sort((a, b) => scoreOf(b.id) - scoreOf(a.id));

  return (
    <section
      aria-label={t(`${K}.panelAriaLabel`)}
      data-slot="pg-flavor-live"
      className={`flex flex-col gap-4 ${className ?? ''}`.trim()}
    >
      {/* Leaderboard (ungated — from scoring) */}
      <div data-slot="pg-leaderboard" className="flex flex-col gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {t(`${K}.leaderboardHeading`)}
        </h3>
        <ul role="list" className="flex flex-col gap-1">
          {sorted.map((player, idx) => (
            <li
              key={player.id}
              data-slot="pg-leaderboard-row"
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
          <PowerGridPlantMarketPanel
            plants={state.plants}
            editable={isHost}
            onSetPlant={editor.setPlant}
            labels={{
              heading: t(`${K}.plantsHeading`),
              currentBank: t(`${K}.currentBank`),
              futureBank: t(`${K}.futureBank`),
              emptySlot: t(`${K}.emptySlot`),
              slotAria: tmpl('slotAria', '{bank} slot {n}'),
            }}
          />
          <PowerGridResourceMarketPanel
            resources={state.resources}
            editable={isHost}
            onBump={editor.bumpResource}
            labels={{
              heading: t(`${K}.resourcesHeading`),
              coal: t(`${K}.coal`),
              oil: t(`${K}.oil`),
              garbage: t(`${K}.garbage`),
              uranium: t(`${K}.uranium`),
              incAria: tmpl('incAria', '{field} +1'),
              decAria: tmpl('decAria', '{field} -1'),
            }}
          />
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
          data-slot="pg-init"
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

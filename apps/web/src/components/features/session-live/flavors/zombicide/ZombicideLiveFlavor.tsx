'use client';

import { type ReactElement } from 'react';

import { useIntl } from 'react-intl';

import { useTranslation } from '@/hooks/useTranslation';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import { hasRequiredRole, type ParticipantRole } from '@/lib/session-live/participant-role';

import { useZombicideStateEditor } from './use-zombicide-state-editor';
import { ZombicideSurvivorsPanel } from './ZombicideSurvivorsPanel';
import { ZombieHordePanel } from './ZombieHordePanel';

export interface ZombicideLiveFlavorProps {
  readonly session: LiveSessionDto;
  readonly viewerRole: ParticipantRole;
  readonly sessionId: string;
  readonly className?: string;
  readonly livePoints?: ReadonlyMap<string, number> | null;
  readonly phaseName?: string | null;
}

const K = 'pages.sessionLive.flavor.zombicide';

export function ZombicideLiveFlavor({
  session,
  viewerRole,
  sessionId,
  className,
  livePoints,
}: ZombicideLiveFlavorProps): ReactElement {
  const { t } = useTranslation();
  const intl = useIntl();
  const isHost = hasRequiredRole(viewerRole, 'Host');
  const playerIds = session.players.map(p => p.id);
  const editor = useZombicideStateEditor(sessionId, playerIds);
  const state = editor.state;

  const tmpl = (id: string, fallback: string) =>
    (intl.messages[`${K}.${id}`] as string) ?? fallback;
  const scoreOf = (playerId: string): number =>
    livePoints?.get(playerId) ?? session.players.find(p => p.id === playerId)?.totalScore ?? 0;
  const sorted = [...session.players].sort((a, b) => scoreOf(b.id) - scoreOf(a.id));

  return (
    <section
      aria-label={t(`${K}.panelAriaLabel`)}
      data-slot="zc-flavor-live"
      className={`flex flex-col gap-4 ${className ?? ''}`.trim()}
    >
      {/* Leaderboard (ungated — from scoring) */}
      <div data-slot="zc-leaderboard" className="flex flex-col gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {t(`${K}.leaderboardHeading`)}
        </h3>
        <ul role="list" className="flex flex-col gap-1">
          {sorted.map((player, idx) => (
            <li
              key={player.id}
              data-slot="zc-leaderboard-row"
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
          <ZombieHordePanel
            zombies={state.zombies}
            editable={isHost}
            onBump={editor.bumpZombie}
            labels={{
              heading: t(`${K}.hordeHeading`),
              walker: t(`${K}.walker`),
              runner: t(`${K}.runner`),
              fatty: t(`${K}.fatty`),
              berserker: t(`${K}.berserker`),
              abomination: t(`${K}.abomination`),
              necromancer: t(`${K}.necromancer`),
              incAria: tmpl('incAria', '{field} +1'),
              decAria: tmpl('decAria', '{field} -1'),
            }}
          />
          <ZombicideSurvivorsPanel
            players={session.players}
            survivors={state.survivors}
            editable={isHost}
            onCycle={editor.cycleWound}
            labels={{
              heading: t(`${K}.survivorsHeading`),
              healthy: t(`${K}.healthy`),
              wounded: t(`${K}.wounded`),
              down: t(`${K}.down`),
              cycleAria: tmpl('cycleAria', '{name}: change wounds'),
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
          data-slot="zc-init"
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

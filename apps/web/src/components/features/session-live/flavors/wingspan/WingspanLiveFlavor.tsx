'use client';

import { type ReactElement } from 'react';

import { useIntl } from 'react-intl';

import { useTranslation } from '@/hooks/useTranslation';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import { hasRequiredRole, type ParticipantRole } from '@/lib/session-live/participant-role';

import { useWingspanStateEditor } from './use-wingspan-state-editor';
import { WINGSPAN_CATEGORIES } from './wingspan-state';
import { WingspanCategoryBreakdown } from './WingspanCategoryBreakdown';
import { WingspanRoundTracker } from './WingspanRoundTracker';

export interface WingspanLiveFlavorProps {
  readonly session: LiveSessionDto;
  readonly viewerRole: ParticipantRole;
  readonly sessionId: string;
  readonly className?: string;
  readonly livePoints?: ReadonlyMap<string, number> | null;
  readonly phaseName?: string | null;
}

const K = 'pages.sessionLive.flavor.wingspan';

export function WingspanLiveFlavor({
  session,
  viewerRole,
  sessionId,
  className,
  livePoints,
}: WingspanLiveFlavorProps): ReactElement {
  const { t } = useTranslation();
  const intl = useIntl();
  const isHost = hasRequiredRole(viewerRole, 'Host');
  const editor = useWingspanStateEditor(sessionId);
  const state = editor.state;

  const tmpl = (id: string, fallback: string) =>
    (intl.messages[`${K}.${id}`] as string) ?? fallback;

  const categoryLabels: Record<string, string> = Object.fromEntries(
    WINGSPAN_CATEGORIES.map(c => [c.id, t(`${K}.category.${c.id}`)])
  );

  const scoreOf = (playerId: string): number =>
    livePoints?.get(playerId) ?? session.players.find(p => p.id === playerId)?.totalScore ?? 0;
  const sorted = [...session.players].sort((a, b) => scoreOf(b.id) - scoreOf(a.id));

  return (
    <section
      aria-label={t(`${K}.panelAriaLabel`)}
      data-slot="wingspan-flavor-live"
      className={`flex flex-col gap-4 ${className ?? ''}`.trim()}
    >
      {/* Leaderboard (always rendered — from scoring, not gameState) */}
      <div data-slot="wingspan-leaderboard" className="flex flex-col gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {t(`${K}.leaderboardHeading`)}
        </h3>
        <ul role="list" className="flex flex-col gap-1">
          {sorted.map((player, idx) => (
            <li
              key={player.id}
              data-slot="wingspan-leaderboard-row"
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
              <span
                aria-label={tmpl('scoreAriaTemplate', '{name}: {score}')
                  .replace('{name}', player.displayName)
                  .replace('{score}', String(scoreOf(player.id)))}
                className="shrink-0 tabular-nums text-sm font-bold text-foreground"
              >
                {scoreOf(player.id)}
              </span>
            </li>
          ))}
        </ul>
      </div>

      {/* Category breakdown (always rendered — from roundScores) */}
      <WingspanCategoryBreakdown
        players={session.players}
        roundScores={session.roundScores}
        categoryLabels={categoryLabels}
        heading={t(`${K}.categoriesHeading`)}
      />

      {/* Round tracker (gameState-gated) */}
      {state != null ? (
        <WingspanRoundTracker
          state={state}
          editable={isHost}
          onAdvanceRound={editor.advanceRound}
          onSetRoundGoal={editor.setRoundGoal}
          labels={{
            heading: t(`${K}.roundHeading`),
            roundTemplate: tmpl('roundTemplate', 'Round {n}/4'),
            turnBudgetTemplate: tmpl('turnBudgetTemplate', '{n} turni'),
            goalsHeading: t(`${K}.goalsHeading`),
            goalPlaceholderTemplate: tmpl('goalPlaceholderTemplate', 'Obiettivo round {n}'),
            advanceRoundLabel: t(`${K}.advanceRoundLabel`),
          }}
        />
      ) : isHost ? (
        <button
          type="button"
          data-slot="wingspan-round-init"
          onClick={editor.initializeState}
          className="self-start rounded-lg border border-entity-session/40 bg-entity-session/10 px-3 py-2 text-sm font-semibold text-entity-session hover:bg-entity-session/20"
        >
          {t(`${K}.initRoundCta`)}
        </button>
      ) : (
        <p role="status" aria-live="polite" className="text-xs text-muted-foreground">
          {t(`${K}.viewerWaiting`)}
        </p>
      )}
    </section>
  );
}

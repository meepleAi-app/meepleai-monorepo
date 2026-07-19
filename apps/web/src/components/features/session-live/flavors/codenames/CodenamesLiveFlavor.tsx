'use client';

import { type ReactElement, useState } from 'react';

import { useIntl } from 'react-intl';

import { useTranslation } from '@/hooks/useTranslation';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import { hasRequiredRole, type ParticipantRole } from '@/lib/session-live/participant-role';

import { codenamesWinner } from './codenames-state';
import { CodenamesCurrentClueStrip } from './CodenamesCurrentClueStrip';
import { CodenamesTeamTracker } from './CodenamesTeamTracker';
import { CodenamesWordGrid } from './CodenamesWordGrid';
import { useCodenamesStateEditor } from './use-codenames-state-editor';

export interface CodenamesLiveFlavorProps {
  readonly session: LiveSessionDto;
  readonly viewerRole: ParticipantRole;
  readonly sessionId: string;
  readonly className?: string;
  readonly livePoints?: ReadonlyMap<string, number> | null;
  readonly phaseName?: string | null;
}

const K = 'pages.sessionLive.flavor.codenames';

export function CodenamesLiveFlavor({
  session,
  viewerRole,
  sessionId,
  className,
  livePoints,
}: CodenamesLiveFlavorProps): ReactElement {
  const { t } = useTranslation();
  const intl = useIntl();
  const isHost = hasRequiredRole(viewerRole, 'Host');
  const editor = useCodenamesStateEditor(sessionId);
  const state = editor.state;
  const [spymaster, setSpymaster] = useState(false);

  const tmpl = (id: string, fallback: string) =>
    (intl.messages[`${K}.${id}`] as string) ?? fallback;
  const scoreOf = (playerId: string): number =>
    livePoints?.get(playerId) ?? session.players.find(p => p.id === playerId)?.totalScore ?? 0;
  const sorted = [...session.players].sort((a, b) => scoreOf(b.id) - scoreOf(a.id));
  const winner = state != null ? codenamesWinner(state) : null;

  return (
    <section
      aria-label={t(`${K}.panelAriaLabel`)}
      data-slot="codenames-flavor-live"
      className={`flex flex-col gap-4 ${className ?? ''}`.trim()}
    >
      {/* Leaderboard (ungated — from scoring) */}
      <div data-slot="codenames-leaderboard" className="flex flex-col gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {t(`${K}.leaderboardHeading`)}
        </h3>
        <ul role="list" className="flex flex-col gap-1">
          {sorted.map((player, idx) => (
            <li
              key={player.id}
              data-slot="codenames-leaderboard-row"
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
          {winner != null && (
            <p
              role="status"
              data-slot="codenames-gameover"
              className="rounded-lg bg-muted px-3 py-2 text-sm font-semibold text-foreground"
            >
              {tmpl('winnerTemplate', '{team} wins').replace(
                '{team}',
                winner === 'red' ? t(`${K}.redLabel`) : t(`${K}.blueLabel`)
              )}
            </p>
          )}

          <CodenamesTeamTracker
            board={state.board}
            currentTeam={state.currentTeam}
            labels={{
              redLabel: t(`${K}.redLabel`),
              blueLabel: t(`${K}.blueLabel`),
              foundTemplate: tmpl('foundTemplate', '{found}/{total}'),
              turnLabel: t(`${K}.turnLabel`),
            }}
          />

          {isHost && (
            <button
              type="button"
              onClick={() => setSpymaster(s => !s)}
              aria-pressed={spymaster}
              className="self-start rounded-md border border-border bg-background px-2 py-1 text-xs font-semibold text-foreground hover:bg-muted"
            >
              {spymaster ? t(`${K}.perspectiveSpymaster`) : t(`${K}.perspectiveOperative`)}
            </button>
          )}

          <CodenamesWordGrid
            board={state.board}
            editable={isHost}
            perspective={isHost && spymaster ? 'spymaster' : 'operative'}
            onRevealCell={editor.revealCell}
            revealAriaTemplate={tmpl('revealAriaTemplate', 'Reveal {word}')}
          />

          <CodenamesCurrentClueStrip
            clue={state.clue}
            currentTeam={state.currentTeam}
            editable={isHost}
            onSetClue={editor.setClue}
            onClearClue={editor.clearClue}
            onSwitchTeam={editor.switchTeam}
            labels={{
              noClue: t(`${K}.noClue`),
              wordPlaceholder: t(`${K}.cluePlaceholder`),
              numberAria: t(`${K}.clueNumberAria`),
              giveClue: t(`${K}.giveClue`),
              endTurn: t(`${K}.endTurn`),
            }}
          />

          {isHost && (
            <button
              type="button"
              onClick={editor.regenerateBoard}
              className="self-start text-xs text-muted-foreground underline hover:text-foreground"
            >
              {t(`${K}.regenerate`)}
            </button>
          )}
        </>
      ) : isHost ? (
        <button
          type="button"
          data-slot="codenames-init"
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

/**
 * PlayerRosterLive — Wave D.2 Foundation sub-PR (Issue #746).
 *
 * Displays live participant list with online status and role badges.
 * Foundation: kick button aria-disabled (Interactions sub-PR wires handler).
 *
 * Gate C: DIVERGES from MeepleCard — live roster, not a card list.
 */

import type { ReactElement } from 'react';

import type { ParticipantRole } from '@/lib/session-live/participant-role';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface LivePlayerEntry {
  readonly id: string;
  readonly name: string;
  readonly role: ParticipantRole;
  readonly score: number;
  readonly isOnline: boolean;
}

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface PlayerRosterLiveLabels {
  readonly title: string;
  /** Pre-resolved ICU plural count from orchestrator (Gate A). */
  readonly playerCountResolved: string;
  readonly onlineLabel: string;
  readonly offlineLabel: string;
  /** Raw template "{playerName}" — component does .replace(). */
  readonly kickAriaLabelTemplate: string;
  readonly roleSpectator: string;
  readonly rolePlayer: string;
  readonly roleHost: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface PlayerRosterLiveProps {
  readonly players: ReadonlyArray<LivePlayerEntry>;
  readonly viewerId: string;
  readonly viewerRole: ParticipantRole;
  readonly onKickParticipant?: (participantId: string) => void;
  readonly compact?: boolean;
  readonly labels: PlayerRosterLiveLabels;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function roleLabel(role: ParticipantRole, labels: PlayerRosterLiveLabels): string {
  if (role === 'Host') return labels.roleHost;
  if (role === 'Spectator') return labels.roleSpectator;
  return labels.rolePlayer;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function PlayerRosterLive({
  players,
  viewerId,
  viewerRole,
  onKickParticipant,
  compact = false,
  labels,
}: PlayerRosterLiveProps): ReactElement {
  const isHost = viewerRole === 'Host';

  return (
    <div data-slot="player-roster-live" className="flex flex-col gap-2">
      {!compact && (
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold text-slate-200">{labels.title}</h3>
          <span className="text-xs text-slate-400">{labels.playerCountResolved}</span>
        </div>
      )}

      <ul role="list" className="flex flex-col gap-1.5" aria-label={labels.title}>
        {players.map(player => {
          const isViewer = player.id === viewerId;
          const kickAriaLabel = labels.kickAriaLabelTemplate.replace('{playerName}', player.name);

          return (
            <li
              key={player.id}
              data-slot="player-roster-entry"
              data-player-id={player.id}
              className={[
                'flex items-center justify-between gap-2 rounded-lg px-2 py-1.5',
                isViewer ? 'bg-[hsl(240,60%,20%)]' : 'bg-slate-800/50',
              ].join(' ')}
            >
              {/* Left: online dot + name */}
              <div className="flex min-w-0 items-center gap-2">
                <span
                  title={player.isOnline ? labels.onlineLabel : labels.offlineLabel}
                  aria-label={player.isOnline ? labels.onlineLabel : labels.offlineLabel}
                  className={[
                    'h-2 w-2 shrink-0 rounded-full',
                    player.isOnline ? 'bg-emerald-400' : 'bg-slate-600',
                  ].join(' ')}
                />
                <span className="truncate text-xs font-medium text-slate-200">{player.name}</span>
                <span className="shrink-0 text-xs text-slate-500">
                  {roleLabel(player.role, labels)}
                </span>
              </div>

              {/* Right: score + kick */}
              <div className="flex shrink-0 items-center gap-2">
                <span className="tabular-nums text-xs font-semibold text-slate-200">
                  {player.score}
                </span>
                {/* Host-only: kick button (own player excluded) */}
                {isHost && !isViewer && (
                  <button
                    type="button"
                    data-slot="player-roster-kick"
                    aria-label={kickAriaLabel}
                    aria-disabled={onKickParticipant == null ? 'true' : undefined}
                    onClick={() => onKickParticipant?.(player.id)}
                    className="rounded p-0.5 text-rose-500 hover:bg-rose-500/10
                      focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-rose-500
                      aria-disabled:cursor-not-allowed aria-disabled:opacity-40"
                  >
                    <svg
                      xmlns="http://www.w3.org/2000/svg"
                      width="12"
                      height="12"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="2"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      aria-hidden="true"
                    >
                      <path d="M18 6 6 18" />
                      <path d="m6 6 12 12" />
                    </svg>
                  </button>
                )}
              </div>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

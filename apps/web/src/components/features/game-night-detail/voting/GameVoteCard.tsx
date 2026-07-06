'use client';

import { Check, Trophy } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';

export interface GameVoteCardProps {
  readonly gameId: string;
  readonly title: string;
  readonly voteCount: number;
  readonly maxVoteCount: number;
  readonly votedByMe: boolean;
  readonly isLeader: boolean;
  readonly isWinner: boolean;
  readonly disabled: boolean;
  readonly pending: boolean;
  readonly onToggle: (gameId: string, votedByMe: boolean) => void;
}

/**
 * A single candidate game in the approval-voting panel (#2700): title, an approval-count
 * bar, a leader/winner badge and a toggle that casts or retracts the caller's approval.
 */
export function GameVoteCard({
  gameId,
  title,
  voteCount,
  maxVoteCount,
  votedByMe,
  isLeader,
  isWinner,
  disabled,
  pending,
  onToggle,
}: GameVoteCardProps): React.JSX.Element {
  const { t } = useTranslation();
  const barPct = maxVoteCount > 0 ? Math.round((voteCount / maxVoteCount) * 100) : 0;

  return (
    <div
      data-testid={`vote-card-${gameId}`}
      className="rounded-lg border border-border bg-card p-4"
    >
      <div className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <span className="font-medium text-foreground">{title}</span>
          {isWinner ? (
            <span
              data-testid={`winner-badge-${gameId}`}
              className="inline-flex items-center gap-1 rounded-full bg-entity-game px-2 py-0.5 text-xs font-semibold text-white"
            >
              <Trophy className="h-3 w-3" />
              {t('gameNightDetail.voting.winnerBadge')}
            </span>
          ) : (
            isLeader && (
              <span
                data-testid={`leader-badge-${gameId}`}
                className="rounded-full bg-muted px-2 py-0.5 text-xs font-semibold text-muted-foreground"
              >
                {t('gameNightDetail.voting.leaderBadge')}
              </span>
            )
          )}
        </div>
        <span className="text-sm text-muted-foreground" data-testid={`vote-count-${gameId}`}>
          {t('gameNightDetail.voting.votesCount', { count: voteCount })}
        </span>
      </div>

      <div className="mt-2 h-2 w-full overflow-hidden rounded-full bg-muted" aria-hidden>
        <div
          className="h-full rounded-full bg-entity-event transition-all"
          style={{ width: `${barPct}%` }}
        />
      </div>

      <div className="mt-3 flex justify-end">
        <Button
          type="button"
          size="sm"
          variant={votedByMe ? 'secondary' : 'default'}
          disabled={disabled || pending}
          onClick={() => onToggle(gameId, votedByMe)}
          data-testid={`vote-toggle-${gameId}`}
        >
          {votedByMe && <Check className="mr-1 h-4 w-4" />}
          {votedByMe ? t('gameNightDetail.voting.retract') : t('gameNightDetail.voting.vote')}
        </Button>
      </div>
    </div>
  );
}

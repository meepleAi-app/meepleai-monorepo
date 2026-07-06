'use client';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  useCastGameNightVote,
  useGameNightVoteTally,
  useResolveGameNightVotingTie,
  useRetractGameNightVote,
} from '@/hooks/queries/useGameNights';
import { useToast } from '@/hooks/useToast';
import { useTranslation } from '@/hooks/useTranslation';

import { GameVoteCard } from './GameVoteCard';
import { VotingTiedResolver } from './VotingTiedResolver';

export interface VotingPanelProps {
  readonly gameNightId: string;
  readonly isOrganizer: boolean;
  /** gameId → display title (from the shared-game catalog); falls back to a generic label. */
  readonly gameTitleById?: Record<string, string>;
}

/**
 * Candidate approval-voting panel for a game night (#2700). Confirmed participants approve
 * candidate games; the organiser resolves a tie once voting closes. Renders the 4 canonical
 * states: loading / empty (no candidates) / error / default.
 */
export function VotingPanel({
  gameNightId,
  isOrganizer,
  gameTitleById = {},
}: VotingPanelProps): React.JSX.Element {
  const { t } = useTranslation();
  const { toast } = useToast();

  const tallyQuery = useGameNightVoteTally(gameNightId);
  const castMutation = useCastGameNightVote();
  const retractMutation = useRetractGameNightVote();
  const resolveMutation = useResolveGameNightVotingTie();

  const title = (gameId: string): string =>
    gameTitleById[gameId] ?? t('gameNightDetail.voting.candidateFallback');

  function onError(): void {
    toast({
      title: t('common.error'),
      description: t('gameNightDetail.voting.actionError'),
      variant: 'destructive',
    });
  }

  function handleToggle(gameId: string, votedByMe: boolean): void {
    const mutation = votedByMe ? retractMutation : castMutation;
    mutation.mutate({ id: gameNightId, candidateGameId: gameId }, { onError });
  }

  function handleResolve(gameId: string): void {
    resolveMutation.mutate({ id: gameNightId, winningCandidateGameId: gameId }, { onError });
  }

  return (
    <section aria-labelledby="voting-heading" className="space-y-3" data-testid="voting-panel">
      <div className="flex items-center justify-between">
        <h2 id="voting-heading" className="font-display text-base font-extrabold text-foreground">
          {t('gameNightDetail.voting.title')}
        </h2>
        {tallyQuery.data?.isVotingClosed && (
          <span className="text-xs font-semibold uppercase text-muted-foreground">
            {t('gameNightDetail.voting.votingClosed')}
          </span>
        )}
      </div>

      {tallyQuery.isLoading ? (
        <div className="space-y-2" data-testid="voting-loading">
          <Skeleton className="h-20 w-full" />
          <Skeleton className="h-20 w-full" />
        </div>
      ) : tallyQuery.isError ? (
        <p className="text-sm text-muted-foreground" data-testid="voting-error">
          {t('gameNightDetail.voting.error')}
        </p>
      ) : !tallyQuery.data || tallyQuery.data.candidates.length === 0 ? (
        <div className="rounded-lg border border-border p-6 text-center" data-testid="voting-empty">
          <p className="font-medium text-foreground">{t('gameNightDetail.voting.empty.title')}</p>
          <p className="mt-1 text-sm text-muted-foreground">
            {t('gameNightDetail.voting.empty.body')}
          </p>
        </div>
      ) : (
        <VotingContent
          data={tallyQuery.data}
          gameNightId={gameNightId}
          isOrganizer={isOrganizer}
          title={title}
          pendingCastId={
            castMutation.isPending ? castMutation.variables?.candidateGameId : undefined
          }
          pendingRetractId={
            retractMutation.isPending ? retractMutation.variables?.candidateGameId : undefined
          }
          resolving={resolveMutation.isPending}
          onToggle={handleToggle}
          onResolve={handleResolve}
        />
      )}
    </section>
  );
}

interface VotingContentProps {
  readonly data: NonNullable<ReturnType<typeof useGameNightVoteTally>['data']>;
  readonly gameNightId: string;
  readonly isOrganizer: boolean;
  readonly title: (gameId: string) => string;
  readonly pendingCastId?: string;
  readonly pendingRetractId?: string;
  readonly resolving: boolean;
  readonly onToggle: (gameId: string, votedByMe: boolean) => void;
  readonly onResolve: (gameId: string) => void;
}

function VotingContent({
  data,
  isOrganizer,
  title,
  pendingCastId,
  pendingRetractId,
  resolving,
  onToggle,
  onResolve,
}: VotingContentProps): React.JSX.Element {
  const maxVoteCount = Math.max(0, ...data.candidates.map(c => c.voteCount));
  const showResolver = isOrganizer && data.isTie && data.isVotingClosed;

  return (
    <>
      <ul className="space-y-2">
        {data.candidates.map(candidate => (
          <li key={candidate.gameId}>
            <GameVoteCard
              gameId={candidate.gameId}
              title={title(candidate.gameId)}
              voteCount={candidate.voteCount}
              maxVoteCount={maxVoteCount}
              votedByMe={candidate.votedByMe}
              isLeader={data.leadingCandidateGameIds.includes(candidate.gameId)}
              isWinner={data.winnerGameId === candidate.gameId}
              disabled={data.isVotingClosed}
              pending={pendingCastId === candidate.gameId || pendingRetractId === candidate.gameId}
              onToggle={onToggle}
            />
          </li>
        ))}
      </ul>

      {showResolver && (
        <VotingTiedResolver
          tiedCandidates={data.leadingCandidateGameIds.map(gameId => ({
            gameId,
            title: title(gameId),
          }))}
          pending={resolving}
          onResolve={onResolve}
        />
      )}
    </>
  );
}

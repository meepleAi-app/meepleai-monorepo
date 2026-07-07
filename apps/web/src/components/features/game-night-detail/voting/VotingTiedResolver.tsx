'use client';

import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';

export interface VotingTiedResolverProps {
  readonly tiedCandidates: ReadonlyArray<{ gameId: string; title: string }>;
  readonly pending: boolean;
  readonly onResolve: (gameId: string) => void;
}

/**
 * Shown to the organiser when voting has closed on a tie (#2700). The host picks the
 * winning game among the tied leaders (mockup Frame06 "pareggio — host sceglie a -1h").
 */
export function VotingTiedResolver({
  tiedCandidates,
  pending,
  onResolve,
}: VotingTiedResolverProps): React.JSX.Element {
  const { t } = useTranslation();

  return (
    <div
      data-testid="voting-tied-resolver"
      className="rounded-lg border border-border-strong bg-muted/40 p-4"
    >
      <h4 className="font-display text-sm font-extrabold text-foreground">
        {t('gameNightDetail.voting.tie.title')}
      </h4>
      <p className="mt-1 text-sm text-muted-foreground">{t('gameNightDetail.voting.tie.body')}</p>

      <div className="mt-3 flex flex-wrap gap-2">
        {tiedCandidates.map(candidate => (
          <Button
            key={candidate.gameId}
            type="button"
            size="sm"
            variant="outline"
            disabled={pending}
            onClick={() => onResolve(candidate.gameId)}
            data-testid={`resolve-tie-${candidate.gameId}`}
          >
            {t('gameNightDetail.voting.tie.resolve', { title: candidate.title })}
          </Button>
        ))}
      </div>
    </div>
  );
}

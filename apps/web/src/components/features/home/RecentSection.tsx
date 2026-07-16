import { SkeletonCardGrid } from '@/components/features/common';
import { MeepleEventCard } from '@/components/game-night/MeepleEventCard';
import { useCompletedGameNights } from '@/hooks/queries/useGameNights';

export interface RecentSectionProps {
  onOpenDetail: (id: string) => void;
  onSeeAll: () => void;
}

/**
 * Sezione "Recenti" della dashboard (invariante #4): serate completate in ordine
 * discendente, sotto "Prossimi". Gap-fill mobile — la dashboard non aveva una
 * vista dei completati (esisteva solo /game-nights?filter=completed).
 */
export function RecentSection({ onOpenDetail, onSeeAll }: RecentSectionProps) {
  const { data, isLoading } = useCompletedGameNights({ limit: 5 });
  const nights = data ?? [];

  return (
    <section data-testid="recent-section">
      <div className="mb-3 flex items-center gap-3">
        <span className="font-mono text-xs font-bold uppercase tracking-wider text-muted-foreground">
          Recenti
        </span>
        <span className="h-px flex-1 bg-border" />
      </div>
      {isLoading ? (
        <div data-testid="recent-section-skeleton">
          <SkeletonCardGrid count={2} />
        </div>
      ) : nights.length === 0 ? (
        <div className="rounded-lg border border-border bg-card p-6 text-center">
          <p className="text-sm text-muted-foreground">Nessuna partita ancora</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-3">
          {nights.map(night => (
            <MeepleEventCard
              key={night.id}
              event={{
                id: night.id,
                title: night.title,
                scheduledAt: night.scheduledAt,
                location: night.location ?? null,
                participantCount: 0,
                gameCount: 0,
              }}
              variant="list"
              onClick={() => onOpenDetail(night.id)}
            />
          ))}
          <button
            type="button"
            onClick={onSeeAll}
            className="min-h-11 rounded-md border border-dashed border-border-strong font-quicksand font-bold text-sm text-muted-foreground transition-colors motion-reduce:transition-none"
          >
            Vedi tutte le completate →
          </button>
        </div>
      )}
    </section>
  );
}

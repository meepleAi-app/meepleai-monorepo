'use client';

import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';

import { Badge } from '@/components/ui/badge';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { api } from '@/lib/api';

const statusColors: Record<string, string> = {
  Published: 'bg-green-100 text-green-900 dark:bg-green-900/30 dark:text-green-300',
  Draft: 'bg-amber-100 text-amber-900 dark:bg-amber-900/30 dark:text-amber-300',
  Archived: 'bg-gray-100 text-gray-900 dark:bg-gray-900/30 dark:text-gray-300',
  PendingApproval: 'bg-blue-100 text-blue-900 dark:bg-blue-900/30 dark:text-blue-300',
};

function StatCardSkeleton() {
  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-slate-200/50 dark:border-zinc-700/50">
      <Skeleton className="h-4 w-24 mb-2" />
      <Skeleton className="h-8 w-12" />
    </div>
  );
}

export function GameCatalogGrid() {
  const router = useRouter();

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'shared-games'],
    queryFn: () => api.sharedGames.getAll({ pageSize: 100 }),
  });

  const games = data?.items ?? [];
  const total = data?.total ?? 0;
  const published = games.filter((g) => g.status === 'Published').length;
  const draft = games.filter((g) => g.status === 'Draft').length;

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <StatCardSkeleton />
          <StatCardSkeleton />
          <StatCardSkeleton />
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-64 rounded-2xl" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Stats Summary */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-slate-200/50 dark:border-zinc-700/50">
          <div className="text-sm text-slate-600 dark:text-zinc-400">Total Games</div>
          <div className="text-2xl font-bold text-slate-900 dark:text-zinc-100">{total}</div>
        </div>
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-slate-200/50 dark:border-zinc-700/50">
          <div className="text-sm text-slate-600 dark:text-zinc-400">Published</div>
          <div className="text-2xl font-bold text-green-600 dark:text-green-400">{published}</div>
        </div>
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-slate-200/50 dark:border-zinc-700/50">
          <div className="text-sm text-slate-600 dark:text-zinc-400">Draft</div>
          <div className="text-2xl font-bold text-amber-600 dark:text-amber-400">{draft}</div>
        </div>
      </div>

      {/* Game Grid */}
      {games.length === 0 ? (
        <div className="text-center py-16 text-muted-foreground">
          <p className="text-lg font-medium">No games in the catalog yet.</p>
          <p className="text-sm mt-1">Add a game to get started.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {games.map((game) => (
            <div
              key={game.id}
              className="relative cursor-pointer"
              onClick={() => router.push(`/admin/shared-games/${game.id}`)}
            >
              <MeepleCard
                entity="game"
                variant="grid"
                title={game.title}
                subtitle={`${game.yearPublished}`}
                imageUrl={game.imageUrl || undefined}
                rating={game.averageRating ?? undefined}
                ratingMax={10}
                metadata={[
                  {
                    label: 'Players',
                    value:
                      game.minPlayers === game.maxPlayers
                        ? `${game.minPlayers}`
                        : `${game.minPlayers}-${game.maxPlayers}`,
                  },
                ]}
              />
              <div className="absolute top-3 right-3">
                <Badge
                  variant="outline"
                  className={statusColors[game.status] ?? statusColors['Draft']}
                >
                  {game.status === 'PendingApproval' ? 'Pending' : game.status}
                </Badge>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

'use client';

/**
 * Golden Set — Game Picker (ADR-051 Sprint 1 / Task 36)
 *
 * Lists SharedGames so an admin can drill into the per-game golden set CRUD page
 * at `./golden/[gameId]`. Hidden behind feature flag
 * `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED === 'true'`.
 */

import { useQuery } from '@tanstack/react-query';
import { ChevronRightIcon } from 'lucide-react';
import Link from 'next/link';
import { notFound } from 'next/navigation';

import { Card, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { api } from '@/lib/api';
import { isMechanicValidationEnabled } from '@/lib/feature-flags/mechanic-validation';

export default function GoldenSetGamePickerPage() {
  if (!isMechanicValidationEnabled()) {
    notFound();
  }

  const { data, isLoading, error } = useQuery({
    queryKey: ['shared-games', 'golden-picker'],
    queryFn: () => api.sharedGames.getAll({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Golden Set Curation
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Pick a shared game to manage its curated golden claims used by the AI Comprehension
          Validation pipeline.
        </p>
      </div>

      <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/60">
        <CardContent className="pt-6">
          {isLoading && (
            <div className="space-y-2">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          )}

          {error && (
            <p className="text-sm text-destructive">
              Failed to load shared games:{' '}
              {error instanceof Error ? error.message : 'unknown error'}
            </p>
          )}

          {!isLoading && !error && (data?.items.length ?? 0) === 0 && (
            <p className="text-sm text-muted-foreground">No shared games available.</p>
          )}

          {!isLoading && !error && (data?.items.length ?? 0) > 0 && (
            <ul className="divide-y divide-slate-200 dark:divide-zinc-700">
              {data?.items.map(game => (
                <li key={game.id}>
                  <Link
                    href={`/admin/knowledge-base/mechanic-extractor/golden/${game.id}`}
                    className="flex items-center justify-between gap-3 px-2 py-3 text-sm hover:bg-slate-50 dark:hover:bg-zinc-800/40 rounded transition-colors"
                  >
                    <span className="font-medium text-foreground">{game.title}</span>
                    <ChevronRightIcon className="h-4 w-4 text-muted-foreground" />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

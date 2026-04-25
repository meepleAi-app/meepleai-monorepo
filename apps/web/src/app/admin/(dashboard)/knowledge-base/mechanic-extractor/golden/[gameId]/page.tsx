'use client';

/**
 * Golden Set — Per-game CRUD page (ADR-051 Sprint 1 / Task 36)
 *
 * Manage curated golden claims for one SharedGame.
 *  - Header shows the game name + `GoldenVersionHashBadge`.
 *  - "New claim" button opens a Dialog hosting `GoldenClaimForm` (create mode).
 *  - `GoldenClaimsList` renders existing claims with edit / deactivate affordances.
 *
 * Hidden behind feature flag `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED === 'true'`.
 */

import { use, useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { ArrowLeftIcon, ClipboardPasteIcon, PlusIcon } from 'lucide-react';
import Link from 'next/link';
import { notFound } from 'next/navigation';

import { BggImporterPasteDialog } from '@/components/admin/mechanic-extractor/validation/BggImporterPasteDialog';
import { GoldenClaimForm } from '@/components/admin/mechanic-extractor/validation/GoldenClaimForm';
import { GoldenClaimsList } from '@/components/admin/mechanic-extractor/validation/GoldenClaimsList';
import { GoldenVersionHashBadge } from '@/components/admin/mechanic-extractor/validation/GoldenVersionHashBadge';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { useGoldenForGame } from '@/hooks/admin/useGoldenForGame';
import { api } from '@/lib/api';

const FEATURE_FLAG = 'true';

interface GoldenForGamePageProps {
  params: Promise<{ gameId: string }>;
}

export default function GoldenForGamePage({ params }: GoldenForGamePageProps) {
  if (process.env.NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED !== FEATURE_FLAG) {
    notFound();
  }

  const { gameId } = use(params);
  const [createOpen, setCreateOpen] = useState(false);
  const [bggImporterOpen, setBggImporterOpen] = useState(false);

  const goldenQuery = useGoldenForGame(gameId);
  const gameQuery = useQuery({
    queryKey: ['shared-game', gameId],
    queryFn: () => api.sharedGames.getById(gameId),
    enabled: !!gameId,
    staleTime: 60_000,
  });

  const gameTitle = gameQuery.data?.title ?? 'Unknown game';
  const versionHash = goldenQuery.data?.versionHash ?? '';
  const claims = goldenQuery.data?.claims ?? [];

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-3">
        <div>
          <Link
            href="/admin/knowledge-base/mechanic-extractor/golden"
            className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground transition-colors"
          >
            <ArrowLeftIcon className="h-3.5 w-3.5" />
            Back to game picker
          </Link>
          <div className="mt-1 flex flex-wrap items-center gap-2">
            <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
              {gameQuery.isLoading ? 'Loading…' : gameTitle}
            </h1>
            {goldenQuery.isSuccess && <GoldenVersionHashBadge hash={versionHash} />}
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            Curated golden claims for the AI Comprehension Validation pipeline.
          </p>
        </div>

        <div className="flex flex-shrink-0 items-center gap-2">
          <Button
            variant="outline"
            onClick={() => setBggImporterOpen(true)}
            data-testid="bgg-importer-trigger"
          >
            <ClipboardPasteIcon className="mr-1 h-4 w-4" />
            Import BGG tags
          </Button>

          <Dialog open={createOpen} onOpenChange={setCreateOpen}>
            <DialogTrigger asChild>
              <Button>
                <PlusIcon className="mr-1 h-4 w-4" />
                New claim
              </Button>
            </DialogTrigger>
            <DialogContent className="max-w-2xl">
              <DialogHeader>
                <DialogTitle>New golden claim</DialogTitle>
              </DialogHeader>
              <GoldenClaimForm
                sharedGameId={gameId}
                mode="create"
                onClose={() => setCreateOpen(false)}
              />
            </DialogContent>
          </Dialog>
        </div>
      </div>

      <BggImporterPasteDialog
        sharedGameId={gameId}
        open={bggImporterOpen}
        onOpenChange={setBggImporterOpen}
      />

      <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/60">
        <CardContent className="pt-6">
          {goldenQuery.isLoading && (
            <div className="space-y-3">
              <Skeleton className="h-6 w-32" />
              <Skeleton className="h-32 w-full" />
            </div>
          )}

          {goldenQuery.error && (
            <p className="text-sm text-destructive">
              Failed to load golden set:{' '}
              {goldenQuery.error instanceof Error ? goldenQuery.error.message : 'unknown error'}
            </p>
          )}

          {goldenQuery.isSuccess && <GoldenClaimsList sharedGameId={gameId} claims={claims} />}
        </CardContent>
      </Card>
    </div>
  );
}

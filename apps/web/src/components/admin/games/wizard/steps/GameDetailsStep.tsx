'use client';

/**
 * Game Details Step
 * Step 2 of the Admin Game Wizard.
 * Shows a preview of the selected BGG game and allows creation.
 */

import {
  ArrowLeftIcon,
  CalendarIcon,
  UsersIcon,
  ClockIcon,
  HashIcon,
  LoaderCircleIcon,
  CheckCircle2Icon,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';

import type { BggSearchResult } from '@/lib/api/schemas/games.schemas';
import {
  useCreateGameFromWizard,
  type CreateGameFromWizardResult,
} from '@/hooks/queries/useAdminGameWizard';

// ─── Types ───────────────────────────────────────────────────────────────────

interface GameDetailsStepProps {
  selectedGame: BggSearchResult;
  onBack: () => void;
  onGameCreated: (result: CreateGameFromWizardResult) => void;
}

// ─── Component ───────────────────────────────────────────────────────────────

export function GameDetailsStep({
  selectedGame,
  onBack,
  onGameCreated,
}: GameDetailsStepProps) {
  const createGame = useCreateGameFromWizard();

  const handleCreateGame = () => {
    createGame.mutate(selectedGame.bggId, {
      onSuccess: (data) => {
        onGameCreated(data);
      },
    });
  };

  return (
    <div className="space-y-6">
      {/* Selected game card */}
      <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
        <CardHeader>
          <CardTitle className="flex items-center gap-3 text-lg">
            {selectedGame.thumbnailUrl ? (
              <img
                src={selectedGame.thumbnailUrl}
                alt={selectedGame.name}
                className="h-20 w-20 rounded-lg object-cover shrink-0 bg-slate-100 dark:bg-zinc-700"
              />
            ) : (
              <div className="h-20 w-20 rounded-lg shrink-0 bg-slate-100 dark:bg-zinc-700 flex items-center justify-center">
                <span className="text-3xl">🎲</span>
              </div>
            )}
            <div>
              <p className="font-quicksand font-bold text-foreground">
                {selectedGame.name}
              </p>
              <p className="text-sm text-muted-foreground font-normal mt-1">
                Selected from BoardGameGeek
              </p>
            </div>
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
            {selectedGame.yearPublished && (
              <div className="flex items-center gap-2">
                <CalendarIcon className="h-4 w-4 text-muted-foreground" />
                <div>
                  <p className="text-xs text-muted-foreground">Year</p>
                  <p className="text-sm font-medium">{selectedGame.yearPublished}</p>
                </div>
              </div>
            )}
            <div className="flex items-center gap-2">
              <HashIcon className="h-4 w-4 text-muted-foreground" />
              <div>
                <p className="text-xs text-muted-foreground">BGG ID</p>
                <p className="text-sm font-medium">{selectedGame.bggId}</p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-muted-foreground text-sm">Type</span>
              <p className="text-sm font-medium capitalize">{selectedGame.type}</p>
            </div>
          </div>

          {/* Info note */}
          <div className="mt-4 rounded-lg bg-amber-50 dark:bg-amber-950/20 border border-amber-200/60 dark:border-amber-800/40 p-3">
            <p className="text-sm text-amber-700 dark:text-amber-400">
              Creating this game will import all available data from BGG including description,
              player counts, play time, categories, mechanics, designers, and publishers.
            </p>
          </div>
        </CardContent>
      </Card>

      {/* Actions */}
      <div className="flex items-center justify-between">
        <Button
          variant="ghost"
          onClick={onBack}
          disabled={createGame.isPending}
        >
          <ArrowLeftIcon className="h-4 w-4 mr-2" />
          Back to Search
        </Button>

        <Button
          onClick={handleCreateGame}
          disabled={createGame.isPending || createGame.isSuccess}
          className="bg-gradient-to-r from-amber-500 to-orange-600 hover:from-amber-600 hover:to-orange-700 text-white"
        >
          {createGame.isPending ? (
            <>
              <LoaderCircleIcon className="h-4 w-4 mr-2 animate-spin" />
              Creating Game...
            </>
          ) : createGame.isSuccess ? (
            <>
              <CheckCircle2Icon className="h-4 w-4 mr-2" />
              Game Created!
            </>
          ) : (
            'Create Game'
          )}
        </Button>
      </div>

      {/* Error state */}
      {createGame.isError && (
        <div className="rounded-lg border border-red-200 dark:border-red-900/40 bg-red-50 dark:bg-red-950/20 p-3">
          <p className="text-sm text-red-700 dark:text-red-400">
            {createGame.error.message}
          </p>
        </div>
      )}
    </div>
  );
}

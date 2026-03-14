'use client';

/**
 * Game Toolbox Page
 *
 * Route: /library/games/[gameId]/toolbox
 * Loads the toolbox for a specific game and renders either
 * Freeform or Phased layout based on toolbox mode.
 *
 * Epic #412 — Game Toolbox.
 */

import { useEffect } from 'react';

import { ArrowLeft, Loader2, AlertCircle, Wrench } from 'lucide-react';
import { useParams, useRouter } from 'next/navigation';

import { FreeformToolbox } from '@/components/toolbox/FreeformToolbox';
import { OfflineBanner } from '@/components/toolbox/OfflineBanner';
import { PhasedToolbox } from '@/components/toolbox/PhasedToolbox';
import { useToolboxSync } from '@/hooks/useToolboxSync';
import { useToolboxStore } from '@/lib/stores/toolboxStore';

export default function GameToolboxPage() {
  const params = useParams();
  const router = useRouter();
  const gameId = params?.gameId as string;

  const toolbox = useToolboxStore(s => s.toolbox);
  const isLoading = useToolboxStore(s => s.isLoading);
  const error = useToolboxStore(s => s.error);
  const loadToolboxByGame = useToolboxStore(s => s.loadToolboxByGame);

  // Load toolbox for this game
  useEffect(() => {
    if (gameId) {
      loadToolboxByGame(gameId);
    }
  }, [gameId, loadToolboxByGame]);

  // Wire SSE sync for live sessions
  useToolboxSync(toolbox?.id ?? undefined);

  // Loading state
  if (isLoading) {
    return (
      <div
        className="flex min-h-[60vh] items-center justify-center"
        data-testid="toolbox-page-loading"
      >
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  // Error state
  if (error) {
    return (
      <div
        className="flex min-h-[60vh] flex-col items-center justify-center gap-3 px-4"
        data-testid="toolbox-page-error"
      >
        <AlertCircle className="h-10 w-10 text-destructive" />
        <p className="text-center text-sm text-destructive">{error}</p>
        <button
          type="button"
          onClick={() => router.back()}
          className="mt-2 flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to game
        </button>
      </div>
    );
  }

  // Not found state
  if (!toolbox) {
    return (
      <div
        className="flex min-h-[60vh] flex-col items-center justify-center gap-3 px-4 text-muted-foreground"
        data-testid="toolbox-page-empty"
      >
        <Wrench className="h-10 w-10" />
        <p className="text-center text-sm">No toolbox configured for this game</p>
        <button
          type="button"
          onClick={() => router.back()}
          className="mt-2 flex items-center gap-1 text-sm hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to game
        </button>
      </div>
    );
  }

  return (
    <div className="flex flex-col" data-testid="toolbox-page">
      {/* Page header */}
      <div className="flex items-center gap-3 border-b px-4 py-3">
        <button
          type="button"
          onClick={() => router.back()}
          className="rounded-md p-1 hover:bg-accent"
          aria-label="Back to game"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <h1 className="text-lg font-semibold">{toolbox.name}</h1>
        <span className="rounded-full bg-muted px-2.5 py-0.5 text-xs text-muted-foreground">
          {toolbox.mode}
        </span>
      </div>

      {/* Offline banner */}
      <OfflineBanner className="mx-4 mt-2" />

      {/* Mode-specific layout */}
      {toolbox.mode === 'Phased' ? (
        <PhasedToolbox toolboxId={toolbox.id} />
      ) : (
        <FreeformToolbox toolboxId={toolbox.id} />
      )}
    </div>
  );
}

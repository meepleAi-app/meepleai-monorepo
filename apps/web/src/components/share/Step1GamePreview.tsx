import { Info } from 'lucide-react';

import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import type { UserLibraryEntry } from '@/lib/api';

/**
 * Step 1: Game Preview
 * Issue #2743: Frontend - UI Condivisione da Libreria
 */

interface Step1GamePreviewProps {
  game: UserLibraryEntry;
  existingInCatalog?: boolean;
}

export function Step1GamePreview({ game, existingInCatalog }: Step1GamePreviewProps) {
  return (
    <div className="space-y-4">
      {/* Game Card Preview */}
      <div className="flex gap-4 rounded-lg border bg-card p-4">
        <div className="relative h-24 w-24 flex-shrink-0">
          {game.gameIconUrl ? (
            /* eslint-disable-next-line @next/next/no-img-element -- External user-provided game icon URL */
            <img
              src={game.gameIconUrl}
              alt={game.gameTitle}
              className="h-full w-full rounded-md object-cover"
            />
          ) : (
            <div className="flex h-full w-full items-center justify-center rounded-md bg-muted text-2xl font-bold text-muted-foreground">
              {game.gameTitle.charAt(0).toUpperCase()}
            </div>
          )}
        </div>

        <div className="flex-1 space-y-1">
          <h3 className="text-lg font-semibold">{game.gameTitle}</h3>
          {game.gamePublisher && (
            <p className="text-sm text-muted-foreground">{game.gamePublisher}</p>
          )}
          {game.gameYearPublished && (
            <p className="text-sm text-muted-foreground">Published: {game.gameYearPublished}</p>
          )}
        </div>
      </div>

      {/* Existing Game Warning */}
      {existingInCatalog && (
        <Alert className="border-yellow-500/50 bg-yellow-50 dark:bg-yellow-950/20">
          <Info className="h-4 w-4 text-yellow-600 dark:text-yellow-500" />
          <AlertDescription className="text-sm text-yellow-800 dark:text-yellow-200">
            <p className="font-semibold">This game already exists in the shared catalog</p>
            <p className="mt-1">
              Your contribution will be added to the existing game entry. You can attach additional
              documents or updated information.
            </p>
          </AlertDescription>
        </Alert>
      )}

      {/* Game Details Summary */}
      <div className="grid grid-cols-2 gap-2 text-sm">
        <div className="rounded-md bg-muted p-3">
          <p className="text-muted-foreground">Publisher</p>
          <p className="font-medium">{game.gamePublisher || 'Unknown'}</p>
        </div>
        <div className="rounded-md bg-muted p-3">
          <p className="text-muted-foreground">Year</p>
          <p className="font-medium">{game.gameYearPublished || 'Unknown'}</p>
        </div>
      </div>

      <p className="text-sm text-muted-foreground">
        Review the game details above before continuing with your share request.
      </p>
    </div>
  );
}

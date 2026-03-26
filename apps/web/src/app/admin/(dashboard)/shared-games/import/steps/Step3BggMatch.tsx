'use client';

/**
 * Step 3: BGG Match
 * Issue #4164: BoardGameGeek search with match scoring and selection
 *
 * Thin wrapper around BggSearchPanel that integrates with the import wizard store.
 * The search/selection logic lives in BggSearchPanel for reusability.
 */

import type { JSX } from 'react';

import { CheckCircle2 } from 'lucide-react';

import { BggSearchPanel } from '@/components/admin/shared-games/BggSearchPanel';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { useGameImportWizardStore, type BggGameData } from '@/stores/useGameImportWizardStore';

export interface Step3BggMatchProps {
  /** Callback when BGG game is selected */
  onComplete?: (bggId: number, data?: BggGameData) => void;
}

export function Step3BggMatch({ onComplete }: Step3BggMatchProps): JSX.Element {
  const { extractedMetadata, selectedBggId, bggGameData, setSelectedBggId } =
    useGameImportWizardStore();

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h3 className="text-lg font-semibold">Seleziona gioco dal catalogo</h3>
        <p className="text-sm text-muted-foreground">
          Cerca il gioco corrispondente o inserisci un ID manualmente.
        </p>
      </div>

      <BggSearchPanel
        initialQuery={extractedMetadata?.title}
        onSelect={data => {
          const bggGameData: BggGameData = {
            bggId: data.id,
            name: data.name,
            yearPublished: data.yearPublished,
            minPlayers: data.minPlayers,
            maxPlayers: data.maxPlayers,
            playingTime: data.playingTime,
            minAge: data.minAge,
            description: data.description,
            imageUrl: data.imageUrl,
            thumbnailUrl: data.thumbnailUrl,
          };
          setSelectedBggId(data.id, bggGameData);
          onComplete?.(data.id, bggGameData);
        }}
      />

      {/* Selected Game Summary */}
      {selectedBggId && bggGameData && (
        <Alert>
          <CheckCircle2 className="h-4 w-4 text-green-500" />
          <AlertTitle>Game Selected</AlertTitle>
          <AlertDescription className="text-sm">
            Selezionato: <strong>{bggGameData.name}</strong> (ID #{selectedBggId}). Clicca
            &quot;Next&quot; to continue.
          </AlertDescription>
        </Alert>
      )}
    </div>
  );
}

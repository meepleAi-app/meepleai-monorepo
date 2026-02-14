/**
 * Add Private Game with BGG Search
 * Issue #4053: User-Facing BGG Search for Private Game Creation
 *
 * Two-step flow:
 * 1. Search BGG or skip to manual entry
 * 2. Fill form (pre-populated from BGG or empty for manual)
 */

'use client';

import { useState, useCallback } from 'react';

import { ArrowLeft, Globe, PenLine, Loader2 } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { BggSearchResult, BggGameDetails } from '@/lib/api/schemas/games.schemas';

import { AddPrivateGameForm, type AddPrivateGameFormData } from './AddPrivateGameForm';
import { BggGameSearch } from './BggGameSearch';

export interface AddPrivateGameWithBggProps {
  onSubmit: (data: AddPrivateGameFormData, source: 'Manual' | 'Bgg', bggId?: number, thumbnailUrl?: string) => Promise<void>;
  onCancel: () => void;
  isSubmitting?: boolean;
}

type Step = 'choose' | 'loading-bgg' | 'form-bgg' | 'form-manual';

function mapBggDetailsToFormData(details: BggGameDetails): Partial<AddPrivateGameFormData> {
  return {
    title: details.name,
    minPlayers: details.minPlayers ?? 1,
    maxPlayers: details.maxPlayers ?? 4,
    yearPublished: details.yearPublished ?? undefined,
    playingTimeMinutes: details.playingTime ?? undefined,
    minAge: details.minAge ?? undefined,
    complexityRating: details.averageWeight ? Math.round(details.averageWeight * 10) / 10 : undefined,
    description: details.description ?? undefined,
    imageUrl: details.imageUrl ?? undefined,
  };
}

export function AddPrivateGameWithBgg({
  onSubmit,
  onCancel,
  isSubmitting = false,
}: AddPrivateGameWithBggProps) {
  const [step, setStep] = useState<Step>('choose');
  const [bggDetails, setBggDetails] = useState<BggGameDetails | null>(null);
  const [formInitialValues, setFormInitialValues] = useState<Partial<AddPrivateGameFormData>>({});

  const handleBggSelect = useCallback(async (result: BggSearchResult) => {
    setStep('loading-bgg');
    try {
      const details = await api.bgg.getGameDetails(result.bggId);
      setBggDetails(details);
      setFormInitialValues(mapBggDetailsToFormData(details));
      setStep('form-bgg');
    } catch (err) {
      console.error('Failed to load BGG game details:', err);
      toast.error('Impossibile caricare i dettagli del gioco da BGG');
      setStep('choose');
    }
  }, []);

  const handleFormSubmit = useCallback(async (data: AddPrivateGameFormData) => {
    if (step === 'form-bgg' && bggDetails) {
      await onSubmit(data, 'Bgg', bggDetails.bggId, bggDetails.thumbnailUrl ?? undefined);
    } else {
      await onSubmit(data, 'Manual');
    }
  }, [step, bggDetails, onSubmit]);

  const handleBack = useCallback(() => {
    setStep('choose');
    setBggDetails(null);
    setFormInitialValues({});
  }, []);

  // Step: Choose mode (BGG search or manual)
  if (step === 'choose') {
    return (
      <div className="space-y-6" data-testid="add-game-choose-mode">
        <div className="space-y-3">
          <p className="text-sm text-muted-foreground">
            Cerca il gioco su BoardGameGeek per compilare automaticamente i dettagli,
            oppure inserisci manualmente le informazioni.
          </p>

          <BggGameSearch onSelect={handleBggSelect} />

          <div className="relative py-3">
            <div className="absolute inset-0 flex items-center">
              <span className="w-full border-t" />
            </div>
            <div className="relative flex justify-center text-xs uppercase">
              <span className="bg-background px-2 text-muted-foreground">oppure</span>
            </div>
          </div>

          <Button
            variant="outline"
            className="w-full"
            onClick={() => setStep('form-manual')}
            data-testid="manual-entry-btn"
          >
            <PenLine className="h-4 w-4 mr-2" />
            Inserimento Manuale
          </Button>
        </div>

        <div className="flex justify-end">
          <Button variant="ghost" onClick={onCancel}>
            Cancel
          </Button>
        </div>
      </div>
    );
  }

  // Step: Loading BGG details
  if (step === 'loading-bgg') {
    return (
      <div className="flex flex-col items-center justify-center py-12 gap-3" data-testid="loading-bgg-details">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        <p className="text-sm text-muted-foreground">Caricamento dettagli da BGG...</p>
      </div>
    );
  }

  // Step: Form (BGG or manual)
  const isBggMode = step === 'form-bgg';

  return (
    <div className="space-y-4" data-testid={isBggMode ? 'add-game-bgg-form' : 'add-game-manual-form'}>
      {/* Header with back button and source indicator */}
      <div className="flex items-center gap-2">
        <Button
          variant="ghost"
          size="sm"
          onClick={handleBack}
          data-testid="back-to-search-btn"
        >
          <ArrowLeft className="h-4 w-4 mr-1" />
          Indietro
        </Button>
        {isBggMode && (
          <span className="inline-flex items-center gap-1 text-xs bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200 px-2 py-0.5 rounded-full">
            <Globe className="h-3 w-3" />
            da BGG
          </span>
        )}
        {!isBggMode && (
          <span className="inline-flex items-center gap-1 text-xs bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-200 px-2 py-0.5 rounded-full">
            <PenLine className="h-3 w-3" />
            Manuale
          </span>
        )}
      </div>

      <AddPrivateGameForm
        key={isBggMode ? `bgg-${bggDetails?.bggId}` : 'manual'}
        onSubmit={handleFormSubmit}
        onCancel={onCancel}
        isSubmitting={isSubmitting}
        initialValues={isBggMode ? formInitialValues : undefined}
        submitLabel={isBggMode ? 'Add from BGG' : 'Add Private Game'}
      />
    </div>
  );
}

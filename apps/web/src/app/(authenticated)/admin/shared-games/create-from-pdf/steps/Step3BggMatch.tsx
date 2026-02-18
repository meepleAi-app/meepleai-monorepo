/**
 * Step3BggMatch - Issue #4141
 *
 * BGG match step with search by title and manual ID input.
 */

'use client';

import { useState } from 'react';

import { Search, Hash, Loader2, AlertCircle } from 'lucide-react';
import useSWR from 'swr';

import { BggGameCard } from '@/components/bgg/BggGameCard';
import { BggPreviewCard } from '@/components/bgg/BggPreviewCard';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { searchBggGames, fetchBggGameById } from '@/lib/api/bgg';
import { usePdfWizardStore } from '@/lib/stores/pdf-wizard-store';

interface Step3BggMatchProps {
  onNext: () => void;
  onBack: () => void;
}

/**
 * Step 3: BGG Match Component
 *
 * Features:
 * - Tabs: Search by title / Enter BGG ID
 * - Auto-fetch suggestions on mount (using extracted title)
 * - Grid of BggGameCard (top 5 results)
 * - Manual BGG ID input with fetch button
 * - BggPreviewCard for selected game
 * - Skip BGG option
 */
export function Step3BggMatch({ onNext, onBack }: Step3BggMatchProps) {
  const [activeTab, setActiveTab] = useState<'search' | 'manual'>('search');
  const [selectedGameId, setSelectedGameId] = useState<number | null>(null);
  const [manualBggId, setManualBggId] = useState<string>('');
  const [manualFetchError, setManualFetchError] = useState<string | null>(null);
  const [isManualFetching, setIsManualFetching] = useState(false);

  const wizardData = usePdfWizardStore((state) => ({
    extractedTitle: state.extractedTitle,
    bggDetails: state.bggDetails,
  }));

  const setStep3Data = usePdfWizardStore((state) => state.setStep3Data);

  // Auto-fetch search suggestions using extracted title
  const { data: searchResults, isLoading: isSearching } = useSWR(
    wizardData.extractedTitle ? ['bgg-search', wizardData.extractedTitle] : null,
    () => searchBggGames(wizardData.extractedTitle),
    {
      revalidateOnFocus: false,
      revalidateOnReconnect: false,
    }
  );

  // Fetch selected game details
  const { data: selectedGameDetails, isLoading: isFetchingDetails } = useSWR(
    selectedGameId ? ['bgg-details', selectedGameId] : null,
    () => fetchBggGameById(selectedGameId as string), // selectedGameId guaranteed non-null by key condition
    {
      revalidateOnFocus: false,
      revalidateOnReconnect: false,
    }
  );

  const handleSelectGame = (gameId: number) => {
    setSelectedGameId(gameId);
    setManualFetchError(null);
  };

  const handleManualFetch = async () => {
    const bggId = parseInt(manualBggId, 10);

    if (isNaN(bggId) || bggId <= 0) {
      setManualFetchError('Please enter a valid BGG ID');
      return;
    }

    setIsManualFetching(true);
    setManualFetchError(null);

    try {
      const game = await fetchBggGameById(bggId);
      setSelectedGameId(game.id);
      setActiveTab('search'); // Switch to search tab to show preview
    } catch (error) {
      setManualFetchError(
        error instanceof Error ? error.message : 'Failed to fetch game'
      );
    } finally {
      setIsManualFetching(false);
    }
  };

  const handleNext = () => {
    if (selectedGameDetails) {
      setStep3Data({
        selectedBggId: selectedGameDetails.id,
        bggDetails: selectedGameDetails,
      });
    }
    onNext();
  };

  const handleSkip = () => {
    setStep3Data({
      selectedBggId: null,
      bggDetails: null,
    });
    onNext();
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-2xl font-bold text-gray-900 mb-2">
          Match with BoardGameGeek
        </h2>
        <p className="font-nunito text-sm text-gray-600">
          Find the game on BGG to auto-fill details (optional).
        </p>
      </div>

      {/* BGG Tabs */}
      <Tabs value={activeTab} onValueChange={(v: string) => setActiveTab(v as 'search' | 'manual')}>
        <TabsList className="grid w-full grid-cols-2">
          <TabsTrigger value="search">
            <Search className="w-4 h-4 mr-2" aria-hidden="true" />
            Search by Title
          </TabsTrigger>
          <TabsTrigger value="manual">
            <Hash className="w-4 h-4 mr-2" aria-hidden="true" />
            Enter BGG ID
          </TabsTrigger>
        </TabsList>

        {/* Search Tab */}
        <TabsContent value="search" className="space-y-4">
          {isSearching && (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="w-8 h-8 animate-spin text-amber-500" aria-hidden="true" />
              <span className="ml-3 font-nunito text-sm text-gray-600">
                Searching BoardGameGeek...
              </span>
            </div>
          )}

          {!isSearching && searchResults && searchResults.length > 0 && (
            <div className="grid grid-cols-1 gap-3">
              {searchResults.slice(0, 5).map((game: import('@/types/bgg').BggSearchResult) => (
                <BggGameCard
                  key={game.id}
                  game={game}
                  selected={selectedGameId === game.id}
                  onSelect={handleSelectGame}
                />
              ))}
            </div>
          )}

          {!isSearching && searchResults && searchResults.length === 0 && (
            <Alert>
              <AlertDescription className="font-nunito">
                No games found for "{wizardData.extractedTitle}". Try entering the BGG ID
                manually.
              </AlertDescription>
            </Alert>
          )}
        </TabsContent>

        {/* Manual ID Tab */}
        <TabsContent value="manual" className="space-y-4">
          <div className="bg-white/70 backdrop-blur-md rounded-lg p-6 border-2 border-gray-200">
            <Label htmlFor="manualBggId" className="font-nunito text-sm font-semibold">
              BoardGameGeek ID
            </Label>
            <div className="flex gap-3 mt-2">
              <Input
                id="manualBggId"
                type="number"
                placeholder="e.g., 13 for Catan"
                value={manualBggId}
                onChange={(e) => setManualBggId(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    handleManualFetch();
                  }
                }}
                disabled={isManualFetching}
              />
              <Button onClick={handleManualFetch} disabled={isManualFetching}>
                {isManualFetching && (
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" aria-hidden="true" />
                )}
                Fetch Details
              </Button>
            </div>

            {manualFetchError && (
              <Alert variant="destructive" className="mt-3">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>{manualFetchError}</AlertDescription>
              </Alert>
            )}
          </div>
        </TabsContent>
      </Tabs>

      {/* Selected Game Preview */}
      {isFetchingDetails && (
        <div className="flex items-center justify-center py-8">
          <Loader2 className="w-6 h-6 animate-spin text-amber-500" aria-hidden="true" />
          <span className="ml-3 font-nunito text-sm text-gray-600">
            Loading game details...
          </span>
        </div>
      )}

      {!isFetchingDetails && selectedGameDetails && (
        <div>
          <Label className="font-nunito text-sm font-semibold text-gray-700 mb-2 block">
            Selected Game
          </Label>
          <BggPreviewCard game={selectedGameDetails} />
        </div>
      )}

      {/* Actions */}
      <div className="flex items-center justify-between gap-4">
        <Button variant="outline" onClick={onBack} size="lg">
          Back
        </Button>

        <div className="flex items-center gap-3">
          <Button variant="secondary" onClick={handleSkip} size="lg">
            Skip BGG
          </Button>
          <Button onClick={handleNext} disabled={!selectedGameDetails} size="lg">
            Use BGG Data
          </Button>
        </div>
      </div>
    </div>
  );
}

/**
 * GameSourceStep - Step 1: Search catalog/BGG or create custom game.
 * Issue #4819: AddGameSheet Step 1 - Game Source
 * Epic #4817: User Collection Wizard
 */

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { ExternalLink, Loader2, Plus, Search, X } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import {
  useAddGameWizardStore,
  type SelectedGameData,
} from '@/lib/stores/add-game-wizard-store';
import { GameSearchResults, type GameSearchResultItem } from './GameSearchResults';
import { CustomGameForm, type CustomGameValues } from './CustomGameForm';

const DEBOUNCE_MS = 300;
const PAGE_SIZE = 10;

export function GameSourceStep() {
  const setSelectedGame = useAddGameWizardStore((s) => s.setSelectedGame);
  const goNext = useAddGameWizardStore((s) => s.goNext);
  const selectedGame = useAddGameWizardStore((s) => s.selectedGame);

  const inputRef = useRef<HTMLInputElement>(null);
  const abortRef = useRef<AbortController | null>(null);

  const [searchTerm, setSearchTerm] = useState('');
  const [debouncedTerm, setDebouncedTerm] = useState('');
  const [results, setResults] = useState<GameSearchResultItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [showBggButton, setShowBggButton] = useState(false);
  const [bggLoading, setBggLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showCustomForm, setShowCustomForm] = useState(false);
  const [customSubmitting, setCustomSubmitting] = useState(false);

  // Debounce search term
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedTerm(searchTerm), DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [searchTerm]);

  // Search catalog when debounced term changes
  useEffect(() => {
    if (!debouncedTerm.trim()) {
      setResults([]);
      setShowBggButton(false);
      setError(null);
      return;
    }

    if (abortRef.current) abortRef.current.abort();
    abortRef.current = new AbortController();

    const searchCatalog = async () => {
      setLoading(true);
      setError(null);
      setShowBggButton(false);

      try {
        const response = await api.sharedGames.search({
          searchTerm: debouncedTerm,
          page: 1,
          pageSize: PAGE_SIZE,
          status: 1,
        });

        if (response.items.length > 0) {
          setResults(
            response.items.map((game) => ({
              id: game.id,
              title: game.title,
              yearPublished: game.yearPublished,
              thumbnailUrl: game.thumbnailUrl,
              minPlayers: game.minPlayers,
              maxPlayers: game.maxPlayers,
              playingTimeMinutes: game.playingTimeMinutes,
              averageRating: game.averageRating,
              source: 'catalog' as const,
              bggId: game.bggId,
            }))
          );
        } else {
          setResults([]);
          setShowBggButton(true);
        }
      } catch (err) {
        if ((err as Error).name !== 'AbortError') {
          setError('Errore nella ricerca. Riprova.');
          setShowBggButton(true);
        }
      } finally {
        setLoading(false);
      }
    };

    void searchCatalog();
  }, [debouncedTerm]);

  // BGG fallback search
  const handleBggSearch = useCallback(async () => {
    if (!debouncedTerm.trim()) return;

    setBggLoading(true);
    setError(null);

    try {
      const response = await api.bgg.search(debouncedTerm, false, 1, PAGE_SIZE);

      if (response.results.length > 0) {
        setResults(
          response.results.map((item) => ({
            id: `bgg-${item.bggId}`,
            title: item.name,
            yearPublished: item.yearPublished ?? undefined,
            thumbnailUrl: item.thumbnailUrl ?? undefined,
            source: 'bgg' as const,
            bggId: item.bggId,
          }))
        );
        setShowBggButton(false);
      } else {
        setResults([]);
        setError('Nessun gioco trovato su BoardGameGeek.');
      }
    } catch {
      setError('BoardGameGeek non disponibile. Riprova più tardi.');
    } finally {
      setBggLoading(false);
    }
  }, [debouncedTerm]);

  // Select game from search results
  const handleSelectGame = useCallback(
    async (game: GameSearchResultItem) => {
      const gameData: SelectedGameData = {
        gameId: game.id,
        title: game.title,
        imageUrl: game.thumbnailUrl,
        thumbnailUrl: game.thumbnailUrl,
        minPlayers: game.minPlayers,
        maxPlayers: game.maxPlayers,
        playingTimeMinutes: game.playingTimeMinutes,
        source: game.source,
        categories: [],
        mechanics: [],
      };

      // If BGG game, try to get full details
      if (game.source === 'bgg' && game.bggId) {
        try {
          const details = await api.bgg.getGameDetails(game.bggId);
          gameData.minPlayers = details.minPlayers ?? undefined;
          gameData.maxPlayers = details.maxPlayers ?? undefined;
          gameData.playingTimeMinutes = details.playingTime ?? undefined;
          gameData.description = details.description ?? undefined;
          gameData.imageUrl = details.imageUrl ?? game.thumbnailUrl;
          gameData.thumbnailUrl = details.thumbnailUrl ?? game.thumbnailUrl;
          gameData.yearPublished = details.yearPublished ?? undefined;
        } catch {
          // Use basic search data if details fail
        }
      }

      setSelectedGame(gameData);
      goNext();
    },
    [setSelectedGame, goNext]
  );

  // Create custom game
  const handleCustomSubmit = useCallback(
    async (values: CustomGameValues) => {
      setCustomSubmitting(true);
      try {
        const result = await api.library.addPrivateGame({
          source: 'Manual',
          title: values.title,
          minPlayers: values.minPlayers,
          maxPlayers: values.maxPlayers,
          playingTimeMinutes: values.playingTimeMinutes ?? null,
          description: values.description ?? null,
        });

        setSelectedGame({
          gameId: result.id,
          title: result.title,
          minPlayers: result.minPlayers,
          maxPlayers: result.maxPlayers,
          playingTimeMinutes: result.playingTimeMinutes ?? undefined,
          description: result.description ?? undefined,
          source: 'custom',
        });
        goNext();
      } catch {
        setError('Errore nella creazione del gioco. Riprova.');
      } finally {
        setCustomSubmitting(false);
      }
    },
    [setSelectedGame, goNext]
  );

  const handleClear = useCallback(() => {
    setSearchTerm('');
    setResults([]);
    setShowBggButton(false);
    setError(null);
    inputRef.current?.focus();
  }, []);

  // Auto-focus on mount
  useEffect(() => {
    inputRef.current?.focus();
  }, []);

  return (
    <div className="space-y-4">
      <div>
        <h3 className="text-base font-semibold text-slate-200 mb-1">Scegli Sorgente Gioco</h3>
        <p className="text-sm text-slate-500">
          Cerca nel catalogo, importa da BGG, o crea manualmente
        </p>
      </div>

      {/* Selected game indicator */}
      {selectedGame && !showCustomForm && (
        <div className="flex items-center gap-3 rounded-lg border border-teal-500/30 bg-teal-500/10 p-3">
          <div className="flex-1 min-w-0">
            <p className="text-sm font-medium text-teal-300 truncate">{selectedGame.title}</p>
            <p className="text-xs text-slate-400">
              {selectedGame.source === 'catalog' ? 'Dal catalogo' : selectedGame.source === 'bgg' ? 'Da BGG' : 'Personalizzato'}
            </p>
          </div>
          <Badge variant="outline" className="text-teal-400 border-teal-500/30">
            Selezionato
          </Badge>
        </div>
      )}

      {/* Search Input */}
      {!showCustomForm && (
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-500" />
          <Input
            ref={inputRef}
            type="search"
            placeholder="Cerca un gioco..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="pl-10 pr-10 bg-slate-800 border-slate-700"
            aria-label="Cerca giochi"
          />
          {searchTerm && (
            <Button
              variant="ghost"
              size="sm"
              onClick={handleClear}
              className="absolute right-1 top-1/2 -translate-y-1/2 h-7 w-7 p-0 text-slate-500"
              aria-label="Cancella ricerca"
            >
              <X className="h-4 w-4" />
            </Button>
          )}
        </div>
      )}

      {/* Error */}
      {error && !loading && (
        <p className="text-sm text-red-400 text-center py-2">{error}</p>
      )}

      {/* Search Results */}
      {!showCustomForm && (
        <GameSearchResults
          results={results}
          loading={loading}
          onSelect={handleSelectGame}
        />
      )}

      {/* BGG Fallback Button */}
      {showBggButton && !loading && !showCustomForm && (
        <div className="text-center py-3">
          <p className="text-sm text-slate-500 mb-3">
            Nessun gioco trovato nel catalogo.
          </p>
          <Button
            onClick={handleBggSearch}
            disabled={bggLoading}
            variant="outline"
            size="sm"
          >
            {bggLoading ? (
              <Loader2 className="h-4 w-4 mr-2 animate-spin" />
            ) : (
              <ExternalLink className="h-4 w-4 mr-2" />
            )}
            Cerca su BoardGameGeek
          </Button>
        </div>
      )}

      {/* No results after search */}
      {!loading && !showBggButton && !error && results.length === 0 && debouncedTerm.trim() && !showCustomForm && (
        <div className="text-center py-4">
          <p className="text-sm text-slate-500">
            Nessun risultato per &quot;{debouncedTerm}&quot;
          </p>
        </div>
      )}

      {/* Custom Game Form */}
      {showCustomForm && (
        <CustomGameForm
          onSubmit={handleCustomSubmit}
          onCancel={() => setShowCustomForm(false)}
          submitting={customSubmitting}
        />
      )}

      {/* Custom Game Button */}
      {!showCustomForm && (
        <button
          type="button"
          onClick={() => setShowCustomForm(true)}
          className="w-full flex items-center gap-3 p-3 rounded-lg border border-dashed border-slate-700 hover:border-slate-600 hover:bg-slate-800/40 transition-colors text-left"
        >
          <div className="w-10 h-10 rounded-lg bg-slate-800 flex items-center justify-center flex-shrink-0">
            <Plus className="h-5 w-5 text-slate-400" />
          </div>
          <div>
            <p className="text-sm font-medium text-slate-300">Crea gioco personalizzato</p>
            <p className="text-xs text-slate-500">Non trovi il gioco? Crealo manualmente</p>
          </div>
        </button>
      )}
    </div>
  );
}

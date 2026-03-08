/**
 * Session Creation Wizard
 *
 * Issue #5041 — Sessions Redesign Phase 1
 *
 * 4-step wizard:
 *   1. Select game from library
 *   2. Configure scoring dimensions
 *   3. Add players (name, color, avatar)
 *   4. Review & Create
 *
 * Creates a LiveSession via liveSessionsClient.
 */

'use client';

import { useState, useCallback } from 'react';

import {
  ArrowLeft,
  ArrowRight,
  Check,
  Dices,
  Loader2,
  Plus,
  Search,
  Trash2,
  X,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { api } from '@/lib/api';
import type {
  PlayerColor,
  CreateLiveSessionRequest,
} from '@/lib/api/schemas/live-sessions.schemas';

// ========== Types ==========

interface GameOption {
  id: string;
  title: string;
  imageUrl?: string;
}

interface ScoringDimension {
  name: string;
  unit: string;
}

interface PlayerEntry {
  id: string;
  displayName: string;
  color: PlayerColor;
}

const PLAYER_COLORS: { value: PlayerColor; label: string; className: string }[] = [
  { value: 'Red', label: 'Rosso', className: 'bg-red-500' },
  { value: 'Blue', label: 'Blu', className: 'bg-blue-500' },
  { value: 'Green', label: 'Verde', className: 'bg-green-500' },
  { value: 'Yellow', label: 'Giallo', className: 'bg-yellow-400' },
  { value: 'Purple', label: 'Viola', className: 'bg-purple-500' },
  { value: 'Orange', label: 'Arancione', className: 'bg-orange-500' },
  { value: 'White', label: 'Bianco', className: 'bg-white border border-gray-300' },
  { value: 'Black', label: 'Nero', className: 'bg-gray-900' },
  { value: 'Pink', label: 'Rosa', className: 'bg-pink-400' },
  { value: 'Teal', label: 'Turchese', className: 'bg-teal-500' },
];

const DEFAULT_DIMENSIONS: ScoringDimension[] = [{ name: 'Punti', unit: 'PV' }];

// ========== Step Indicator ==========

function StepIndicator({ currentStep, totalSteps }: { currentStep: number; totalSteps: number }) {
  return (
    <div className="flex items-center justify-center gap-2 mb-6">
      {Array.from({ length: totalSteps }, (_, i) => (
        <div
          key={i}
          className={`h-2 rounded-full transition-all ${
            i === currentStep
              ? 'w-8 bg-indigo-500'
              : i < currentStep
                ? 'w-2 bg-indigo-300'
                : 'w-2 bg-muted'
          }`}
        />
      ))}
    </div>
  );
}

// ========== Step 1: Select Game ==========

function SelectGameStep({
  selectedGame,
  gameName,
  onSelectGame,
  onGameNameChange,
}: {
  selectedGame: GameOption | null;
  gameName: string;
  onSelectGame: (game: GameOption | null) => void;
  onGameNameChange: (name: string) => void;
}) {
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<GameOption[]>([]);
  const [isSearching, setIsSearching] = useState(false);

  const handleSearch = useCallback(async (query: string) => {
    setSearchQuery(query);
    if (query.length < 2) {
      setSearchResults([]);
      return;
    }

    setIsSearching(true);
    try {
      const response = await api.library.getLibrary({ pageSize: 50 });
      const filtered = (response.items ?? [])
        .filter(g => g.gameTitle.toLowerCase().includes(query.toLowerCase()))
        .slice(0, 10);
      setSearchResults(
        filtered.map(g => ({
          id: g.gameId,
          title: g.gameTitle,
          imageUrl: g.gameImageUrl ?? undefined,
        }))
      );
    } catch {
      setSearchResults([]);
    } finally {
      setIsSearching(false);
    }
  }, []);

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-lg font-semibold font-quicksand">Scegli il gioco</h2>
        <p className="text-sm text-muted-foreground">
          Cerca nella tua libreria o inserisci un nome
        </p>
      </div>

      {selectedGame ? (
        <div className="flex items-center gap-3 rounded-xl border border-indigo-200 bg-indigo-50 p-3 dark:bg-indigo-950/20 dark:border-indigo-800">
          {selectedGame.imageUrl && (
            <img
              src={selectedGame.imageUrl}
              alt={selectedGame.title}
              className="h-12 w-12 rounded-lg object-cover"
            />
          )}
          <div className="flex-1 min-w-0">
            <p className="font-semibold truncate">{selectedGame.title}</p>
            <p className="text-xs text-muted-foreground">Dalla tua libreria</p>
          </div>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              onSelectGame(null);
              onGameNameChange('');
            }}
          >
            <X className="h-4 w-4" />
          </Button>
        </div>
      ) : (
        <>
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Cerca nella tua libreria..."
              value={searchQuery}
              onChange={e => handleSearch(e.target.value)}
              className="pl-9"
            />
            {isSearching && (
              <Loader2 className="absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 animate-spin text-muted-foreground" />
            )}
          </div>

          {/* Search results */}
          {searchResults.length > 0 && (
            <div className="rounded-xl border border-border bg-card divide-y divide-border max-h-60 overflow-y-auto">
              {searchResults.map(game => (
                <button
                  key={game.id}
                  onClick={() => {
                    onSelectGame(game);
                    onGameNameChange(game.title);
                    setSearchQuery('');
                    setSearchResults([]);
                  }}
                  className="flex items-center gap-3 w-full p-3 text-left hover:bg-muted/50 transition-colors"
                >
                  {game.imageUrl && (
                    <img
                      src={game.imageUrl}
                      alt={game.title}
                      className="h-10 w-10 rounded-lg object-cover"
                    />
                  )}
                  <span className="font-medium text-sm truncate">{game.title}</span>
                </button>
              ))}
            </div>
          )}

          <div className="relative flex items-center gap-3">
            <div className="flex-1 border-t border-border" />
            <span className="text-xs text-muted-foreground px-2">oppure</span>
            <div className="flex-1 border-t border-border" />
          </div>

          <Input
            placeholder="Nome del gioco (es. Catan, Ticket to Ride...)"
            value={gameName}
            onChange={e => onGameNameChange(e.target.value)}
          />
        </>
      )}
    </div>
  );
}

// ========== Step 2: Configure Scoring ==========

function ConfigureScoringStep({
  dimensions,
  onDimensionsChange,
}: {
  dimensions: ScoringDimension[];
  onDimensionsChange: (dims: ScoringDimension[]) => void;
}) {
  const [newDimName, setNewDimName] = useState('');
  const [newDimUnit, setNewDimUnit] = useState('');

  const addDimension = () => {
    if (!newDimName.trim()) return;
    onDimensionsChange([
      ...dimensions,
      { name: newDimName.trim(), unit: newDimUnit.trim() || newDimName.trim() },
    ]);
    setNewDimName('');
    setNewDimUnit('');
  };

  const removeDimension = (index: number) => {
    onDimensionsChange(dimensions.filter((_, i) => i !== index));
  };

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-lg font-semibold font-quicksand">Configura Punteggi</h2>
        <p className="text-sm text-muted-foreground">
          Definisci le dimensioni di punteggio per la sessione
        </p>
      </div>

      {/* Current dimensions */}
      <div className="space-y-2">
        {dimensions.map((dim, i) => (
          <div
            key={i}
            className="flex items-center justify-between rounded-lg border border-border bg-card p-3"
          >
            <div className="flex items-center gap-2">
              <Dices className="h-4 w-4 text-indigo-500" />
              <span className="font-medium text-sm">{dim.name}</span>
              {dim.unit && dim.unit !== dim.name && (
                <Badge variant="outline" className="text-xs">
                  {dim.unit}
                </Badge>
              )}
            </div>
            {dimensions.length > 1 && (
              <Button variant="ghost" size="sm" onClick={() => removeDimension(i)}>
                <Trash2 className="h-4 w-4 text-muted-foreground" />
              </Button>
            )}
          </div>
        ))}
      </div>

      {/* Add new dimension */}
      <div className="flex gap-2">
        <Input
          placeholder="Nome dimensione (es. Risorse)"
          value={newDimName}
          onChange={e => setNewDimName(e.target.value)}
          className="flex-1"
          onKeyDown={e => e.key === 'Enter' && addDimension()}
        />
        <Input
          placeholder="Unità (es. R)"
          value={newDimUnit}
          onChange={e => setNewDimUnit(e.target.value)}
          className="w-24"
          onKeyDown={e => e.key === 'Enter' && addDimension()}
        />
        <Button variant="outline" size="icon" onClick={addDimension}>
          <Plus className="h-4 w-4" />
        </Button>
      </div>

      <p className="text-xs text-muted-foreground">
        Puoi sempre modificare le dimensioni durante la sessione.
      </p>
    </div>
  );
}

// ========== Step 3: Add Players ==========

function AddPlayersStep({
  players,
  onPlayersChange,
}: {
  players: PlayerEntry[];
  onPlayersChange: (players: PlayerEntry[]) => void;
}) {
  const [newName, setNewName] = useState('');

  const usedColors = players.map(p => p.color);
  const nextAvailableColor = PLAYER_COLORS.find(c => !usedColors.includes(c.value))?.value ?? 'Red';

  const addPlayer = () => {
    if (!newName.trim()) return;
    onPlayersChange([
      ...players,
      {
        id: `temp-${Date.now()}`,
        displayName: newName.trim(),
        color: nextAvailableColor,
      },
    ]);
    setNewName('');
  };

  const removePlayer = (id: string) => {
    onPlayersChange(players.filter(p => p.id !== id));
  };

  const changeColor = (id: string, color: PlayerColor) => {
    onPlayersChange(players.map(p => (p.id === id ? { ...p, color } : p)));
  };

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-lg font-semibold font-quicksand">Aggiungi Giocatori</h2>
        <p className="text-sm text-muted-foreground">Aggiungi i partecipanti alla sessione</p>
      </div>

      {/* Player list */}
      <div className="space-y-2">
        {players.map(player => (
          <div
            key={player.id}
            className="flex items-center gap-3 rounded-lg border border-border bg-card p-3"
          >
            <div
              className={`h-8 w-8 rounded-full shrink-0 ${
                PLAYER_COLORS.find(c => c.value === player.color)?.className ?? 'bg-gray-400'
              }`}
            />
            <span className="font-medium text-sm flex-1 truncate">{player.displayName}</span>

            {/* Color selector */}
            <div className="flex gap-1">
              {PLAYER_COLORS.filter(c => c.value === player.color || !usedColors.includes(c.value))
                .slice(0, 5)
                .map(c => (
                  <button
                    key={c.value}
                    onClick={() => changeColor(player.id, c.value)}
                    className={`h-5 w-5 rounded-full transition-all ${c.className} ${
                      c.value === player.color ? 'ring-2 ring-offset-1 ring-indigo-500' : ''
                    }`}
                    title={c.label}
                  />
                ))}
            </div>

            <Button variant="ghost" size="sm" onClick={() => removePlayer(player.id)}>
              <X className="h-4 w-4 text-muted-foreground" />
            </Button>
          </div>
        ))}
      </div>

      {/* Add player */}
      <div className="flex gap-2">
        <Input
          placeholder="Nome giocatore"
          value={newName}
          onChange={e => setNewName(e.target.value)}
          className="flex-1"
          onKeyDown={e => e.key === 'Enter' && addPlayer()}
        />
        <Button variant="outline" onClick={addPlayer}>
          <Plus className="h-4 w-4 mr-1" />
          Aggiungi
        </Button>
      </div>
    </div>
  );
}

// ========== Step 4: Review ==========

function ReviewStep({
  gameName,
  selectedGame,
  dimensions,
  players,
}: {
  gameName: string;
  selectedGame: GameOption | null;
  dimensions: ScoringDimension[];
  players: PlayerEntry[];
}) {
  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-lg font-semibold font-quicksand">Riepilogo</h2>
        <p className="text-sm text-muted-foreground">
          Controlla i dettagli della sessione prima di crearla
        </p>
      </div>

      {/* Game */}
      <div className="rounded-xl border border-border bg-card p-4">
        <p className="text-xs text-muted-foreground uppercase tracking-wide mb-1">Gioco</p>
        <div className="flex items-center gap-3">
          {selectedGame?.imageUrl && (
            <img
              src={selectedGame.imageUrl}
              alt={gameName}
              className="h-10 w-10 rounded-lg object-cover"
            />
          )}
          <span className="font-semibold">{gameName}</span>
        </div>
      </div>

      {/* Scoring */}
      <div className="rounded-xl border border-border bg-card p-4">
        <p className="text-xs text-muted-foreground uppercase tracking-wide mb-2">Punteggi</p>
        <div className="flex flex-wrap gap-2">
          {dimensions.map((dim, i) => (
            <Badge key={i} variant="outline">
              {dim.name}
              {dim.unit && dim.unit !== dim.name ? ` (${dim.unit})` : ''}
            </Badge>
          ))}
        </div>
      </div>

      {/* Players */}
      <div className="rounded-xl border border-border bg-card p-4">
        <p className="text-xs text-muted-foreground uppercase tracking-wide mb-2">
          Giocatori ({players.length})
        </p>
        <div className="flex flex-wrap gap-2">
          {players.map(player => (
            <div
              key={player.id}
              className="flex items-center gap-2 rounded-full border border-border bg-muted/50 px-3 py-1"
            >
              <div
                className={`h-4 w-4 rounded-full ${
                  PLAYER_COLORS.find(c => c.value === player.color)?.className ?? 'bg-gray-400'
                }`}
              />
              <span className="text-sm font-medium">{player.displayName}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// ========== Main Wizard ==========

export function SessionCreationWizard() {
  const router = useRouter();
  const [step, setStep] = useState(0);
  const [isCreating, setIsCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Step 1: Game
  const [selectedGame, setSelectedGame] = useState<GameOption | null>(null);
  const [gameName, setGameName] = useState('');

  // Step 2: Scoring
  const [dimensions, setDimensions] = useState<ScoringDimension[]>(DEFAULT_DIMENSIONS);

  // Step 3: Players
  const [players, setPlayers] = useState<PlayerEntry[]>([]);

  const totalSteps = 4;

  const canProceed = (): boolean => {
    switch (step) {
      case 0:
        return !!gameName.trim();
      case 1:
        return dimensions.length > 0;
      case 2:
        return players.length >= 1;
      case 3:
        return true;
      default:
        return false;
    }
  };

  const handleCreate = useCallback(async () => {
    setIsCreating(true);
    setError(null);

    try {
      const request: CreateLiveSessionRequest = {
        gameName: gameName.trim(),
        gameId: selectedGame?.id,
        scoringDimensions: dimensions.map(d => d.name),
        dimensionUnits: Object.fromEntries(dimensions.map(d => [d.name, d.unit])),
      };

      const sessionId = await api.liveSessions.createSession(request);

      // Add players after session creation
      for (const player of players) {
        await api.liveSessions.addPlayer(sessionId, {
          displayName: player.displayName,
          color: player.color,
        });
      }

      // Start the session
      await api.liveSessions.startSession(sessionId);

      router.push(`/sessions/${sessionId}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Errore nella creazione della sessione');
      setIsCreating(false);
    }
  }, [gameName, selectedGame, dimensions, players, router]);

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <StepIndicator currentStep={step} totalSteps={totalSteps} />

      {/* Steps */}
      {step === 0 && (
        <SelectGameStep
          selectedGame={selectedGame}
          gameName={gameName}
          onSelectGame={setSelectedGame}
          onGameNameChange={setGameName}
        />
      )}
      {step === 1 && (
        <ConfigureScoringStep dimensions={dimensions} onDimensionsChange={setDimensions} />
      )}
      {step === 2 && <AddPlayersStep players={players} onPlayersChange={setPlayers} />}
      {step === 3 && (
        <ReviewStep
          gameName={gameName}
          selectedGame={selectedGame}
          dimensions={dimensions}
          players={players}
        />
      )}

      {/* Error */}
      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:bg-red-950/20 dark:border-red-800 dark:text-red-300">
          {error}
        </div>
      )}

      {/* Navigation */}
      <div className="flex items-center justify-between pt-2">
        <Button
          variant="ghost"
          onClick={() => (step === 0 ? router.back() : setStep(s => s - 1))}
          disabled={isCreating}
        >
          <ArrowLeft className="h-4 w-4 mr-1" />
          {step === 0 ? 'Annulla' : 'Indietro'}
        </Button>

        {step < totalSteps - 1 ? (
          <Button onClick={() => setStep(s => s + 1)} disabled={!canProceed()}>
            Avanti
            <ArrowRight className="h-4 w-4 ml-1" />
          </Button>
        ) : (
          <Button
            onClick={handleCreate}
            disabled={!canProceed() || isCreating}
            className="bg-indigo-600 hover:bg-indigo-700"
          >
            {isCreating ? (
              <>
                <Loader2 className="h-4 w-4 mr-1 animate-spin" />
                Creazione...
              </>
            ) : (
              <>
                <Check className="h-4 w-4 mr-1" />
                Crea Sessione
              </>
            )}
          </Button>
        )}
      </div>
    </div>
  );
}

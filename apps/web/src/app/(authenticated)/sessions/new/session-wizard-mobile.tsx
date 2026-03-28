/**
 * SessionWizardMobile — 3-step mobile wizard for creating a game session
 *
 * Phase 5: Game Night — Task 3
 *
 * Steps:
 * 1. Choose Game — pick from library games
 * 2. Add Players — name + color
 * 3. Ready — summary + start
 *
 * API flow:
 *   createSession → addPlayer (per player) → navigate to /sessions/live/{id}
 */

'use client';

import { useState, useCallback, useMemo } from 'react';

import { Search, Check, Plus, Trash2, Gamepad2, Users, Rocket } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { api } from '@/lib/api';
import type { PlayerColor } from '@/lib/api/schemas/live-sessions.schemas';
import { cn } from '@/lib/utils';

// ========== Types ==========

interface WizardPlayer {
  id: string;
  displayName: string;
  color: PlayerColor;
}

type WizardStep = 1 | 2 | 3;

// ========== Constants ==========

const COLOR_PALETTE: { value: PlayerColor; label: string; hex: string; className: string }[] = [
  { value: 'Red', label: 'Rosso', hex: '#ef4444', className: 'bg-red-500' },
  { value: 'Blue', label: 'Blu', hex: '#3b82f6', className: 'bg-blue-500' },
  { value: 'Green', label: 'Verde', hex: '#22c55e', className: 'bg-green-500' },
  { value: 'Yellow', label: 'Giallo', hex: '#eab308', className: 'bg-yellow-400' },
  { value: 'Purple', label: 'Viola', hex: '#a855f7', className: 'bg-purple-500' },
  { value: 'Orange', label: 'Arancione', hex: '#f97316', className: 'bg-orange-500' },
];

const STEP_ICONS: Record<WizardStep, React.ElementType> = {
  1: Gamepad2,
  2: Users,
  3: Rocket,
};

// ========== Component ==========

export function SessionWizardMobile() {
  const router = useRouter();
  const [step, setStep] = useState<WizardStep>(1);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);
  const [selectedGameName, setSelectedGameName] = useState('');
  const [players, setPlayers] = useState<WizardPlayer[]>([
    { id: `p-${Date.now()}`, displayName: 'Giocatore 1', color: 'Red' },
  ]);
  const [newPlayerName, setNewPlayerName] = useState('');
  const [isCreating, setIsCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Fetch library games
  const { data: libraryData, isLoading: isLoadingLibrary } = useLibrary(
    { page: 1, pageSize: 100 },
    true
  );

  const filteredGames = useMemo(() => {
    const items = libraryData?.items ?? [];
    if (!searchQuery.trim()) return items;
    const q = searchQuery.toLowerCase();
    return items.filter(g => g.gameTitle.toLowerCase().includes(q));
  }, [libraryData, searchQuery]);

  // Color helpers
  const usedColors = players.map(p => p.color);
  const nextAvailableColor =
    COLOR_PALETTE.find(c => !usedColors.includes(c.value))?.value ?? COLOR_PALETTE[0].value;

  // Player management
  const addPlayer = useCallback(() => {
    const name = newPlayerName.trim() || `Giocatore ${players.length + 1}`;
    setPlayers(prev => [
      ...prev,
      { id: `p-${Date.now()}`, displayName: name, color: nextAvailableColor },
    ]);
    setNewPlayerName('');
  }, [newPlayerName, players.length, nextAvailableColor]);

  const removePlayer = useCallback((id: string) => {
    setPlayers(prev => prev.filter(p => p.id !== id));
  }, []);

  const changePlayerColor = useCallback((id: string, color: PlayerColor) => {
    setPlayers(prev => prev.map(p => (p.id === id ? { ...p, color } : p)));
  }, []);

  // Create session and navigate
  const handleStart = useCallback(async () => {
    if (!selectedGameName || players.length === 0) return;

    setIsCreating(true);
    setError(null);

    try {
      // 1. Create session
      const sessionId = await api.liveSessions.createSession({
        gameName: selectedGameName,
        gameId: selectedGameId ?? undefined,
      });

      // 2. Add all players
      for (const player of players) {
        await api.liveSessions.addPlayer(sessionId, {
          displayName: player.displayName,
          color: player.color,
        });
      }

      // 3. Navigate to live session
      router.push(`/sessions/live/${sessionId}`);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Errore nella creazione della sessione';
      setError(msg);
      setIsCreating(false);
    }
  }, [selectedGameName, selectedGameId, players, router]);

  // Navigation
  const canProceedStep1 = !!selectedGameName;
  const canProceedStep2 = players.length > 0;

  return (
    <div className="flex flex-col min-h-[60vh]">
      {/* Progress dots */}
      <div className="flex items-center justify-center gap-3 py-4">
        {([1, 2, 3] as WizardStep[]).map(s => {
          const Icon = STEP_ICONS[s];
          const isActive = s === step;
          const isDone = s < step;
          return (
            <button
              key={s}
              onClick={() => {
                if (s < step) setStep(s);
              }}
              disabled={s > step}
              className={cn(
                'flex items-center justify-center h-10 w-10 rounded-full transition-all',
                isActive && 'bg-amber-500 text-white shadow-md scale-110',
                isDone && 'bg-amber-500/20 text-amber-600',
                !isActive && !isDone && 'bg-slate-200 dark:bg-slate-700 text-slate-400'
              )}
              aria-label={`Passo ${s}`}
            >
              {isDone ? <Check className="h-4 w-4" /> : <Icon className="h-4 w-4" />}
            </button>
          );
        })}
      </div>

      {/* Step content */}
      <div className="flex-1 px-1">
        {/* ——— Step 1: Choose Game ——— */}
        {step === 1 && (
          <div className="space-y-4">
            <div>
              <h2 className="text-lg font-bold font-quicksand">Scegli un gioco</h2>
              <p className="text-sm text-muted-foreground">Seleziona un gioco dalla tua libreria</p>
            </div>

            {/* Search */}
            <div className="relative">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Cerca gioco..."
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
                className="pl-10"
                aria-label="Cerca gioco"
              />
            </div>

            {/* Game grid */}
            {isLoadingLibrary ? (
              <div className="flex items-center justify-center py-12">
                <p className="text-sm text-muted-foreground">Caricamento libreria...</p>
              </div>
            ) : filteredGames.length === 0 ? (
              <div className="flex items-center justify-center py-12">
                <p className="text-sm text-muted-foreground">
                  {searchQuery ? 'Nessun gioco trovato' : 'La tua libreria e vuota'}
                </p>
              </div>
            ) : (
              <div className="grid grid-cols-2 gap-2 max-h-[50vh] overflow-y-auto">
                {filteredGames.map(game => {
                  const isSelected = selectedGameId === game.gameId;
                  return (
                    <button
                      key={game.gameId}
                      onClick={() => {
                        setSelectedGameId(game.gameId);
                        setSelectedGameName(game.gameTitle);
                      }}
                      className={cn(
                        'flex flex-col items-center gap-2 rounded-xl border p-3 text-left transition-all',
                        isSelected
                          ? 'border-amber-500 bg-amber-500/10 ring-2 ring-amber-500/30'
                          : 'border-border bg-card hover:border-amber-300'
                      )}
                    >
                      {game.gameImageUrl ? (
                        <img
                          src={game.gameImageUrl}
                          alt={game.gameTitle}
                          className="h-16 w-16 rounded-lg object-cover"
                        />
                      ) : (
                        <div className="h-16 w-16 rounded-lg bg-slate-200 dark:bg-slate-700 flex items-center justify-center">
                          <Gamepad2 className="h-6 w-6 text-muted-foreground" />
                        </div>
                      )}
                      <span className="text-xs font-medium text-center line-clamp-2">
                        {game.gameTitle}
                      </span>
                      {isSelected && <Check className="h-4 w-4 text-amber-500" />}
                    </button>
                  );
                })}
              </div>
            )}

            {/* Continue */}
            <GradientButton
              fullWidth
              size="lg"
              disabled={!canProceedStep1}
              onClick={() => setStep(2)}
            >
              Avanti
            </GradientButton>
          </div>
        )}

        {/* ——— Step 2: Add Players ——— */}
        {step === 2 && (
          <div className="space-y-4">
            <div>
              <h2 className="text-lg font-bold font-quicksand">Aggiungi giocatori</h2>
              <p className="text-sm text-muted-foreground">
                Chi gioca stasera? Assegna nomi e colori.
              </p>
            </div>

            {/* Player list */}
            <div className="space-y-2" role="list" aria-label="Lista giocatori">
              {players.map(player => {
                const colorInfo = COLOR_PALETTE.find(c => c.value === player.color);
                return (
                  <div
                    key={player.id}
                    role="listitem"
                    className="flex items-center gap-3 rounded-xl border border-border bg-card p-3"
                  >
                    {/* Color dot */}
                    <div
                      className={cn(
                        'h-8 w-8 rounded-full shrink-0',
                        colorInfo?.className ?? 'bg-gray-400'
                      )}
                    />

                    {/* Name */}
                    <span className="flex-1 font-medium text-sm truncate">
                      {player.displayName}
                    </span>

                    {/* Color selector */}
                    <div className="flex gap-1">
                      {COLOR_PALETTE.filter(
                        c => c.value === player.color || !usedColors.includes(c.value)
                      )
                        .slice(0, 4)
                        .map(c => (
                          <button
                            key={c.value}
                            type="button"
                            onClick={() => changePlayerColor(player.id, c.value)}
                            className={cn(
                              'h-5 w-5 rounded-full transition-all',
                              c.className,
                              c.value === player.color && 'ring-2 ring-offset-1 ring-amber-500'
                            )}
                            title={c.label}
                            aria-label={`Colore ${c.label}`}
                          />
                        ))}
                    </div>

                    {/* Remove */}
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => removePlayer(player.id)}
                      aria-label={`Rimuovi ${player.displayName}`}
                    >
                      <Trash2 className="h-4 w-4 text-muted-foreground" />
                    </Button>
                  </div>
                );
              })}
            </div>

            {/* Add player input */}
            <div className="flex gap-2">
              <Input
                placeholder="Nome giocatore"
                value={newPlayerName}
                onChange={e => setNewPlayerName(e.target.value)}
                className="flex-1"
                onKeyDown={e => e.key === 'Enter' && addPlayer()}
                aria-label="Nome nuovo giocatore"
              />
              <Button variant="outline" onClick={addPlayer}>
                <Plus className="h-4 w-4 mr-1" />
                Aggiungi
              </Button>
            </div>

            {/* Navigation */}
            <div className="flex gap-3">
              <Button variant="outline" className="flex-1" onClick={() => setStep(1)}>
                Indietro
              </Button>
              <GradientButton
                fullWidth
                size="lg"
                disabled={!canProceedStep2}
                onClick={() => setStep(3)}
                className="flex-1"
              >
                Avanti
              </GradientButton>
            </div>
          </div>
        )}

        {/* ——— Step 3: Ready ——— */}
        {step === 3 && (
          <div className="space-y-6">
            <div>
              <h2 className="text-lg font-bold font-quicksand">Tutto pronto!</h2>
              <p className="text-sm text-muted-foreground">Controlla e inizia la partita.</p>
            </div>

            {/* Summary */}
            <div className="rounded-xl border border-border bg-card p-4 space-y-4">
              {/* Game */}
              <div className="flex items-center gap-3">
                <div className="h-10 w-10 rounded-lg bg-amber-500/10 flex items-center justify-center">
                  <Gamepad2 className="h-5 w-5 text-amber-600" />
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">Gioco</p>
                  <p className="font-semibold text-sm">{selectedGameName}</p>
                </div>
              </div>

              {/* Players */}
              <div>
                <p className="text-xs text-muted-foreground mb-2">Giocatori ({players.length})</p>
                <div className="flex flex-wrap gap-2">
                  {players.map(player => {
                    const colorInfo = COLOR_PALETTE.find(c => c.value === player.color);
                    return (
                      <div
                        key={player.id}
                        className="flex items-center gap-1.5 rounded-full bg-slate-100 dark:bg-slate-800 px-3 py-1"
                      >
                        <div
                          className={cn(
                            'h-3 w-3 rounded-full',
                            colorInfo?.className ?? 'bg-gray-400'
                          )}
                        />
                        <span className="text-xs font-medium">{player.displayName}</span>
                      </div>
                    );
                  })}
                </div>
              </div>
            </div>

            {/* Error */}
            {error && (
              <p className="text-sm text-red-600 text-center" role="alert">
                {error}
              </p>
            )}

            {/* Actions */}
            <div className="flex gap-3">
              <Button variant="outline" className="flex-1" onClick={() => setStep(2)}>
                Indietro
              </Button>
              <GradientButton
                fullWidth
                size="lg"
                loading={isCreating}
                disabled={isCreating}
                onClick={handleStart}
                className="flex-1"
              >
                Inizia a Giocare
              </GradientButton>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

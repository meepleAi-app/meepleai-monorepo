/**
 * SessionWizardMobile — 5-step mobile wizard for creating a game session
 *
 * Phase 5: Game Night — Task 3
 * Game Session Flow v2.0 — Task 14 (added phases step)
 * S1/S2 Wizard Prefill — Task 4/5/6 (prefill, turn order step, updateTurnOrder)
 *
 * Steps:
 * 1. Choose Game — pick from library games (skipped if prefilledGameId)
 * 2. Add Players — name + color
 * 3. Turn Order — reorder players before game starts
 * 4. Configure Phases — pre-loaded templates, optional
 * 5. Ready — summary + start
 *
 * API flow:
 *   createSession → addPlayer (per player) → updateTurnOrder → configurePhases (if any) → navigate to /sessions/live/{id}
 */

'use client';

import { useState, useCallback, useMemo, useEffect } from 'react';

import {
  Search,
  Check,
  Plus,
  Trash2,
  Gamepad2,
  Users,
  Rocket,
  Layers,
  X,
  ArrowUpDown,
  ChevronUp,
  ChevronDown,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { api } from '@/lib/api';
import type { PhaseTemplateDto } from '@/lib/api/clients/gamesClient';
import type { PlayerColor } from '@/lib/api/schemas/live-sessions.schemas';
import { cn } from '@/lib/utils';

// ========== Types ==========

interface WizardPlayer {
  id: string;
  displayName: string;
  color: PlayerColor;
}

interface WizardPhase {
  localId: string;
  phaseName: string;
}

type WizardStep = 1 | 2 | 3 | 4 | 5;

// ========== Props ==========

interface SessionWizardMobileProps {
  prefilledGameId?: string;
  prefilledGameName?: string;
}

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
  3: ArrowUpDown,
  4: Layers,
  5: Rocket,
};

export function SessionWizardMobile({
  prefilledGameId,
  prefilledGameName,
}: SessionWizardMobileProps = {}) {
  const router = useRouter();
  const [step, setStep] = useState<WizardStep>(prefilledGameId ? 2 : 1);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedGameId, setSelectedGameId] = useState<string | null>(prefilledGameId ?? null);
  const [selectedGameName, setSelectedGameName] = useState(prefilledGameName ?? '');
  const [players, setPlayers] = useState<WizardPlayer[]>([
    { id: `p-${Date.now()}`, displayName: 'Giocatore 1', color: 'Red' },
  ]);
  const [newPlayerName, setNewPlayerName] = useState('');
  const [isCreating, setIsCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Phase templates
  const [phaseTemplates, setPhaseTemplates] = useState<PhaseTemplateDto[]>([]);
  const [phases, setPhases] = useState<WizardPhase[]>([]);
  const [isLoadingPhases, setIsLoadingPhases] = useState(false);

  // Load phase templates when game is selected
  useEffect(() => {
    if (!selectedGameId) {
      setPhaseTemplates([]);
      setPhases([]);
      return;
    }
    setIsLoadingPhases(true);
    api.games
      .getPhaseTemplates(selectedGameId)
      .then(templates => {
        const sorted = [...templates].sort((a, b) => a.phaseOrder - b.phaseOrder);
        setPhaseTemplates(sorted);
        setPhases(sorted.map(t => ({ localId: t.id, phaseName: t.phaseName })));
      })
      .catch(() => {
        setPhaseTemplates([]);
        setPhases([]);
      })
      .finally(() => setIsLoadingPhases(false));
  }, [selectedGameId]);

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

  const movePlayerUp = useCallback((id: string) => {
    setPlayers(prev => {
      const idx = prev.findIndex(p => p.id === id);
      if (idx <= 0) return prev;
      const next = [...prev];
      [next[idx - 1], next[idx]] = [next[idx], next[idx - 1]];
      return next;
    });
  }, []);

  const movePlayerDown = useCallback((id: string) => {
    setPlayers(prev => {
      const idx = prev.findIndex(p => p.id === id);
      if (idx >= prev.length - 1) return prev;
      const next = [...prev];
      [next[idx], next[idx + 1]] = [next[idx + 1], next[idx]];
      return next;
    });
  }, []);

  // Phase management
  const addPhase = useCallback(() => {
    setPhases(prev => [...prev, { localId: `new-${Date.now()}`, phaseName: '' }]);
  }, []);

  const removePhase = useCallback((localId: string) => {
    setPhases(prev => prev.filter(p => p.localId !== localId));
  }, []);

  const updatePhaseName = useCallback((localId: string, name: string) => {
    setPhases(prev => prev.map(p => (p.localId === localId ? { ...p, phaseName: name } : p)));
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

      // 2. Add all players in turn order — collect returned playerIds
      const addedPlayerIds: string[] = [];
      for (const player of players) {
        try {
          const playerId = await api.liveSessions.addPlayer(sessionId, {
            displayName: player.displayName,
            color: player.color,
          });
          if (playerId && typeof playerId === 'string') {
            addedPlayerIds.push(playerId);
          }
        } catch (playerErr) {
          const failedName = player.displayName;
          const remaining = players.length - addedPlayerIds.length;
          const msg = playerErr instanceof Error ? playerErr.message : 'Errore aggiunta giocatore';
          setError(
            `Errore aggiungendo "${failedName}" (${addedPlayerIds.length}/${players.length} aggiunti). ${msg}. ` +
              (remaining > 1 ? `${remaining - 1} giocatori restanti non aggiunti.` : '')
          );
          router.push(`/sessions/live/${sessionId}`);
          return;
        }
      }

      // 3. Set turn order — order of addedPlayerIds matches user-defined order from step 3
      if (addedPlayerIds.length > 1) {
        try {
          await api.liveSessions.updateTurnOrder(sessionId, { playerIds: addedPlayerIds });
        } catch {
          // Non-blocking — session proceeds with default order
        }
      }

      // 4. Configure phases if any defined
      const validPhases = phases.filter(p => p.phaseName.trim().length > 0);
      if (validPhases.length > 0) {
        try {
          await api.liveSessions.configurePhases(sessionId, {
            phaseNames: validPhases.map(p => p.phaseName.trim()),
          });
        } catch {
          // Non-blocking
        }
      }

      // 5. Navigate to live session
      router.push(`/sessions/live/${sessionId}`);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Errore nella creazione della sessione';
      setError(msg);
    } finally {
      setIsCreating(false);
    }
  }, [selectedGameName, selectedGameId, players, phases, router]);

  // Navigation
  const canProceedStep1 = !!selectedGameName;
  const canProceedStep2 = players.length > 0;

  return (
    <div className="flex flex-col min-h-[60vh]">
      {/* Progress dots */}
      <div className="flex items-center justify-center gap-3 py-4">
        {([1, 2, 3, 4, 5] as WizardStep[]).map(s => {
          const Icon = STEP_ICONS[s];
          const isActive = s === step;
          const isDone = s < step || (s === 1 && !!prefilledGameId);
          return (
            <button
              key={s}
              onClick={() => {
                if (s === 1 && !!prefilledGameId) return;
                if (s < step) setStep(s);
              }}
              disabled={s > step || (s === 1 && !!prefilledGameId)}
              className={cn(
                'flex items-center justify-center h-10 w-10 rounded-full transition-all',
                isActive && 'bg-amber-500 text-white shadow-md scale-110',
                isDone && !isActive && 'bg-amber-500/20 text-amber-600',
                !isActive && !isDone && 'bg-slate-200 dark:bg-slate-700 text-slate-400',
                s === 1 && !!prefilledGameId && 'opacity-50'
              )}
              aria-label={`Passo ${s}`}
            >
              {isDone && !isActive ? <Check className="h-4 w-4" /> : <Icon className="h-4 w-4" />}
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

            {/* Context pill — gioco pre-selezionato */}
            {prefilledGameId && selectedGameName && (
              <div className="flex items-center gap-2 rounded-xl bg-amber-500/10 border border-amber-500/30 px-3 py-2">
                <Gamepad2 className="h-4 w-4 text-amber-500 shrink-0" />
                <div>
                  <p className="text-[10px] text-muted-foreground uppercase tracking-wide">
                    Gioco selezionato
                  </p>
                  <p className="text-sm font-semibold text-amber-400">{selectedGameName}</p>
                </div>
              </div>
            )}

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

        {/* ——— Step 3: Ordine Turni ——— */}
        {step === 3 && (
          <div className="space-y-4">
            <div>
              <h2 className="text-lg font-bold font-quicksand">Ordine turni</h2>
              <p className="text-sm text-muted-foreground">
                Chi gioca per primo? Usa le frecce per riordinare.
              </p>
            </div>

            <div className="space-y-2" role="list" aria-label="Ordine dei turni">
              {players.map((player, index) => {
                const colorInfo = COLOR_PALETTE.find(c => c.value === player.color);
                const isFirst = index === 0;
                const isLast = index === players.length - 1;
                return (
                  <div
                    key={player.id}
                    role="listitem"
                    className={cn(
                      'flex items-center gap-3 rounded-xl border p-3 transition-colors',
                      isFirst ? 'border-amber-500/40 bg-amber-500/5' : 'border-border bg-card'
                    )}
                  >
                    {/* Position badge */}
                    <span className="text-xs font-bold text-amber-500 w-5 shrink-0 text-center">
                      {index + 1}°
                    </span>

                    {/* Color dot */}
                    <div
                      className={cn(
                        'h-7 w-7 rounded-full shrink-0',
                        colorInfo?.className ?? 'bg-gray-400'
                      )}
                    />

                    {/* Name */}
                    <span className="flex-1 font-medium text-sm truncate">
                      {player.displayName}
                    </span>

                    {/* Reorder buttons */}
                    <div className="flex flex-col gap-0.5 shrink-0">
                      <button
                        type="button"
                        disabled={isFirst}
                        onClick={() => movePlayerUp(player.id)}
                        aria-label={`Sposta in alto ${player.displayName}`}
                        className={cn(
                          'p-1 rounded transition-colors',
                          isFirst
                            ? 'opacity-20 cursor-not-allowed text-muted-foreground'
                            : 'text-muted-foreground hover:bg-white/10 hover:text-white'
                        )}
                      >
                        <ChevronUp className="h-4 w-4" />
                      </button>
                      <button
                        type="button"
                        disabled={isLast}
                        onClick={() => movePlayerDown(player.id)}
                        aria-label={`Sposta in basso ${player.displayName}`}
                        className={cn(
                          'p-1 rounded transition-colors',
                          isLast
                            ? 'opacity-20 cursor-not-allowed text-muted-foreground'
                            : 'text-muted-foreground hover:bg-white/10 hover:text-white'
                        )}
                      >
                        <ChevronDown className="h-4 w-4" />
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>

            {players.length === 1 && (
              <p className="text-xs text-muted-foreground text-center py-2">
                Un solo giocatore — ordine non applicabile.
              </p>
            )}

            {/* Navigation */}
            <div className="flex gap-3">
              <Button variant="outline" className="flex-1" onClick={() => setStep(2)}>
                Indietro
              </Button>
              <GradientButton fullWidth size="lg" onClick={() => setStep(4)} className="flex-1">
                Avanti
              </GradientButton>
            </div>
          </div>
        )}

        {/* ——— Step 4: Configure Phases ——— */}
        {step === 4 && (
          <div className="space-y-4">
            <div>
              <h2 className="text-lg font-bold font-quicksand">Configura le fasi</h2>
              <p className="text-sm text-muted-foreground">
                {phaseTemplates.length > 0
                  ? 'Fasi pre-caricate dalle impostazioni del gioco. Puoi modificarle o saltare.'
                  : 'Aggiungi le fasi del turno, oppure salta questo passo.'}
              </p>
            </div>

            {isLoadingPhases ? (
              <div className="flex items-center justify-center py-8">
                <p className="text-sm text-muted-foreground">Caricamento fasi...</p>
              </div>
            ) : (
              <>
                {/* Phase list */}
                <div className="space-y-2" role="list" aria-label="Fasi del turno">
                  {phases.length === 0 && (
                    <div className="flex items-center justify-center py-8 rounded-xl border border-dashed border-white/20">
                      <p className="text-sm text-muted-foreground">
                        Nessuna fase. Aggiungine una o salta.
                      </p>
                    </div>
                  )}
                  {phases.map((phase, idx) => (
                    <div
                      key={phase.localId}
                      role="listitem"
                      className="flex items-center gap-2 rounded-xl border border-white/10 bg-white/5 px-3 py-2"
                    >
                      <span className="text-xs font-mono text-muted-foreground w-4 shrink-0">
                        {idx + 1}
                      </span>
                      <div className="h-2 w-2 rounded-full bg-purple-400 shrink-0" aria-hidden />
                      <Input
                        value={phase.phaseName}
                        onChange={e => updatePhaseName(phase.localId, e.target.value)}
                        placeholder={`Fase ${idx + 1}`}
                        className="flex-1 h-8 text-sm"
                        aria-label={`Nome fase ${idx + 1}`}
                      />
                      <button
                        onClick={() => removePhase(phase.localId)}
                        className="p-1 rounded hover:bg-red-500/10 text-muted-foreground hover:text-red-400 shrink-0"
                        aria-label={`Rimuovi fase ${idx + 1}`}
                      >
                        <X className="h-4 w-4" />
                      </button>
                    </div>
                  ))}
                </div>

                <Button variant="outline" size="sm" onClick={addPhase} className="w-full">
                  <Plus className="h-4 w-4 mr-1.5" />
                  Aggiungi fase
                </Button>
              </>
            )}

            {/* Navigation */}
            <div className="flex gap-3">
              <Button variant="outline" className="flex-1" onClick={() => setStep(3)}>
                Indietro
              </Button>
              <GradientButton fullWidth size="lg" onClick={() => setStep(5)} className="flex-1">
                {phases.filter(p => p.phaseName.trim()).length > 0 ? 'Avanti' : 'Salta'}
              </GradientButton>
            </div>
          </div>
        )}

        {/* ——— Step 5: Ready ——— */}
        {step === 5 && (
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

              {/* Turn order summary */}
              {players.length > 1 && (
                <div>
                  <p className="text-xs text-muted-foreground mb-2">Ordine turni</p>
                  <div className="flex flex-wrap gap-2">
                    {players.map((player, index) => {
                      const colorInfo = COLOR_PALETTE.find(c => c.value === player.color);
                      return (
                        <div
                          key={player.id}
                          className="flex items-center gap-1.5 rounded-full bg-slate-100 dark:bg-slate-800 px-2.5 py-1"
                        >
                          <span className="text-[10px] font-bold text-amber-500">{index + 1}°</span>
                          <div
                            className={cn(
                              'h-2.5 w-2.5 rounded-full',
                              colorInfo?.className ?? 'bg-gray-400'
                            )}
                          />
                          <span className="text-xs font-medium">{player.displayName}</span>
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}

              {/* Phases (if any) */}
              {phases.filter(p => p.phaseName.trim()).length > 0 && (
                <div>
                  <p className="text-xs text-muted-foreground mb-2">
                    Fasi ({phases.filter(p => p.phaseName.trim()).length})
                  </p>
                  <div className="flex flex-wrap gap-1.5">
                    {phases
                      .filter(p => p.phaseName.trim())
                      .map((phase, idx) => (
                        <span
                          key={phase.localId}
                          className="text-xs bg-purple-500/15 text-purple-300 border border-purple-500/20 rounded-full px-2.5 py-0.5"
                        >
                          {idx + 1}. {phase.phaseName}
                        </span>
                      ))}
                  </div>
                </div>
              )}
            </div>

            {/* Error */}
            {error && (
              <p className="text-sm text-red-600 text-center" role="alert">
                {error}
              </p>
            )}

            {/* Actions */}
            <div className="flex gap-3">
              <Button variant="outline" className="flex-1" onClick={() => setStep(4)}>
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

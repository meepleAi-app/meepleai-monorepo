'use client';

/**
 * NewPlayRecordSheet — BottomSheet 3-step per registrare una nuova partita
 *
 * Step 1 — Gioco: picker dalla libreria + data + visibilità
 * Step 2 — Giocatori: righe player con punteggio inline + vincitore auto
 * Step 3 — Riepilogo: classifica + note opzionali + Salva
 */

import { useEffect, useRef, useState } from 'react';

import { ChevronLeft, Plus, Trophy, X } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { GameCombobox } from '@/components/play-records/GameCombobox';
import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { playRecordsApi } from '@/lib/api/play-records.api';
import type { PlayRecordVisibility } from '@/lib/api/schemas/play-records.schemas';
import { cn } from '@/lib/utils';

// ── Types ────────────────────────────────────────────────────────────────────

interface PlayerEntry {
  id: string; // client-side temp id
  name: string;
  score: string; // string per input, converte a number
}

export interface NewPlayRecordSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Pre-seleziona un gioco dalla library card */
  preselectedGameId?: string;
  preselectedGameName?: string;
}

// ── Step indicator ────────────────────────────────────────────────────────────

function StepIndicator({ current, total }: { current: number; total: number }) {
  return (
    <div className="flex items-center gap-1.5">
      {Array.from({ length: total }).map((_, i) => (
        <div
          key={i}
          className={cn(
            'h-1.5 rounded-full transition-all duration-200',
            i < current ? 'w-4 bg-emerald-400' : i === current ? 'w-6 bg-white' : 'w-4 bg-white/20'
          )}
        />
      ))}
    </div>
  );
}

// ── Component ────────────────────────────────────────────────────────────────

export function NewPlayRecordSheet({
  open,
  onOpenChange,
  preselectedGameId,
  preselectedGameName,
}: NewPlayRecordSheetProps) {
  const router = useRouter();
  const closeTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Step 0-indexed (0=Gioco, 1=Giocatori, 2=Riepilogo)
  const [step, setStep] = useState(0);
  const [saving, setSaving] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  // Step 1 — Gioco
  const [gameId, setGameId] = useState<string | undefined>(preselectedGameId);
  const [gameName, setGameName] = useState(preselectedGameName ?? '');
  const [sessionDate, setSessionDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [visibility, setVisibility] = useState<PlayRecordVisibility>('Private');

  // Step 2 — Giocatori
  const [players, setPlayers] = useState<PlayerEntry[]>([]);
  const [newPlayerName, setNewPlayerName] = useState('');

  // Step 3 — Riepilogo
  const [notes, setNotes] = useState('');

  // Pulizia timer su unmount
  useEffect(
    () => () => {
      if (closeTimerRef.current) clearTimeout(closeTimerRef.current);
    },
    []
  );

  // Reset quando il sheet viene riaperto
  useEffect(() => {
    if (open) {
      setStep(0);
      setErrorMsg(null);
      setSaving(false);
      setGameId(preselectedGameId);
      setGameName(preselectedGameName ?? '');
      setSessionDate(new Date().toISOString().slice(0, 10));
      setVisibility('Private');
      setPlayers([]);
      setNewPlayerName('');
      setNotes('');
    }
  }, [open, preselectedGameId, preselectedGameName]);

  const handleClose = () => {
    if (closeTimerRef.current) clearTimeout(closeTimerRef.current);
    onOpenChange(false);
  };

  // ── Helpers ─────────────────────────────────────────────────────────────────

  const winnerPlayer = players.reduce<PlayerEntry | null>((best, p) => {
    const score = parseFloat(p.score);
    if (isNaN(score)) return best;
    if (!best || score > parseFloat(best.score)) return p;
    return best;
  }, null);

  const addPlayer = () => {
    const trimmed = newPlayerName.trim();
    if (!trimmed) return;
    setPlayers(prev => [...prev, { id: crypto.randomUUID(), name: trimmed, score: '' }]);
    setNewPlayerName('');
  };

  const removePlayer = (id: string) => {
    setPlayers(prev => prev.filter(p => p.id !== id));
  };

  const updateScore = (id: string, score: string) => {
    setPlayers(prev => prev.map(p => (p.id === id ? { ...p, score } : p)));
  };

  const sortedPlayers = [...players].sort((a, b) => {
    const sa = parseFloat(a.score);
    const sb = parseFloat(b.score);
    if (isNaN(sa) && isNaN(sb)) return 0;
    if (isNaN(sa)) return 1;
    if (isNaN(sb)) return -1;
    return sb - sa;
  });

  const rankEmoji = (i: number) => ['🥇', '🥈', '🥉'][i] ?? `${i + 1}.`;

  // ── Salvataggio ──────────────────────────────────────────────────────────────

  const handleSave = async () => {
    if (!gameName.trim()) return;
    setSaving(true);
    setErrorMsg(null);

    try {
      // Crea il record
      const recordId = await playRecordsApi.createRecord({
        gameId: gameId ?? undefined,
        gameName: gameName.trim(),
        sessionDate,
        visibility,
      });

      // Aggiunge i giocatori sequenzialmente (il backend li assegna con UUID server)
      if (players.length > 0) {
        for (const p of players) {
          await playRecordsApi.addPlayer(recordId, { displayName: p.name });
        }

        // Recupera il record salvato per ottenere gli UUID server dei giocatori
        const savedRecord = await playRecordsApi.getRecord(recordId);
        const scoredPlayers = players.filter(
          p => p.score.trim() !== '' && !isNaN(parseFloat(p.score))
        );

        if (scoredPlayers.length > 0) {
          await Promise.allSettled(
            scoredPlayers.map(p => {
              const serverPlayer = savedRecord.players.find(sp => sp.displayName === p.name);
              if (!serverPlayer) return Promise.resolve();
              return playRecordsApi.recordScore(recordId, {
                playerId: serverPlayer.id,
                dimension: 'Total',
                value: parseFloat(p.score),
              });
            })
          );
        }
      }

      // Aggiorna le note se presenti
      const notesVal = notes.trim() || undefined;
      if (notesVal) {
        await playRecordsApi.updateRecord(recordId, { notes: notesVal });
      }

      handleClose();
      router.push(`/play-records/${recordId}`);
    } catch (err) {
      setErrorMsg(err instanceof Error ? err.message : 'Errore durante il salvataggio.');
      setSaving(false);
    }
  };

  // ── Render ───────────────────────────────────────────────────────────────────

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex flex-col justify-end">
      {/* Overlay */}
      <div
        className="absolute inset-0 bg-black/60 backdrop-blur-sm"
        onClick={handleClose}
        aria-hidden="true"
      />

      {/* Sheet */}
      <div
        className="relative z-10 flex flex-col rounded-t-2xl bg-[var(--gaming-bg-surface,#1a1a2e)] border-t border-white/10 max-h-[92dvh]"
        role="dialog"
        aria-modal="true"
        aria-label="Registra partita"
      >
        {/* Drag handle */}
        <div className="flex justify-center pt-3 pb-1">
          <div className="h-1 w-10 rounded-full bg-white/20" />
        </div>

        {/* Header */}
        <div className="flex items-center justify-between px-4 py-3">
          <div className="flex items-center gap-3">
            {step > 0 && (
              <button
                type="button"
                onClick={() => setStep(s => s - 1)}
                className="flex h-8 w-8 items-center justify-center rounded-full text-white/60 hover:bg-white/5"
                aria-label="Passo precedente"
              >
                <ChevronLeft className="h-5 w-5" />
              </button>
            )}
            <StepIndicator current={step} total={3} />
          </div>
          <button
            type="button"
            onClick={handleClose}
            className="flex h-8 w-8 items-center justify-center rounded-full text-white/50 hover:bg-white/5"
            aria-label="Chiudi"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Content scrollabile */}
        <div className="flex-1 overflow-y-auto px-4 pb-6">
          {/* ── STEP 1: Gioco ─────────────────────────────────────────────────── */}
          {step === 0 && (
            <div className="flex flex-col gap-5">
              <div>
                <p className="text-base font-bold text-white mb-1">Quale gioco hai giocato?</p>
                <p className="text-xs text-white/40">
                  Cerca nella tua libreria o inserisci il nome
                </p>
              </div>

              <div>
                <GameCombobox
                  value={gameId}
                  onSelect={(id, name) => {
                    setGameId(id);
                    setGameName(name);
                  }}
                  placeholder="Cerca nella libreria…"
                />
              </div>

              {/* Nome libero se non trovato in libreria */}
              {!gameId && (
                <div>
                  <label className="block text-xs font-semibold text-white/50 mb-1.5">
                    Nome gioco (testo libero)
                  </label>
                  <input
                    type="text"
                    value={gameName}
                    onChange={e => setGameName(e.target.value)}
                    placeholder="Es. Catan, Puerto Rico…"
                    className="w-full rounded-xl bg-white/5 border border-white/8 px-3 py-2.5 text-sm text-white placeholder:text-white/30 focus:outline-none focus:ring-1 focus:ring-white/20"
                    data-testid="game-name-input"
                  />
                </div>
              )}

              <div>
                <label className="block text-xs font-semibold text-white/50 mb-1.5">
                  📅 Data partita
                </label>
                <input
                  type="date"
                  value={sessionDate}
                  onChange={e => setSessionDate(e.target.value)}
                  className="w-full rounded-xl bg-white/5 border border-white/8 px-3 py-2.5 text-sm text-white focus:outline-none focus:ring-1 focus:ring-white/20"
                  data-testid="session-date-input"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-white/50 mb-1.5">
                  👁 Visibilità
                </label>
                <div className="flex gap-2">
                  {(['Private', 'Group'] as const).map(v => (
                    <button
                      key={v}
                      type="button"
                      onClick={() => setVisibility(v)}
                      className={cn(
                        'flex-1 rounded-xl border py-2.5 text-sm font-semibold transition-colors',
                        visibility === v
                          ? 'border-emerald-500/40 bg-emerald-500/15 text-emerald-400'
                          : 'border-white/8 bg-white/5 text-white/50 hover:border-white/15'
                      )}
                      data-testid={`visibility-${v.toLowerCase()}`}
                    >
                      {v === 'Private' ? '🔒 Privata' : '👥 Gruppo'}
                    </button>
                  ))}
                </div>
              </div>
            </div>
          )}

          {/* ── STEP 2: Giocatori ─────────────────────────────────────────────── */}
          {step === 1 && (
            <div className="flex flex-col gap-4">
              <div>
                <p className="text-base font-bold text-white mb-1">Chi ha giocato?</p>
                <p className="text-xs text-white/40 mb-0.5">
                  {gameName} •{' '}
                  {new Date(sessionDate + 'T12:00:00').toLocaleDateString('it-IT', {
                    day: 'numeric',
                    month: 'long',
                  })}
                </p>
              </div>

              {/* Lista giocatori */}
              <div className="flex flex-col gap-2">
                {players.map(p => (
                  <div
                    key={p.id}
                    className="flex items-center gap-3 rounded-xl bg-white/5 border border-white/8 px-3 py-2.5"
                  >
                    <span className="flex-1 text-sm font-semibold text-white truncate">
                      {p.name}
                    </span>
                    <div className="flex items-center gap-1.5">
                      <input
                        type="number"
                        value={p.score}
                        onChange={e => updateScore(p.id, e.target.value)}
                        placeholder="Pts"
                        className="w-16 rounded-lg bg-white/5 border border-white/8 px-2 py-1 text-center text-sm text-white placeholder:text-white/30 focus:outline-none focus:ring-1 focus:ring-white/20"
                        data-testid={`player-score-${p.name}`}
                      />
                      <button
                        type="button"
                        onClick={() => removePlayer(p.id)}
                        className="flex h-6 w-6 items-center justify-center rounded-full text-white/30 hover:text-red-400"
                        aria-label={`Rimuovi ${p.name}`}
                      >
                        <X className="h-3.5 w-3.5" />
                      </button>
                    </div>
                  </div>
                ))}
              </div>

              {/* Aggiungi giocatore */}
              <div className="flex gap-2">
                <input
                  type="text"
                  value={newPlayerName}
                  onChange={e => setNewPlayerName(e.target.value)}
                  onKeyDown={e => e.key === 'Enter' && addPlayer()}
                  placeholder="Nome giocatore…"
                  className="flex-1 rounded-xl bg-white/5 border border-white/8 px-3 py-2.5 text-sm text-white placeholder:text-white/30 focus:outline-none focus:ring-1 focus:ring-white/20"
                  data-testid="new-player-input"
                />
                <button
                  type="button"
                  onClick={addPlayer}
                  className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-xl bg-white/5 border border-white/8 text-white/60 hover:bg-white/10"
                  aria-label="Aggiungi giocatore"
                  data-testid="add-player-btn"
                >
                  <Plus className="h-5 w-5" />
                </button>
              </div>

              {/* Vincitore auto */}
              {winnerPlayer && (
                <div className="flex items-center gap-2 rounded-xl bg-amber-500/10 border border-amber-500/20 px-3 py-2.5">
                  <Trophy className="h-4 w-4 text-amber-400 flex-shrink-0" />
                  <p className="text-sm font-semibold text-amber-400">
                    Vincitore: <span className="text-white">{winnerPlayer.name}</span>
                    {winnerPlayer.score && (
                      <span className="text-amber-400/70 ml-1">({winnerPlayer.score} pts)</span>
                    )}
                  </p>
                </div>
              )}
            </div>
          )}

          {/* ── STEP 3: Riepilogo ─────────────────────────────────────────────── */}
          {step === 2 && (
            <div className="flex flex-col gap-5">
              <div>
                <p className="text-base font-bold text-white mb-0.5">Riepilogo partita</p>
                <p className="text-xs text-white/40">
                  {gameName} • {players.length} {players.length === 1 ? 'giocatore' : 'giocatori'}
                </p>
              </div>

              {/* Classifica */}
              {sortedPlayers.length > 0 && (
                <div className="flex flex-col gap-1.5">
                  {sortedPlayers.map((p, i) => (
                    <div
                      key={p.id}
                      className={cn(
                        'flex items-center justify-between rounded-xl px-3 py-2.5',
                        i === 0
                          ? 'bg-amber-500/10 border border-amber-500/20'
                          : 'bg-white/5 border border-white/8'
                      )}
                    >
                      <div className="flex items-center gap-2.5">
                        <span className="text-base">{rankEmoji(i)}</span>
                        <span
                          className={cn(
                            'text-sm font-semibold',
                            i === 0 ? 'text-amber-300' : 'text-white'
                          )}
                        >
                          {p.name}
                        </span>
                      </div>
                      {p.score && (
                        <span
                          className={cn(
                            'text-sm font-bold',
                            i === 0 ? 'text-amber-400' : 'text-white/60'
                          )}
                        >
                          {p.score} pts
                        </span>
                      )}
                    </div>
                  ))}
                </div>
              )}

              {/* Note */}
              <div>
                <label className="block text-xs font-semibold text-white/50 mb-1.5">
                  📝 Note (opzionale)
                </label>
                <textarea
                  value={notes}
                  onChange={e => setNotes(e.target.value)}
                  placeholder="Come è andata la partita?"
                  rows={3}
                  className="w-full rounded-xl bg-white/5 border border-white/8 px-3 py-2.5 text-sm text-white placeholder:text-white/30 focus:outline-none focus:ring-1 focus:ring-white/20 resize-none"
                  data-testid="notes-input"
                />
              </div>

              {/* Error */}
              {errorMsg && (
                <div
                  className="rounded-xl border border-red-500/20 bg-red-500/10 px-4 py-3 text-sm text-red-400"
                  data-testid="save-error"
                >
                  {errorMsg}
                </div>
              )}
            </div>
          )}
        </div>

        {/* CTA footer */}
        <div className="px-4 pb-6 pt-3 border-t border-white/5">
          {step < 2 ? (
            <GradientButton
              fullWidth
              size="lg"
              onClick={() => setStep(s => s + 1)}
              disabled={step === 0 && !gameName.trim()}
              data-testid="next-step-btn"
            >
              {step === 0 ? 'Continua →' : 'Continua →'}
            </GradientButton>
          ) : (
            <GradientButton
              fullWidth
              size="lg"
              onClick={handleSave}
              loading={saving}
              data-testid="save-record-btn"
            >
              {saving ? 'Salvataggio…' : '▶ Salva Partita'}
            </GradientButton>
          )}
        </div>
      </div>
    </div>
  );
}

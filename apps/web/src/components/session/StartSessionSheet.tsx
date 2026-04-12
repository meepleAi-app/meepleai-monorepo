'use client';

/**
 * StartSessionSheet — 2-step bottom sheet to start a session from a library game card.
 *
 * Step 1 — Giocatori: add/remove players with name + color
 * Step 2 — Ordine turni: drag to reorder turn order → Avvia Partita
 *
 * Game is pre-selected (passed as prop). Navigates to /sessions/live/{id} on start.
 */

import { useCallback, useState } from 'react';

import { GripVertical, Plus, Trash2, X } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { BottomSheet } from '@/components/ui/overlays/BottomSheet';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import type { PlayerColor } from '@/lib/api/schemas/live-sessions.schemas';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

interface SheetPlayer {
  id: string;
  name: string;
  color: PlayerColor;
}

type SheetStep = 'players' | 'order';

interface StartSessionSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  gameId: string;
  gameName: string;
}

// ============================================================================
// Constants
// ============================================================================

const COLOR_OPTIONS: { value: PlayerColor; hex: string; label: string }[] = [
  { value: 'Red', hex: '#ef4444', label: 'Rosso' },
  { value: 'Blue', hex: '#3b82f6', label: 'Blu' },
  { value: 'Green', hex: '#22c55e', label: 'Verde' },
  { value: 'Yellow', hex: '#eab308', label: 'Giallo' },
  { value: 'Purple', hex: '#a855f7', label: 'Viola' },
  { value: 'Orange', hex: '#f97316', label: 'Arancione' },
];

const STEP_LABELS: Record<SheetStep, string> = {
  players: 'Giocatori',
  order: 'Ordine turni',
};

// ============================================================================
// Component
// ============================================================================

export function StartSessionSheet({
  open,
  onOpenChange,
  gameId,
  gameName,
}: StartSessionSheetProps) {
  const router = useRouter();

  const [step, setStep] = useState<SheetStep>('players');
  const [players, setPlayers] = useState<SheetPlayer[]>([
    { id: `p-${Date.now()}`, name: 'Giocatore 1', color: 'Red' },
  ]);
  const [newName, setNewName] = useState('');
  const [isCreating, setIsCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Drag state for step 2
  const [dragIndex, setDragIndex] = useState<number | null>(null);

  const usedColors = players.map(p => p.color);
  const nextColor =
    COLOR_OPTIONS.find(c => !usedColors.includes(c.value))?.value ?? COLOR_OPTIONS[0].value;

  const handleClose = () => {
    onOpenChange(false);
    // Reset after close animation
    setTimeout(() => {
      setStep('players');
      setPlayers([{ id: `p-${Date.now()}`, name: 'Giocatore 1', color: 'Red' }]);
      setNewName('');
      setError(null);
    }, 300);
  };

  // ── Players step ────────────────────────────────────────────────────────────

  const addPlayer = useCallback(() => {
    const trimmed = newName.trim();
    if (!trimmed) return;
    setPlayers(prev => [...prev, { id: `p-${Date.now()}`, name: trimmed, color: nextColor }]);
    setNewName('');
  }, [newName, nextColor]);

  const removePlayer = (id: string) => setPlayers(prev => prev.filter(p => p.id !== id));

  const setColor = (id: string, color: PlayerColor) =>
    setPlayers(prev => prev.map(p => (p.id === id ? { ...p, color } : p)));

  // ── Order step ──────────────────────────────────────────────────────────────

  const handleDragStart = (idx: number) => setDragIndex(idx);
  const handleDragOver = (e: React.DragEvent) => e.preventDefault();
  const handleDrop = (targetIdx: number) => {
    if (dragIndex === null || dragIndex === targetIdx) {
      setDragIndex(null);
      return;
    }
    const reordered = [...players];
    const [moved] = reordered.splice(dragIndex, 1);
    reordered.splice(targetIdx, 0, moved);
    setPlayers(reordered);
    setDragIndex(null);
  };

  // ── Start session ───────────────────────────────────────────────────────────

  const handleStart = useCallback(async () => {
    if (players.length === 0) return;
    setIsCreating(true);
    setError(null);

    try {
      const sessionId = await api.liveSessions.createSession({
        gameName,
        gameId,
      });

      for (const player of players) {
        try {
          await api.liveSessions.addPlayer(sessionId, {
            displayName: player.name,
            color: player.color,
          });
        } catch {
          // Non-blocking — continue with remaining players
        }
      }

      router.push(`/sessions/live/${sessionId}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Impossibile avviare la sessione.');
      setIsCreating(false);
    }
  }, [players, gameId, gameName, router]);

  // ── Render ──────────────────────────────────────────────────────────────────

  const title = step === 'players' ? 'Aggiungi giocatori' : 'Ordine dei turni';

  return (
    <BottomSheet open={open} onOpenChange={handleClose} title={title} height="full">
      {/* Step indicator */}
      <div className="flex items-center gap-2 mb-4 -mt-1" data-testid="step-indicator">
        {(['players', 'order'] as SheetStep[]).map((s, i) => (
          <div key={s} className="flex items-center gap-2">
            {i > 0 && <div className="h-px w-6 bg-white/20" />}
            <div
              className={cn(
                'flex items-center gap-1.5 text-xs font-medium',
                step === s ? 'text-white' : 'text-white/40'
              )}
            >
              <span
                className={cn(
                  'flex h-5 w-5 items-center justify-center rounded-full text-[10px] font-bold',
                  step === s
                    ? 'bg-[hsl(142,70%,45%)] text-white'
                    : s === 'players' && step === 'order'
                      ? 'bg-white/20 text-white/60'
                      : 'bg-white/10 text-white/40'
                )}
              >
                {i + 1}
              </span>
              {STEP_LABELS[s]}
            </div>
          </div>
        ))}
      </div>

      {/* ── Step 1: Players ── */}
      {step === 'players' && (
        <div className="flex flex-col gap-4" data-testid="players-step">
          {/* Player list */}
          <div className="flex flex-col gap-2">
            {players.map(p => (
              <div
                key={p.id}
                className="flex items-center gap-2 rounded-xl bg-white/5 px-3 py-2.5"
                data-testid={`player-row-${p.id}`}
              >
                {/* Name */}
                <span className="flex-1 text-sm font-medium text-white">{p.name}</span>

                {/* Color picker */}
                <div className="flex items-center gap-1">
                  {COLOR_OPTIONS.map(c => (
                    <button
                      key={c.value}
                      type="button"
                      aria-label={c.label}
                      onClick={() => setColor(p.id, c.value)}
                      className={cn(
                        'h-5 w-5 rounded-full transition-transform',
                        p.color === c.value ? 'scale-125 ring-2 ring-white/60' : 'opacity-60'
                      )}
                      style={{ backgroundColor: c.hex }}
                    />
                  ))}
                </div>

                {/* Remove */}
                <button
                  type="button"
                  onClick={() => removePlayer(p.id)}
                  disabled={players.length <= 1}
                  aria-label={`Rimuovi ${p.name}`}
                  className="text-white/40 hover:text-red-400 disabled:opacity-20"
                  data-testid={`remove-player-${p.id}`}
                >
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
            ))}
          </div>

          {/* Add player form */}
          <div className="flex gap-2">
            <Input
              value={newName}
              onChange={e => setNewName(e.target.value)}
              onKeyDown={e => {
                if (e.key === 'Enter') addPlayer();
              }}
              placeholder="Nome giocatore…"
              className="flex-1 bg-white/10 border-white/20 text-white placeholder:text-white/40"
              data-testid="new-player-input"
            />
            <Button
              type="button"
              variant="outline"
              size="icon"
              onClick={addPlayer}
              disabled={!newName.trim()}
              aria-label="Aggiungi giocatore"
              className="border-white/20 text-white hover:bg-white/10"
              data-testid="add-player-btn"
            >
              <Plus className="h-4 w-4" />
            </Button>
          </div>

          {/* Next step */}
          <GradientButton
            fullWidth
            disabled={players.length === 0}
            onClick={() => setStep('order')}
            data-testid="next-step-btn"
          >
            Continua →
          </GradientButton>
        </div>
      )}

      {/* ── Step 2: Turn order ── */}
      {step === 'order' && (
        <div className="flex flex-col gap-4" data-testid="order-step">
          <p className="text-xs text-white/50">
            Trascina per cambiare l&apos;ordine dei turni. Il primo in lista inizia.
          </p>

          {/* Draggable list */}
          <div className="flex flex-col gap-2">
            {players.map((p, idx) => {
              const colorHex = COLOR_OPTIONS.find(c => c.value === p.color)?.hex ?? '#888';
              return (
                <div
                  key={p.id}
                  draggable
                  onDragStart={() => handleDragStart(idx)}
                  onDragOver={handleDragOver}
                  onDrop={() => handleDrop(idx)}
                  className={cn(
                    'flex items-center gap-3 rounded-xl bg-white/5 px-3 py-3 cursor-grab',
                    dragIndex === idx && 'opacity-40'
                  )}
                  data-testid={`order-row-${p.id}`}
                >
                  <span className="text-sm font-bold tabular-nums text-white/40 w-4">
                    {idx + 1}
                  </span>
                  <span
                    className="h-3 w-3 rounded-full shrink-0"
                    style={{ backgroundColor: colorHex }}
                  />
                  <span className="flex-1 text-sm font-medium text-white">{p.name}</span>
                  <GripVertical className="h-4 w-4 text-white/30" />
                </div>
              );
            })}
          </div>

          {error && (
            <p className="text-xs text-red-400" data-testid="session-error">
              {error}
            </p>
          )}

          {/* Actions */}
          <div className="flex gap-2">
            <Button
              type="button"
              variant="outline"
              className="border-white/20 text-white hover:bg-white/10"
              onClick={() => setStep('players')}
              data-testid="back-btn"
            >
              <X className="h-4 w-4 mr-1" />
              Indietro
            </Button>
            <GradientButton
              fullWidth
              disabled={isCreating}
              onClick={handleStart}
              data-testid="start-session-btn"
            >
              {isCreating ? 'Avvio…' : '▶ Avvia Partita'}
            </GradientButton>
          </div>
        </div>
      )}
    </BottomSheet>
  );
}

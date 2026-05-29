/**
 * SessionCreateForm — Wizard 3-step responsive
 *
 * Mobile  (<= 768px): wizard steps 1/2/3 with StepIndicator sticky top.
 * Desktop (> 768px):  split-form 8-col (fields) + 4-col (live preview).
 *
 * Step 1 — Gioco:    GameCombobox catalog + freeform fallback
 * Step 2 — Quando:   sessionDate + location (optional) + notes (optional)
 * Step 3 — Punteggi: PlayerManager (add/remove) + inline scoring
 *
 * AC-3.1 … AC-3.10 — Issue #1488 play-records reskin Task 3
 */

'use client';

import { useState, useId } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { ArrowLeft, ArrowRight, Check, Trophy, Users, X, Plus } from 'lucide-react';
import { useForm } from 'react-hook-form';

import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/forms/form';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { RadioGroup, RadioGroupItem } from '@/components/ui/primitives/radio-group';
import { Textarea } from '@/components/ui/primitives/textarea';
import { useTranslation } from '@/hooks/useTranslation';
import {
  SessionCreateFormSchema,
  type SessionCreateForm as SessionCreateFormData,
} from '@/lib/api/schemas/play-records.schemas';
import { useMediaQuery } from '@/lib/hooks/useMediaQuery';
import { usePlayRecordsStore } from '@/lib/stores/play-records-store';
import { cn } from '@/lib/utils';

import { GameCombobox } from './GameCombobox';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface SessionCreateFormProps {
  onSubmit: (data: SessionCreateFormData) => void;
  onCancel?: () => void;
  isSubmitting?: boolean;
  /**
   * AC-4.1/AC-4.3: mode='edit' enforces the K5 gate — game (Step 1) and
   * players/scores (Step 3) become readonly, leaving only sessionDate/notes/
   * location editable (Step 2). The gate is driven entirely by `mode`.
   */
  mode?: 'create' | 'edit';
}

interface PlayerEntry {
  id: string;
  name: string;
  score: string;
}

// ─── Constants ────────────────────────────────────────────────────────────────

const STEP_COUNT = 3;
const MOBILE_BREAKPOINT = '(max-width: 768px)';

const STEP_CONFIG = [
  { icon: Trophy, labelKey: 'step1Label' },
  { icon: Users, labelKey: 'step2Label' },
  { icon: Check, labelKey: 'step3Label' },
] as const;

// ─── StepIndicator ────────────────────────────────────────────────────────────

interface StepIndicatorProps {
  current: number; // 0-indexed
  onBack?: () => void;
  t: (k: string) => string;
}

function StepIndicator({ current, onBack, t }: StepIndicatorProps) {
  return (
    <nav
      aria-label={t('a11y.stepIndicatorLabel')}
      data-testid="step-indicator"
      className="sticky top-0 z-10 flex items-center gap-3 px-4 py-3 bg-background/90 backdrop-blur-[12px] border-b border-border"
    >
      {onBack && (
        <button
          type="button"
          onClick={onBack}
          aria-label={t('a11y.backButton')}
          className="flex h-8 w-8 items-center justify-center rounded-full text-foreground/80 hover:bg-muted flex-shrink-0"
        >
          <ArrowLeft className="h-4 w-4" />
        </button>
      )}

      <div className="flex items-center gap-2 flex-1 min-w-0">
        {STEP_CONFIG.map((step, idx) => {
          const isActive = idx === current;
          const isDone = idx < current;
          const StepIcon = step.icon;

          return (
            <div key={idx} className="flex items-center gap-1 min-w-0">
              <div className="flex items-center gap-1.5 min-w-0 flex-shrink-0">
                <span
                  aria-current={isActive ? 'step' : undefined}
                  className={cn(
                    'flex h-6 w-6 items-center justify-center rounded-full border-2 text-xs font-bold transition-colors',
                    isDone && 'bg-entity-session text-white border-entity-session',
                    isActive && 'border-entity-session text-entity-session',
                    !isDone && !isActive && 'border-border/40 text-muted-foreground'
                  )}
                >
                  {isDone ? <Check className="h-3 w-3" /> : <StepIcon className="h-3 w-3" />}
                </span>
                <span
                  className={cn(
                    'text-xs font-semibold whitespace-nowrap',
                    isActive ? 'text-foreground' : 'text-muted-foreground',
                    !isActive && 'hidden sm:inline'
                  )}
                >
                  {t(step.labelKey)}
                </span>
              </div>

              {idx < STEP_CONFIG.length - 1 && (
                <span
                  className={cn(
                    'flex-1 h-[2px] min-w-4 rounded-full',
                    isDone ? 'bg-entity-session' : 'bg-border/30'
                  )}
                />
              )}
            </div>
          );
        })}
      </div>

      <span className="text-xs text-muted-foreground font-mono font-bold flex-shrink-0 hidden sm:inline">
        {stepNum(current)}/{STEP_COUNT}
      </span>
    </nav>
  );
}

function stepNum(zero: number) {
  return zero + 1;
}

// ─── LivePreview (desktop only) ───────────────────────────────────────────────

interface LivePreviewProps {
  gameName: string;
  players: PlayerEntry[];
  t: (k: string) => string;
}

function LivePreview({ gameName, players, t }: LivePreviewProps) {
  const sortedPlayers = [...players]
    .filter(p => p.score !== '')
    .sort((a, b) => parseFloat(b.score) - parseFloat(a.score));

  return (
    <aside
      className="sticky top-24 flex flex-col gap-4"
      aria-label={t('a11y.previewLabel')}
      data-testid="live-preview"
    >
      <div className="flex items-center gap-2 font-mono text-[10px] font-bold text-muted-foreground uppercase tracking-widest">
        <span
          className="h-1.5 w-1.5 rounded-full bg-entity-session"
          style={{
            boxShadow: '0 0 0 3px hsl(var(--c-session) / 0.2)',
            animation: 'pulse 2s infinite',
          }}
          aria-hidden="true"
        />
        {t('preview.sectionLabel')}
      </div>

      <article className="rounded-2xl bg-card border border-border overflow-hidden shadow-md">
        {/* Cover section */}
        <div className="p-5 bg-entity-session/10 border-b border-border">
          <div className="flex items-center gap-2 mb-2">
            <span className="px-2 py-0.5 rounded-full bg-entity-session/20 text-entity-session font-mono text-[9px] font-bold uppercase tracking-widest border border-entity-session/30">
              Partita
            </span>
            <span className="px-2 py-0.5 rounded-full bg-entity-session/20 text-entity-session font-mono text-[9px] font-bold uppercase tracking-widest border border-entity-session/30">
              Bozza
            </span>
          </div>
          <h3 className="text-lg font-bold text-foreground">
            {gameName || t('preview.noGameSelected')}
          </h3>
        </div>

        {/* Ranking */}
        <div className="p-4">
          {players.length > 0 ? (
            <>
              <div className="font-mono text-[9px] font-bold text-muted-foreground uppercase tracking-widest mb-3">
                {t('preview.rankingLabel').replace('{count}', String(players.length))}
              </div>
              <div className="flex flex-col gap-1.5">
                {sortedPlayers.map((p, rank) => (
                  <div
                    key={p.id}
                    className={cn(
                      'flex items-center gap-2 px-2 py-1.5 rounded-lg',
                      rank === 0
                        ? 'bg-entity-session/8 border border-entity-session/20'
                        : 'bg-muted'
                    )}
                  >
                    <span className="font-mono text-xs font-bold text-muted-foreground w-4">
                      {rank + 1}
                    </span>
                    <span
                      className={cn(
                        'flex-1 text-sm font-semibold',
                        rank === 0 ? 'text-entity-session' : 'text-foreground'
                      )}
                    >
                      {p.name}
                      {rank === 0 ? ' 🏆' : ''}
                    </span>
                    <span
                      className={cn(
                        'font-mono text-sm font-bold tabular-nums',
                        rank === 0 ? 'text-entity-session' : 'text-muted-foreground'
                      )}
                    >
                      {p.score === '' ? '—' : p.score}
                    </span>
                  </div>
                ))}
                {/* Players without scores */}
                {players
                  .filter(p => p.score === '')
                  .map(p => (
                    <div
                      key={p.id}
                      className="flex items-center gap-2 px-2 py-1.5 rounded-lg bg-muted"
                    >
                      <span className="font-mono text-xs font-bold text-muted-foreground w-4">
                        —
                      </span>
                      <span className="flex-1 text-sm font-semibold text-foreground">{p.name}</span>
                      <span className="font-mono text-sm font-bold tabular-nums text-muted-foreground">
                        —
                      </span>
                    </div>
                  ))}
              </div>
            </>
          ) : (
            <p className="text-sm text-muted-foreground italic">{t('step3.noPlayersHint')}</p>
          )}
        </div>
      </article>

      <div className="rounded-lg bg-entity-session/6 border border-dashed border-entity-session/30 p-3 flex gap-2">
        <span className="text-sm flex-shrink-0" aria-hidden="true">
          💡
        </span>
        <p className="text-xs text-muted-foreground leading-relaxed">{t('preview.tip')}</p>
      </div>
    </aside>
  );
}

// ─── Step 1 — Gioco ───────────────────────────────────────────────────────────

interface Step1Props {
  form: ReturnType<typeof useForm<SessionCreateFormData>>;
  isSubmitting: boolean;
  t: (k: string) => string;
  /** K5 gate: disable game field in edit mode */
  isReadonly?: boolean;
}

function Step1Gioco({ form, isSubmitting, t, isReadonly }: Step1Props) {
  const gameType = form.watch('gameType');

  return (
    <div className="flex flex-col gap-5">
      <div>
        <h2 className="text-xl font-bold text-foreground">{t('step1.title')}</h2>
        <p className="text-sm text-muted-foreground mt-1">{t('step1.subtitle')}</p>
      </div>

      {/* Game source toggle — AC-4.3 disabled in edit mode */}
      <FormField
        control={form.control}
        name="gameType"
        render={({ field }) => (
          <FormItem>
            <FormControl>
              <RadioGroup
                onValueChange={field.onChange}
                defaultValue={field.value}
                className="flex gap-3"
                disabled={isReadonly}
              >
                <div className="flex items-center space-x-2">
                  <RadioGroupItem value="catalog" id="catalog" disabled={isReadonly} />
                  <Label
                    htmlFor="catalog"
                    className={isReadonly ? 'cursor-not-allowed opacity-50' : 'cursor-pointer'}
                  >
                    Libreria
                  </Label>
                </div>
                <div className="flex items-center space-x-2">
                  <RadioGroupItem value="freeform" id="freeform" disabled={isReadonly} />
                  <Label
                    htmlFor="freeform"
                    className={isReadonly ? 'cursor-not-allowed opacity-50' : 'cursor-pointer'}
                  >
                    Freeform
                  </Label>
                </div>
              </RadioGroup>
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />

      {gameType === 'catalog' ? (
        <FormField
          control={form.control}
          name="gameId"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('step1.searchPlaceholder')}</FormLabel>
              <FormControl>
                <GameCombobox
                  value={field.value}
                  onSelect={(gameId, gameName) => {
                    field.onChange(gameId);
                    form.setValue('gameName', gameName);
                  }}
                  disabled={isSubmitting || isReadonly}
                  placeholder={t('step1.searchPlaceholder')}
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
      ) : (
        <FormField
          control={form.control}
          name="gameName"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('step1.freeformLabel')}</FormLabel>
              <FormControl>
                <Input
                  placeholder={t('step1.freeformPlaceholder')}
                  disabled={isReadonly}
                  readOnly={isReadonly}
                  aria-readonly={isReadonly}
                  aria-describedby="freeform-hint"
                  {...field}
                />
              </FormControl>
              <FormDescription id="freeform-hint">{t('step1.freeformHint')}</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
      )}

      {/* AC-3.10: If catalog selected but nothing chosen, show hint */}
      {gameType === 'catalog' && !form.watch('gameId') && (
        <div
          role="status"
          className="rounded-lg border border-dashed border-entity-game/30 bg-entity-game/6 px-3 py-2.5 font-mono text-xs font-bold text-entity-game"
        >
          {t('step1.emptyLibraryHint')}
        </div>
      )}
    </div>
  );
}

// ─── Step 2 — Quando ──────────────────────────────────────────────────────────

interface Step2Props {
  form: ReturnType<typeof useForm<SessionCreateFormData>>;
  t: (k: string) => string;
}

function Step2Quando({ form, t }: Step2Props) {
  return (
    <div className="flex flex-col gap-5">
      <div>
        <h2 className="text-xl font-bold text-foreground">{t('step2.title')}</h2>
        <p className="text-sm text-muted-foreground mt-1">{t('step2.subtitle')}</p>
      </div>

      {/* Date picker */}
      <FormField
        control={form.control}
        name="sessionDate"
        render={({ field }) => (
          <FormItem className="flex flex-col">
            <FormLabel>{t('step2.dateLabel')}</FormLabel>
            <FormControl>
              <Input
                type="datetime-local"
                value={field.value instanceof Date ? field.value.toISOString().slice(0, 16) : ''}
                onChange={e => field.onChange(new Date(e.target.value))}
                max={new Date().toISOString().slice(0, 16)}
              />
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />

      {/* Location (optional) */}
      <FormField
        control={form.control}
        name="location"
        render={({ field }) => (
          <FormItem>
            <FormLabel>{t('step2.locationLabel')}</FormLabel>
            <FormControl>
              <Input placeholder={t('step2.locationPlaceholder')} {...field} />
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />

      {/* Notes (optional) */}
      <FormField
        control={form.control}
        name="notes"
        render={({ field }) => (
          <FormItem>
            <FormLabel>{t('step2.notesLabel')}</FormLabel>
            <FormControl>
              <Textarea
                placeholder={t('step2.notesPlaceholder')}
                className="resize-none"
                rows={3}
                {...field}
              />
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />
    </div>
  );
}

// ─── Step 3 — Punteggi ───────────────────────────────────────────────────────

interface Step3Props {
  players: PlayerEntry[];
  newPlayerName: string;
  onNewPlayerNameChange: (v: string) => void;
  onAddPlayer: () => void;
  onRemovePlayer: (id: string) => void;
  onUpdateScore: (id: string, score: string) => void;
  t: (k: string) => string;
  /** K5 gate: disable player add/remove in edit mode */
  isReadonly?: boolean;
}

function Step3Punteggi({
  players,
  newPlayerName,
  onNewPlayerNameChange,
  onAddPlayer,
  onRemovePlayer,
  onUpdateScore,
  t,
  isReadonly,
}: Step3Props) {
  const winnerIdx = (() => {
    let best = -Infinity;
    let idx = -1;
    players.forEach((p, i) => {
      const v = parseFloat(p.score);
      if (!isNaN(v) && v > best) {
        best = v;
        idx = i;
      }
    });
    return idx;
  })();

  return (
    <div className="flex flex-col gap-5">
      <div>
        <h2 className="text-xl font-bold text-foreground">{t('step3.title')}</h2>
        <p className="text-sm text-muted-foreground mt-1">{t('step3.subtitle')}</p>
      </div>

      {/* Players label */}
      <div className="font-mono text-[10px] font-bold text-muted-foreground uppercase tracking-widest">
        {t('step3.playersLabel').replace('{count}', String(players.length))}
      </div>

      {/* Player list */}
      <div className="flex flex-col gap-2">
        {players.map((p, idx) => {
          const isWinner = idx === winnerIdx && p.score !== '';
          return (
            <div
              key={p.id}
              className={cn(
                'flex items-center gap-3 rounded-xl px-3 py-2.5',
                isWinner
                  ? 'bg-entity-session/8 border border-entity-session/30'
                  : 'bg-card border border-border'
              )}
            >
              <span className="flex-1 text-sm font-semibold text-foreground truncate">
                {p.name}
                {isWinner && (
                  <span className="ml-2 inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full bg-entity-session/15 text-entity-session font-mono text-[8px] font-bold uppercase tracking-widest border border-entity-session/25">
                    🏆 {t('step3.winnerBadge')}
                  </span>
                )}
              </span>
              <div className="flex items-center gap-1.5">
                <input
                  type="number"
                  value={p.score}
                  onChange={e => onUpdateScore(p.id, e.target.value)}
                  placeholder="Pts"
                  disabled={isReadonly}
                  readOnly={isReadonly}
                  aria-readonly={isReadonly}
                  className={cn(
                    'w-16 rounded-lg bg-muted border border-border px-2 py-1 text-center text-sm font-mono font-bold text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring/30',
                    isReadonly && 'opacity-60 cursor-not-allowed'
                  )}
                  aria-label={`Punteggio di ${p.name}`}
                  data-testid={`player-score-${p.name}`}
                />
                <button
                  type="button"
                  onClick={() => onRemovePlayer(p.id)}
                  disabled={isReadonly}
                  className={cn(
                    'flex h-6 w-6 items-center justify-center rounded-full text-muted-foreground hover:text-destructive',
                    isReadonly && 'opacity-40 cursor-not-allowed hover:text-muted-foreground'
                  )}
                  aria-label={`Rimuovi ${p.name}`}
                >
                  <X className="h-3.5 w-3.5" />
                </button>
              </div>
            </div>
          );
        })}
      </div>

      {/* Add player input — AC-4.3 disabled in edit mode */}
      <div className="flex gap-2">
        <input
          type="text"
          value={newPlayerName}
          onChange={e => onNewPlayerNameChange(e.target.value)}
          onKeyDown={e => {
            if (e.key === 'Enter') {
              e.preventDefault();
              onAddPlayer();
            }
          }}
          disabled={isReadonly}
          placeholder={t('step3.addPlayerPlaceholder')}
          className={cn(
            'flex-1 rounded-xl bg-muted border border-border px-3 py-2.5 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring/30',
            isReadonly && 'opacity-50 cursor-not-allowed'
          )}
          data-testid="new-player-input"
        />
        <button
          type="button"
          onClick={onAddPlayer}
          disabled={isReadonly}
          className={cn(
            'flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-xl bg-muted border border-border text-foreground/80 hover:bg-muted/80',
            isReadonly && 'opacity-40 cursor-not-allowed hover:bg-muted'
          )}
          aria-label={t('step3.addPlayerButton')}
          data-testid="add-player-btn"
        >
          <Plus className="h-5 w-5" />
        </button>
      </div>

      {/* Empty hint */}
      {players.length === 0 && (
        <p className="text-xs text-muted-foreground italic">{t('step3.noPlayersHint')}</p>
      )}
    </div>
  );
}

// ─── Main form ────────────────────────────────────────────────────────────────

export function SessionCreateForm({
  onSubmit,
  onCancel,
  isSubmitting = false,
  mode = 'create',
}: SessionCreateFormProps) {
  const { t: tRaw } = useTranslation();
  const ns = mode === 'edit' ? 'playRecords.edit' : 'playRecords.new';
  const t = (key: string, params?: Record<string, string | number>) =>
    params ? tRaw(`${ns}.${key}`, params) : tRaw(`${ns}.${key}`);
  const isMobile = useMediaQuery(MOBILE_BREAKPOINT);
  const formId = useId();

  const { sessionCreation, nextStep, prevStep, resetSessionCreation } = usePlayRecordsStore();
  const currentStep = sessionCreation.currentStep; // 0-indexed

  // Local player state (Step 3)
  const [players, setPlayers] = useState<PlayerEntry[]>([]);
  const [newPlayerName, setNewPlayerName] = useState('');

  const form = useForm<SessionCreateFormData>({
    resolver: zodResolver(SessionCreateFormSchema),
    defaultValues: {
      gameType: 'catalog',
      gameName: '',
      sessionDate: new Date(),
      visibility: 'Private',
      enableScoring: false,
      scoringDimensions: [],
      notes: '',
      location: '',
    },
  });

  // ── Step field maps for validation ────────────────────────────────────────
  const STEP_FIELDS: Array<Array<keyof SessionCreateFormData>> = [
    ['gameType', 'gameId', 'gameName'],
    ['sessionDate'],
    ['enableScoring', 'scoringDimensions'],
  ];

  const handleNext = async () => {
    const fields = STEP_FIELDS[currentStep] ?? [];
    const isValid = await form.trigger(fields);
    if (!isValid) return;

    // Step 0: also enforce gameName non-empty (schema allows empty for multi-step flexibility)
    if (currentStep === 0) {
      const gameName = form.getValues('gameName');
      if (!gameName?.trim()) {
        form.setError('gameName', { type: 'manual', message: 'Il nome del gioco è obbligatorio' });
        return;
      }
    }

    nextStep();
  };

  const handlePrev = () => {
    prevStep();
  };

  const handleCancel = () => {
    resetSessionCreation();
    form.reset();
    setPlayers([]);
    setNewPlayerName('');
    onCancel?.();
  };

  const handleFormSubmit = form.handleSubmit(data => {
    onSubmit(data);
    resetSessionCreation();
    form.reset();
    setPlayers([]);
    setNewPlayerName('');
  });

  // ── Player helpers ───────────────────────────────────────────────────────
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

  // ── Step content ─────────────────────────────────────────────────────────
  const isReadonlyMode = mode === 'edit';
  const renderStepContent = () => {
    if (currentStep === 0) {
      return (
        <Step1Gioco form={form} isSubmitting={isSubmitting} t={t} isReadonly={isReadonlyMode} />
      );
    }
    if (currentStep === 1) return <Step2Quando form={form} t={t} />;
    return (
      <Step3Punteggi
        players={players}
        newPlayerName={newPlayerName}
        onNewPlayerNameChange={setNewPlayerName}
        onAddPlayer={addPlayer}
        onRemovePlayer={removePlayer}
        onUpdateScore={updateScore}
        t={t}
        isReadonly={isReadonlyMode}
      />
    );
  };

  // ── Action bar ────────────────────────────────────────────────────────────
  const actionBar = (
    <div className="flex justify-between items-center pt-6 border-t border-border">
      <div>
        {currentStep > 0 && (
          <Button type="button" variant="outline" onClick={handlePrev} disabled={isSubmitting}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            {t('actions.back')}
          </Button>
        )}
        {currentStep === 0 && onCancel && (
          <Button type="button" variant="ghost" onClick={handleCancel} disabled={isSubmitting}>
            {t('actions.cancel')}
          </Button>
        )}
      </div>

      <div className="flex gap-2">
        {currentStep < STEP_COUNT - 1 ? (
          <Button type="button" onClick={handleNext} disabled={isSubmitting}>
            {t('actions.next')}
            <ArrowRight className="w-4 h-4 ml-2" />
          </Button>
        ) : (
          <Button type="submit" form={formId} disabled={isSubmitting}>
            {isSubmitting ? (
              <>{t('actions.saving')}</>
            ) : (
              <>
                <Check className="w-4 h-4 mr-2" />
                {t('actions.save')}
              </>
            )}
          </Button>
        )}
      </div>
    </div>
  );

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <div role="region" aria-label={t('a11y.wizardLabel')}>
      {/* Step indicator — always rendered (sticky top on mobile) */}
      <StepIndicator
        current={currentStep}
        onBack={currentStep > 0 ? handlePrev : undefined}
        t={t}
      />

      {/* Mobile layout: single column full wizard */}
      {isMobile ? (
        <div className="px-4 py-6">
          <Form {...form}>
            <form id={formId} onSubmit={handleFormSubmit} className="space-y-0">
              {renderStepContent()}
              {actionBar}
            </form>
          </Form>
        </div>
      ) : (
        /* Desktop layout: 8-col form + 4-col live preview */
        <div
          className="grid gap-6 px-6 py-6"
          style={{ gridTemplateColumns: 'minmax(0, 8fr) minmax(0, 4fr)' }}
        >
          {/* Form column */}
          <div>
            <Form {...form}>
              <form
                id={formId}
                onSubmit={handleFormSubmit}
                className="space-y-0 flex flex-col gap-0"
              >
                {renderStepContent()}
                {actionBar}
              </form>
            </Form>
          </div>

          {/* Live preview column — K16 described */}
          <div className="pt-2">
            <LivePreview gameName={form.watch('gameName') ?? ''} players={players} t={t} />
          </div>
        </div>
      )}
    </div>
  );
}

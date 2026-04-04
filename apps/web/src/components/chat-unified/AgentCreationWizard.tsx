/**
 * AgentCreationWizard - 4-step wizard to create a user-owned custom agent (Issue #4915)
 *
 * Steps:
 * 1. GameCollectionPicker  — select a game from user's library
 * 2. AgentTypePicker       — choose agent personality: Tutor / Arbitro / Stratega / Narratore
 * 3. AgentNameAndKbStep    — give agent a name + pick KB PDFs
 * 4. AgentCreationReview   — summary + confirm creation
 *
 * On submit: POST /api/v1/agents/user (with documentIds) → redirect /chat/new
 */

'use client';

import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import {
  AlertCircle,
  BookOpen,
  Bot,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  FileText,
  Gamepad2,
  Loader2,
  Search,
  Shield,
  Swords,
  Target,
} from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { api } from '@/lib/api';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import type { GamePdfDto } from '@/lib/api/schemas/pdf.schemas';
import { cn } from '@/lib/utils';

import {
  WIZARD_AGENT_TYPE,
  WIZARD_BTN,
  WIZARD_STEP_LABEL,
  WIZARD_STEP_TITLE,
  WIZARD_TESTID,
  WIZARD_TYPE_TO_BACKEND,
} from './wizard-constants';

import type { WizardAgentTypeId } from './wizard-constants';

// ============================================================================
// Types
// ============================================================================

interface AgentTypeOption {
  id: string;
  label: string;
  description: string;
  icon: React.ElementType;
  color: string;
}

interface WizardState {
  selectedGame: UserLibraryEntry | null;
  agentType: string | null;
  agentName: string;
  selectedPdfIds: string[];
}

// ============================================================================
// Constants
// ============================================================================

const AGENT_TYPES: AgentTypeOption[] = [
  {
    id: WIZARD_AGENT_TYPE.Tutor,
    label: WIZARD_AGENT_TYPE.Tutor,
    description: 'Spiega regole e meccaniche del gioco in modo chiaro e didattico',
    icon: BookOpen,
    color: 'amber',
  },
  {
    id: WIZARD_AGENT_TYPE.Arbitro,
    label: WIZARD_AGENT_TYPE.Arbitro,
    description: 'Risolve dubbi, dispute e interpretazioni delle regole',
    icon: Shield,
    color: 'blue',
  },
  {
    id: WIZARD_AGENT_TYPE.Stratega,
    label: WIZARD_AGENT_TYPE.Stratega,
    description: 'Fornisce consigli strategici e tattici per la partita',
    icon: Target,
    color: 'green',
  },
  {
    id: WIZARD_AGENT_TYPE.Narratore,
    label: WIZARD_AGENT_TYPE.Narratore,
    description: 'Racconta ambientazione, lore e atmosfera del gioco',
    icon: BookOpen,
    color: 'purple',
  },
];

// ============================================================================
// Step Indicator
// ============================================================================

function StepIndicator({ current, total }: { current: number; total: number }) {
  return (
    <div className="flex items-center justify-center gap-0 mb-8">
      {Array.from({ length: total }, (_, i) => {
        const step = i + 1;
        const isCompleted = step < current;
        const isActive = step === current;
        return (
          <React.Fragment key={step}>
            <div className="flex flex-col items-center">
              <div
                className={cn(
                  'w-9 h-9 rounded-full flex items-center justify-center text-sm font-semibold transition-all',
                  isCompleted && 'bg-amber-500 text-white',
                  isActive && 'bg-amber-600 text-white ring-4 ring-amber-200 dark:ring-amber-900',
                  !isCompleted && !isActive && 'bg-muted text-muted-foreground'
                )}
              >
                {isCompleted ? <CheckCircle2 className="h-5 w-5" /> : step}
              </div>
              <span
                className={cn(
                  'text-xs mt-1 font-nunito whitespace-nowrap',
                  isActive
                    ? 'text-amber-600 dark:text-amber-400 font-semibold'
                    : 'text-muted-foreground'
                )}
              >
                {WIZARD_STEP_LABEL[i]}
              </span>
            </div>
            {i < total - 1 && (
              <div
                className={cn(
                  'h-0.5 w-12 mx-1 mb-5 transition-all',
                  step < current ? 'bg-amber-500' : 'bg-muted'
                )}
              />
            )}
          </React.Fragment>
        );
      })}
    </div>
  );
}

// ============================================================================
// Step 1: Game Picker
// ============================================================================

function GameCollectionPicker({
  selected,
  onSelect,
}: {
  selected: UserLibraryEntry | null;
  onSelect: (entry: UserLibraryEntry) => void;
}) {
  const [entries, setEntries] = useState<UserLibraryEntry[]>([]);
  const [search, setSearch] = useState('');
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    api.library
      .getLibrary({ pageSize: 100 })
      .then(res => setEntries(res.items ?? []))
      .catch(console.error)
      .finally(() => setIsLoading(false));
  }, []);

  const filtered = useMemo(() => {
    if (!search.trim()) return entries;
    const lower = search.toLowerCase();
    return entries.filter(e => e.gameTitle.toLowerCase().includes(lower));
  }, [entries, search]);

  return (
    <div className="space-y-4">
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <input
          type="text"
          placeholder="Cerca nella tua libreria..."
          value={search}
          onChange={e => setSearch(e.target.value)}
          className="w-full pl-10 pr-4 py-2.5 rounded-lg border border-border bg-white/50 dark:bg-black/20 backdrop-blur-sm text-sm font-nunito focus:outline-none focus:ring-2 focus:ring-amber-400"
        />
      </div>

      {isLoading ? (
        <div
          data-testid={WIZARD_TESTID.LibraryLoading}
          className="grid grid-cols-2 sm:grid-cols-3 gap-3"
        >
          {[...Array(6)].map((_, i) => (
            <div key={i} className="h-36 rounded-xl bg-muted animate-pulse" />
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <div
          data-testid={WIZARD_TESTID.LibraryEmpty}
          className="text-center py-10 text-muted-foreground font-nunito"
        >
          {search ? 'Nessun gioco trovato' : 'Nessun gioco nella libreria'}
        </div>
      ) : (
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-3 max-h-96 overflow-y-auto pr-1">
          {filtered.map(entry => (
            <button
              key={entry.gameId}
              data-testid={WIZARD_TESTID.GameCard(entry.gameTitle)}
              onClick={() => onSelect(entry)}
              className={cn(
                'text-left rounded-xl border-2 transition-all focus:outline-none focus:ring-2 focus:ring-amber-400',
                selected?.gameId === entry.gameId
                  ? 'border-amber-500 ring-2 ring-amber-200 dark:ring-amber-900'
                  : 'border-transparent hover:border-amber-200'
              )}
            >
              <MeepleCard
                entity="game"
                variant="compact"
                title={entry.gameTitle}
                imageUrl={entry.gameImageUrl ?? undefined}
              />
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

// ============================================================================
// Step 2: Agent Type Picker
// ============================================================================

function AgentTypePicker({
  selected,
  onSelect,
}: {
  selected: string | null;
  onSelect: (type: string) => void;
}) {
  const colorMap: Record<string, string> = {
    amber: 'border-amber-400 bg-amber-50 dark:bg-amber-900/20 ring-amber-200 dark:ring-amber-800',
    blue: 'border-blue-400 bg-blue-50 dark:bg-blue-900/20 ring-blue-200 dark:ring-blue-800',
    green: 'border-green-400 bg-green-50 dark:bg-green-900/20 ring-green-200 dark:ring-green-800',
  };
  const iconColorMap: Record<string, string> = {
    amber: 'text-amber-600 dark:text-amber-400',
    blue: 'text-blue-600 dark:text-blue-400',
    green: 'text-green-600 dark:text-green-400',
  };

  return (
    <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
      {AGENT_TYPES.map(option => {
        const Icon = option.icon;
        const isSelected = selected === option.id;
        return (
          <button
            key={option.id}
            data-testid={WIZARD_TESTID.AgentTypeBtn(option.id)}
            onClick={() => onSelect(option.id)}
            className={cn(
              'p-5 rounded-2xl border-2 text-left transition-all focus:outline-none focus:ring-2',
              'bg-white/70 dark:bg-black/30 backdrop-blur-sm hover:shadow-md',
              isSelected
                ? `border-2 ring-4 ${colorMap[option.color]}`
                : 'border-border hover:border-amber-200'
            )}
          >
            <div className={cn('mb-3', iconColorMap[option.color])}>
              <Icon className="h-8 w-8" />
            </div>
            <div className="font-semibold font-quicksand text-base text-foreground mb-1">
              {option.label}
            </div>
            <div className="text-sm text-muted-foreground font-nunito leading-snug">
              {option.description}
            </div>
            {isSelected && (
              <div className="mt-3 flex items-center gap-1 text-amber-600 dark:text-amber-400">
                <CheckCircle2 className="h-4 w-4" />
                <span className="text-xs font-semibold font-nunito">Selezionato</span>
              </div>
            )}
          </button>
        );
      })}
    </div>
  );
}

// ============================================================================
// Step 3: Name + KB
// ============================================================================

function AgentNameAndKbStep({
  gameId,
  agentName,
  selectedPdfIds,
  onNameChange,
  onPdfToggle,
}: {
  gameId: string;
  agentName: string;
  selectedPdfIds: string[];
  onNameChange: (name: string) => void;
  onPdfToggle: (pdfId: string) => void;
}) {
  const [pdfs, setPdfs] = useState<GamePdfDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    setIsLoading(true);
    api.library
      .getGamePdfs(gameId)
      .then(setPdfs)
      .catch(console.error)
      .finally(() => setIsLoading(false));
  }, [gameId]);

  const formatSize = (bytes: number) => {
    if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
    return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  };

  return (
    <div className="space-y-6">
      {/* Agent Name */}
      <div>
        <label className="block text-sm font-semibold font-nunito text-foreground mb-2">
          Nome dell&apos;agent <span className="text-red-500">*</span>
        </label>
        <input
          data-testid={WIZARD_TESTID.NameInput}
          type="text"
          placeholder="Es. Il mio Tutor di Catan"
          value={agentName}
          onChange={e => onNameChange(e.target.value)}
          maxLength={100}
          className="w-full px-4 py-2.5 rounded-lg border border-border bg-white/50 dark:bg-black/20 backdrop-blur-sm text-sm font-nunito focus:outline-none focus:ring-2 focus:ring-amber-400"
        />
        <div className="text-xs text-muted-foreground font-nunito mt-1 text-right">
          {agentName.length}/100
        </div>
      </div>

      {/* PDF Selection */}
      <div>
        <label className="block text-sm font-semibold font-nunito text-foreground mb-2">
          Knowledge Base <span className="text-muted-foreground font-normal">(opzionale)</span>
        </label>
        <p className="text-xs text-muted-foreground font-nunito mb-3">
          Seleziona i PDF che l&apos;agent userà come fonte di conoscenza
        </p>

        {isLoading ? (
          <div className="space-y-2">
            {[...Array(3)].map((_, i) => (
              <div key={i} className="h-14 rounded-lg bg-muted animate-pulse" />
            ))}
          </div>
        ) : pdfs.length === 0 ? (
          <div
            data-testid={WIZARD_TESTID.PdfsEmpty}
            className="flex items-center gap-2 p-4 rounded-lg bg-muted/50 text-muted-foreground font-nunito text-sm"
          >
            <FileText className="h-5 w-5 shrink-0" />
            <span>Nessun PDF disponibile per questo gioco</span>
          </div>
        ) : (
          <div className="space-y-2 max-h-56 overflow-y-auto pr-1">
            {pdfs.map(pdf => {
              const isSelected = selectedPdfIds.includes(pdf.id);
              return (
                <button
                  key={pdf.id}
                  onClick={() => onPdfToggle(pdf.id)}
                  className={cn(
                    'w-full flex items-center gap-3 p-3 rounded-lg border text-left transition-all',
                    isSelected
                      ? 'border-amber-400 bg-amber-50 dark:bg-amber-900/20'
                      : 'border-border hover:border-amber-200 bg-white/50 dark:bg-black/10'
                  )}
                >
                  <div
                    className={cn(
                      'w-5 h-5 rounded border-2 shrink-0 flex items-center justify-center transition-all',
                      isSelected ? 'bg-amber-500 border-amber-500' : 'border-muted-foreground'
                    )}
                  >
                    {isSelected && <CheckCircle2 className="h-3 w-3 text-white" />}
                  </div>
                  <FileText
                    className={cn(
                      'h-4 w-4 shrink-0',
                      isSelected ? 'text-amber-600' : 'text-muted-foreground'
                    )}
                  />
                  <div className="min-w-0 flex-1">
                    <div className="text-sm font-semibold font-nunito text-foreground truncate">
                      {pdf.name}
                    </div>
                    <div className="text-xs text-muted-foreground font-nunito">
                      {pdf.pageCount} pag. · {formatSize(pdf.fileSizeBytes)} ·{' '}
                      <span
                        className={
                          pdf.source === 'Custom'
                            ? 'text-amber-600 dark:text-amber-400'
                            : 'text-muted-foreground'
                        }
                      >
                        {pdf.source === 'Custom' ? 'Caricato da te' : 'Catalogo'}
                      </span>
                    </div>
                  </div>
                </button>
              );
            })}
          </div>
        )}

        {selectedPdfIds.length > 0 && (
          <p className="text-xs text-amber-600 dark:text-amber-400 font-nunito mt-2">
            {selectedPdfIds.length} PDF selezionat{selectedPdfIds.length === 1 ? 'o' : 'i'}
          </p>
        )}
      </div>
    </div>
  );
}

// ============================================================================
// Step 4: Review
// ============================================================================

function AgentCreationReview({
  state,
  onSubmit,
  isSubmitting,
  error,
}: {
  state: WizardState;
  onSubmit: () => void;
  isSubmitting: boolean;
  error: string | null;
}) {
  const agentTypeLabel = AGENT_TYPES.find(t => t.id === state.agentType)?.label ?? state.agentType;

  return (
    <div className="space-y-5">
      <div className="rounded-2xl border border-border bg-white/70 dark:bg-black/30 backdrop-blur-sm p-5 space-y-4">
        <ReviewRow icon={Gamepad2} label="Gioco" value={state.selectedGame?.gameTitle ?? '—'} />
        <ReviewRow icon={Bot} label="Tipo" value={agentTypeLabel ?? '—'} />
        <ReviewRow icon={Swords} label="Nome" value={state.agentName || '—'} />
        <ReviewRow
          icon={FileText}
          label="PDF Knowledge Base"
          value={
            state.selectedPdfIds.length > 0
              ? `${state.selectedPdfIds.length} PDF selezionat${state.selectedPdfIds.length === 1 ? 'o' : 'i'}`
              : 'Nessuno'
          }
        />
      </div>

      {error && (
        <div className="flex items-center gap-2 p-3 rounded-lg bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 text-red-700 dark:text-red-400 text-sm font-nunito">
          <AlertCircle className="h-4 w-4 shrink-0" />
          {error}
        </div>
      )}

      <button
        onClick={onSubmit}
        disabled={isSubmitting}
        className="w-full py-3 rounded-xl bg-amber-500 hover:bg-amber-600 text-white font-semibold font-quicksand text-base transition-all disabled:opacity-60 disabled:cursor-not-allowed flex items-center justify-center gap-2"
      >
        {isSubmitting ? (
          <>
            <Loader2 className="h-5 w-5 animate-spin" />
            {WIZARD_BTN.Submitting}
          </>
        ) : (
          <>
            <Bot className="h-5 w-5" />
            {WIZARD_BTN.Submit}
          </>
        )}
      </button>
    </div>
  );
}

function ReviewRow({
  icon: Icon,
  label,
  value,
}: {
  icon: React.ElementType;
  label: string;
  value: string;
}) {
  return (
    <div className="flex items-start gap-3">
      <Icon className="h-5 w-5 text-amber-600 dark:text-amber-400 mt-0.5 shrink-0" />
      <div>
        <div className="text-xs text-muted-foreground font-nunito">{label}</div>
        <div className="text-sm font-semibold font-nunito text-foreground">{value}</div>
      </div>
    </div>
  );
}

// ============================================================================
// Main Wizard
// ============================================================================

export function AgentCreationWizard() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const preselectedGameId = searchParams?.get('gameId');
  const initialStep = parseInt(searchParams?.get('step') || '0', 10);
  const didPreselect = useRef(false);

  const [step, setStep] = useState(1);
  const [state, setState] = useState<WizardState>({
    selectedGame: null,
    agentType: null,
    agentName: '',
    selectedPdfIds: [],
  });

  // Auto-preselect game if gameId is passed in query params
  // If step param is also provided and >= 2, skip to that step instead of default step 2
  useEffect(() => {
    if (!preselectedGameId || didPreselect.current) return;
    didPreselect.current = true;

    api.library
      .getLibrary({ pageSize: 100 })
      .then(res => {
        const match = res.items?.find(e => e.gameId === preselectedGameId);
        if (match) {
          setState(s => ({ ...s, selectedGame: match }));
          const targetStep = initialStep >= 2 ? Math.min(initialStep, 4) : 2;
          setStep(targetStep);
        }
      })
      .catch(() => {
        /* silent */
      });
  }, [preselectedGameId, initialStep]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const canGoNext = useMemo(() => {
    if (step === 1) return state.selectedGame !== null;
    if (step === 2) return state.agentType !== null;
    if (step === 3) return state.agentName.trim().length > 0;
    return true;
  }, [step, state]);

  const handleNext = useCallback(() => {
    if (canGoNext && step < 4) setStep(s => s + 1);
  }, [canGoNext, step]);

  const handleBack = useCallback(() => {
    if (step > 1) setStep(s => s - 1);
  }, [step]);

  const handleGameSelect = useCallback((entry: UserLibraryEntry) => {
    setState(s => ({ ...s, selectedGame: entry, selectedPdfIds: [] }));
  }, []);

  const handleTypeSelect = useCallback((type: string) => {
    setState(s => ({ ...s, agentType: type }));
  }, []);

  const handleNameChange = useCallback((name: string) => {
    setState(s => ({ ...s, agentName: name }));
  }, []);

  const handlePdfToggle = useCallback((pdfId: string) => {
    setState(s => ({
      ...s,
      selectedPdfIds: s.selectedPdfIds.includes(pdfId)
        ? s.selectedPdfIds.filter(id => id !== pdfId)
        : [...s.selectedPdfIds, pdfId],
    }));
  }, []);

  const handleSubmit = useCallback(async () => {
    if (!state.selectedGame || !state.agentType) return;
    setIsSubmitting(true);
    setSubmitError(null);

    try {
      const backendType = WIZARD_TYPE_TO_BACKEND[state.agentType as WizardAgentTypeId];
      if (!backendType) {
        setSubmitError(`Tipo di agent non supportato: ${state.agentType}`);
        setIsSubmitting(false);
        return;
      }
      await api.agents.createUserAgent({
        gameId: state.selectedGame.gameId,
        agentType: backendType,
        name: state.agentName.trim() || undefined,
        documentIds: state.selectedPdfIds.length > 0 ? state.selectedPdfIds : undefined,
      });

      router.push(`/chat/new?game=${state.selectedGame.gameId}`);
    } catch (err) {
      setSubmitError(
        err instanceof Error ? err.message : "Errore nella creazione dell'agent. Riprova."
      );
    } finally {
      setIsSubmitting(false);
    }
  }, [state, router]);

  const stepTitles = WIZARD_STEP_TITLE;

  return (
    <div className="min-h-dvh bg-gradient-to-br from-amber-50 via-background to-background dark:from-amber-950/20 dark:via-background dark:to-background p-4 sm:p-8">
      <div className="max-w-2xl mx-auto">
        {/* Header */}
        <div className="mb-8 text-center">
          <div className="inline-flex items-center gap-2 mb-3">
            <Bot className="h-7 w-7 text-amber-600 dark:text-amber-400" />
            <h1 className="text-2xl font-bold font-quicksand text-foreground">Crea il tuo Agent</h1>
          </div>
          <p className="text-muted-foreground font-nunito text-sm">
            Configura un agent personalizzato per il tuo gioco
          </p>
        </div>

        {/* Step Indicator */}
        <StepIndicator current={step} total={4} />

        {/* Step Content */}
        <div className="rounded-2xl border border-border bg-white/70 dark:bg-black/30 backdrop-blur-md p-6 shadow-sm">
          <h2 className="text-lg font-semibold font-quicksand text-foreground mb-5">
            {stepTitles[step - 1]}
          </h2>

          {step === 1 && (
            <GameCollectionPicker selected={state.selectedGame} onSelect={handleGameSelect} />
          )}
          {step === 2 && <AgentTypePicker selected={state.agentType} onSelect={handleTypeSelect} />}
          {step === 3 && state.selectedGame && (
            <AgentNameAndKbStep
              gameId={state.selectedGame.gameId}
              agentName={state.agentName}
              selectedPdfIds={state.selectedPdfIds}
              onNameChange={handleNameChange}
              onPdfToggle={handlePdfToggle}
            />
          )}
          {step === 4 && (
            <AgentCreationReview
              state={state}
              onSubmit={handleSubmit}
              isSubmitting={isSubmitting}
              error={submitError}
            />
          )}
        </div>

        {/* Navigation Buttons (steps 1–3) */}
        {step < 4 && (
          <div className="flex items-center justify-between mt-5">
            <button
              onClick={step === 1 ? () => router.push('/chat/new') : handleBack}
              className="flex items-center gap-1.5 px-4 py-2 rounded-lg border border-border text-sm font-nunito font-semibold text-foreground hover:bg-muted transition-all"
            >
              <ChevronLeft className="h-4 w-4" />
              {step === 1 ? WIZARD_BTN.Cancel : WIZARD_BTN.Back}
            </button>

            <button
              onClick={handleNext}
              disabled={!canGoNext}
              className="flex items-center gap-1.5 px-5 py-2 rounded-lg bg-amber-500 hover:bg-amber-600 text-white text-sm font-nunito font-semibold transition-all disabled:opacity-40 disabled:cursor-not-allowed"
            >
              {WIZARD_BTN.Next}
              <ChevronRight className="h-4 w-4" />
            </button>
          </div>
        )}

        {/* Back button on review step */}
        {step === 4 && (
          <div className="mt-5">
            <button
              onClick={handleBack}
              className="flex items-center gap-1.5 px-4 py-2 rounded-lg border border-border text-sm font-nunito font-semibold text-foreground hover:bg-muted transition-all"
            >
              <ChevronLeft className="h-4 w-4" />
              {WIZARD_BTN.Edit}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

'use client';

/**
 * ToolkitConfiguratorClient — Admin/Editor toolkit configurator (Issue #4978).
 *
 * Layout:
 *  ┌─────────────────────────────────────────────────────────┐
 *  │ ← Giochi   Toolkit Configurator          [Pubblica v3] │
 *  ├──────────────────────┬──────────────────────────────────┤
 *  │ OVERRIDE BASE TOOL   │  TOOL EXTRA                      │
 *  │  Ordine di turno [T] │  🎲 [+ Aggiungi dado]           │
 *  │  Scoreboard      [T] │  🃏 [+ Aggiungi carta]          │
 *  │  Set dadi        [T] │  ⏱ [+ Aggiungi timer]           │
 *  │                      │  🔢 [+ Aggiungi contatore]      │
 *  ├──────────────────────┴──────────────────────────────────┤
 *  │ ANTEPRIMA — tool rail as the player will see it         │
 *  └─────────────────────────────────────────────────────────┘
 *
 * Admin/Editor only — guarded by AdminAuthGuard.
 */

import React, { useState } from 'react';

import {
  BadgeCheck,
  ChevronLeft,
  Dices,
  Loader2,
  PlaySquare,
  Plus,
  Timer,
  Trash2,
  Hash,
} from 'lucide-react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';
import { useAuth } from '@/components/auth/AuthProvider';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Switch } from '@/components/ui/forms/switch';
import { useToolkitEditor } from '@/lib/hooks/useToolkitEditor';
import type {
  CardToolDto,
  CounterToolDto,
  DiceToolDto,
  GameToolkitDto,
  TimerToolDto,
} from '@/lib/types/gameToolkit';
import { resolveSessionTools } from '@/lib/utils/resolveSessionTools';

// ── Types ─────────────────────────────────────────────────────────────────────

interface Props {
  gameId: string;
}

// ── Add-tool form state types ─────────────────────────────────────────────────

interface DiceForm {
  name: string;
  diceType: string;
  quantity: number;
  isInteractive: boolean;
}

interface CardForm {
  name: string;
  deckType: string;
  cardCount: number;
  shuffleable: boolean;
  allowDraw: boolean;
  allowDiscard: boolean;
  allowPeek: boolean;
  allowReturnToDeck: boolean;
}

interface TimerForm {
  name: string;
  durationSeconds: number;
  timerType: string;
  autoStart: boolean;
  isPerPlayer: boolean;
}

interface CounterForm {
  name: string;
  minValue: number;
  maxValue: number;
  defaultValue: number;
  isPerPlayer: boolean;
}

// ── Default form values ───────────────────────────────────────────────────────

const defaultDiceForm = (): DiceForm => ({
  name: '',
  diceType: 'd6',
  quantity: 2,
  isInteractive: true,
});

const defaultCardForm = (): CardForm => ({
  name: '',
  deckType: 'Standard52',
  cardCount: 52,
  shuffleable: true,
  allowDraw: true,
  allowDiscard: true,
  allowPeek: false,
  allowReturnToDeck: false,
});

const defaultTimerForm = (): TimerForm => ({
  name: '',
  durationSeconds: 60,
  timerType: 'countdown',
  autoStart: false,
  isPerPlayer: false,
});

const defaultCounterForm = (): CounterForm => ({
  name: '',
  minValue: 0,
  maxValue: 100,
  defaultValue: 0,
  isPerPlayer: false,
});

// ── Preview section ───────────────────────────────────────────────────────────

const BASE_TOOL_LABELS: Record<string, string> = {
  scoreboard: '📊 Scoreboard',
  'turn-order': '🔄 Ordine di turno',
  dice: '🎲 Dadi',
  whiteboard: '🖊️ Lavagna',
};

function ToolkitPreview({ toolkit }: { toolkit: GameToolkitDto | null }) {
  const { visibleBaseToolIds, customTools } = resolveSessionTools(toolkit);

  return (
    <section
      aria-label="Anteprima tool rail"
      className="rounded-lg border border-stone-200 dark:border-stone-700 p-4 bg-stone-50 dark:bg-stone-900"
    >
      <h3 className="text-xs font-semibold uppercase tracking-wider text-stone-500 dark:text-stone-400 mb-3">
        Anteprima — Tool Rail
      </h3>
      <div className="flex flex-wrap gap-2">
        {[...visibleBaseToolIds].map(id => (
          <span
            key={id}
            className="inline-flex items-center gap-1 px-2 py-1 rounded bg-amber-100 dark:bg-amber-900/30 text-amber-800 dark:text-amber-200 text-xs font-medium"
          >
            {BASE_TOOL_LABELS[id] ?? id}
          </span>
        ))}
        {customTools.map(t => (
          <span
            key={t.id}
            className="inline-flex items-center gap-1 px-2 py-1 rounded bg-sky-100 dark:bg-sky-900/30 text-sky-800 dark:text-sky-200 text-xs font-medium"
          >
            {t.icon}
            {t.label}
          </span>
        ))}
      </div>
      {visibleBaseToolIds.size === 0 && customTools.length === 0 && (
        <p className="text-xs text-stone-400">Nessun tool visibile.</p>
      )}
    </section>
  );
}

// ── CreateToolkitPanel ────────────────────────────────────────────────────────

function CreateToolkitPanel({
  isSaving,
  onCreate,
}: {
  isSaving: boolean;
  onCreate: (name: string) => Promise<void>;
}) {
  const [name, setName] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;
    try {
      await onCreate(name.trim());
      toast.success('Toolkit creato');
    } catch {
      toast.error('Errore nella creazione del toolkit');
    }
  };

  return (
    <div className="flex flex-col items-center justify-center h-64 gap-4 text-center">
      <p className="text-stone-500 dark:text-stone-400 text-sm">
        Nessun toolkit configurato per questo gioco.
      </p>
      <form onSubmit={e => void handleSubmit(e)} className="flex gap-2 w-full max-w-sm">
        <Input
          placeholder="Nome toolkit (es. Catan Toolkit)"
          value={name}
          onChange={e => setName(e.target.value)}
          required
          aria-label="Nome toolkit"
          className="flex-1"
        />
        <Button type="submit" disabled={isSaving || !name.trim()}>
          {isSaving ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Crea'}
        </Button>
      </form>
    </div>
  );
}

// ── OverrideToggle ────────────────────────────────────────────────────────────

function OverrideToggle({
  label,
  checked,
  disabled,
  onToggle,
}: {
  label: string;
  checked: boolean;
  disabled: boolean;
  onToggle: (value: boolean) => void;
}) {
  return (
    <div className="flex items-center justify-between py-2">
      <Label className="text-sm font-medium cursor-pointer">{label}</Label>
      <Switch
        checked={checked}
        onCheckedChange={onToggle}
        disabled={disabled}
        aria-label={label}
      />
    </div>
  );
}

// ── AddDiceToolForm ───────────────────────────────────────────────────────────

function AddDiceToolForm({
  isSaving,
  existingNames = [],
  onAdd,
  onCancel,
}: {
  isSaving: boolean;
  existingNames?: string[];
  onAdd: (tool: DiceToolDto) => Promise<void>;
  onCancel: () => void;
}) {
  const [form, setForm] = useState<DiceForm>(defaultDiceForm());
  const [nameError, setNameError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.name.trim()) return;
    if (existingNames.some(n => n.toLowerCase() === form.name.trim().toLowerCase())) {
      setNameError('Esiste già un dado con questo nome');
      return;
    }
    await onAdd({
      name: form.name.trim(),
      diceType: form.diceType,
      quantity: form.quantity,
      isInteractive: form.isInteractive,
    });
    onCancel();
  };

  return (
    <form
      onSubmit={e => void handleSubmit(e)}
      className="mt-2 p-3 rounded-lg border border-stone-200 dark:border-stone-700 bg-stone-50 dark:bg-stone-900 space-y-2"
    >
      <div className="grid grid-cols-2 gap-2">
        <div>
          <Label className="text-xs">Nome *</Label>
          <Input
            value={form.name}
            onChange={e => { setNameError(null); setForm(f => ({ ...f, name: e.target.value })); }}
            placeholder="es. 2×d6"
            required
            className="h-8 text-sm"
          />
          {nameError && <p className="text-xs text-red-500 mt-0.5">{nameError}</p>}
        </div>
        <div>
          <Label className="text-xs">Tipo dado</Label>
          <Input
            value={form.diceType}
            onChange={e => setForm(f => ({ ...f, diceType: e.target.value }))}
            placeholder="d6"
            className="h-8 text-sm"
          />
        </div>
        <div>
          <Label className="text-xs">Quantità</Label>
          <Input
            type="number"
            min={1}
            max={20}
            value={form.quantity}
            onChange={e => setForm(f => ({ ...f, quantity: parseInt(e.target.value, 10) || 1 }))}
            className="h-8 text-sm"
          />
        </div>
        <div className="flex items-end gap-2 pb-1">
          <Switch
            checked={form.isInteractive}
            onCheckedChange={v => setForm(f => ({ ...f, isInteractive: v }))}
            id="dice-interactive"
          />
          <Label htmlFor="dice-interactive" className="text-xs cursor-pointer">
            Interattivo
          </Label>
        </div>
      </div>
      <div className="flex gap-2 justify-end">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel}>
          Annulla
        </Button>
        <Button type="submit" size="sm" disabled={isSaving || !form.name.trim()}>
          {isSaving ? <Loader2 className="w-3 h-3 animate-spin" /> : 'Aggiungi'}
        </Button>
      </div>
    </form>
  );
}

// ── AddCardToolForm ───────────────────────────────────────────────────────────

function AddCardToolForm({
  isSaving,
  existingNames = [],
  onAdd,
  onCancel,
}: {
  isSaving: boolean;
  existingNames?: string[];
  onAdd: (tool: CardToolDto) => Promise<void>;
  onCancel: () => void;
}) {
  const [form, setForm] = useState<CardForm>(defaultCardForm());
  const [nameError, setNameError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.name.trim()) return;
    if (existingNames.some(n => n.toLowerCase() === form.name.trim().toLowerCase())) {
      setNameError('Esiste già un mazzo con questo nome');
      return;
    }
    await onAdd({
      name: form.name.trim(),
      deckType: form.deckType,
      cardCount: form.cardCount,
      shuffleable: form.shuffleable,
      allowDraw: form.allowDraw,
      allowDiscard: form.allowDiscard,
      allowPeek: form.allowPeek,
      allowReturnToDeck: form.allowReturnToDeck,
    });
    onCancel();
  };

  return (
    <form
      onSubmit={e => void handleSubmit(e)}
      className="mt-2 p-3 rounded-lg border border-stone-200 dark:border-stone-700 bg-stone-50 dark:bg-stone-900 space-y-2"
    >
      <div className="grid grid-cols-2 gap-2">
        <div>
          <Label className="text-xs">Nome *</Label>
          <Input
            value={form.name}
            onChange={e => { setNameError(null); setForm(f => ({ ...f, name: e.target.value })); }}
            placeholder="es. Deck sviluppo"
            required
            className="h-8 text-sm"
          />
          {nameError && <p className="text-xs text-red-500 mt-0.5">{nameError}</p>}
        </div>
        <div>
          <Label className="text-xs">Tipo mazzo</Label>
          <Input
            value={form.deckType}
            onChange={e => setForm(f => ({ ...f, deckType: e.target.value }))}
            placeholder="Standard52"
            className="h-8 text-sm"
          />
        </div>
        <div>
          <Label className="text-xs">N° carte</Label>
          <Input
            type="number"
            min={1}
            value={form.cardCount}
            onChange={e => setForm(f => ({ ...f, cardCount: parseInt(e.target.value, 10) || 52 }))}
            className="h-8 text-sm"
          />
        </div>
      </div>
      <div className="flex flex-wrap gap-4 text-xs">
        {(
          [
            ['shuffleable', 'Mischiabile'],
            ['allowDraw', 'Pesca'],
            ['allowDiscard', 'Scarta'],
            ['allowPeek', 'Guarda'],
            ['allowReturnToDeck', 'Rimetti'],
          ] as const
        ).map(([key, label]) => (
          <label key={key} className="flex items-center gap-1 cursor-pointer">
            <Switch
              checked={form[key]}
              onCheckedChange={v => setForm(f => ({ ...f, [key]: v }))}
            />
            {label}
          </label>
        ))}
      </div>
      <div className="flex gap-2 justify-end">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel}>
          Annulla
        </Button>
        <Button type="submit" size="sm" disabled={isSaving || !form.name.trim()}>
          {isSaving ? <Loader2 className="w-3 h-3 animate-spin" /> : 'Aggiungi'}
        </Button>
      </div>
    </form>
  );
}

// ── AddTimerToolForm ──────────────────────────────────────────────────────────

function AddTimerToolForm({
  isSaving,
  existingNames = [],
  onAdd,
  onCancel,
}: {
  isSaving: boolean;
  existingNames?: string[];
  onAdd: (tool: TimerToolDto) => Promise<void>;
  onCancel: () => void;
}) {
  const [form, setForm] = useState<TimerForm>(defaultTimerForm());
  const [nameError, setNameError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.name.trim()) return;
    if (existingNames.some(n => n.toLowerCase() === form.name.trim().toLowerCase())) {
      setNameError('Esiste già un timer con questo nome');
      return;
    }
    await onAdd({
      name: form.name.trim(),
      durationSeconds: form.durationSeconds,
      timerType: form.timerType,
      autoStart: form.autoStart,
      isPerPlayer: form.isPerPlayer,
    });
    onCancel();
  };

  return (
    <form
      onSubmit={e => void handleSubmit(e)}
      className="mt-2 p-3 rounded-lg border border-stone-200 dark:border-stone-700 bg-stone-50 dark:bg-stone-900 space-y-2"
    >
      <div className="grid grid-cols-2 gap-2">
        <div>
          <Label className="text-xs">Nome *</Label>
          <Input
            value={form.name}
            onChange={e => { setNameError(null); setForm(f => ({ ...f, name: e.target.value })); }}
            placeholder="es. Countdown"
            required
            className="h-8 text-sm"
          />
          {nameError && <p className="text-xs text-red-500 mt-0.5">{nameError}</p>}
        </div>
        <div>
          <Label className="text-xs">Durata (s)</Label>
          <Input
            type="number"
            min={1}
            value={form.durationSeconds}
            onChange={e =>
              setForm(f => ({ ...f, durationSeconds: parseInt(e.target.value, 10) || 60 }))
            }
            className="h-8 text-sm"
          />
        </div>
        <div>
          <Label className="text-xs">Tipo</Label>
          <Input
            value={form.timerType}
            onChange={e => setForm(f => ({ ...f, timerType: e.target.value }))}
            placeholder="countdown"
            className="h-8 text-sm"
          />
        </div>
      </div>
      <div className="flex gap-4 text-xs">
        <label className="flex items-center gap-1 cursor-pointer">
          <Switch
            checked={form.autoStart}
            onCheckedChange={v => setForm(f => ({ ...f, autoStart: v }))}
          />
          Auto-start
        </label>
        <label className="flex items-center gap-1 cursor-pointer">
          <Switch
            checked={form.isPerPlayer}
            onCheckedChange={v => setForm(f => ({ ...f, isPerPlayer: v }))}
          />
          Per giocatore
        </label>
      </div>
      <div className="flex gap-2 justify-end">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel}>
          Annulla
        </Button>
        <Button type="submit" size="sm" disabled={isSaving || !form.name.trim()}>
          {isSaving ? <Loader2 className="w-3 h-3 animate-spin" /> : 'Aggiungi'}
        </Button>
      </div>
    </form>
  );
}

// ── AddCounterToolForm ────────────────────────────────────────────────────────

function AddCounterToolForm({
  isSaving,
  existingNames = [],
  onAdd,
  onCancel,
}: {
  isSaving: boolean;
  existingNames?: string[];
  onAdd: (tool: CounterToolDto) => Promise<void>;
  onCancel: () => void;
}) {
  const [form, setForm] = useState<CounterForm>(defaultCounterForm());
  const [nameError, setNameError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.name.trim()) return;
    if (existingNames.some(n => n.toLowerCase() === form.name.trim().toLowerCase())) {
      setNameError('Esiste già un contatore con questo nome');
      return;
    }
    await onAdd({
      name: form.name.trim(),
      minValue: form.minValue,
      maxValue: form.maxValue,
      defaultValue: form.defaultValue,
      isPerPlayer: form.isPerPlayer,
    });
    onCancel();
  };

  return (
    <form
      onSubmit={e => void handleSubmit(e)}
      className="mt-2 p-3 rounded-lg border border-stone-200 dark:border-stone-700 bg-stone-50 dark:bg-stone-900 space-y-2"
    >
      <div className="grid grid-cols-2 gap-2">
        <div className="col-span-2">
          <Label className="text-xs">Nome *</Label>
          <Input
            value={form.name}
            onChange={e => { setNameError(null); setForm(f => ({ ...f, name: e.target.value })); }}
            placeholder="es. Risorse"
            required
            className="h-8 text-sm"
          />
          {nameError && <p className="text-xs text-red-500 mt-0.5">{nameError}</p>}
        </div>
        <div>
          <Label className="text-xs">Min</Label>
          <Input
            type="number"
            value={form.minValue}
            onChange={e => setForm(f => ({ ...f, minValue: parseInt(e.target.value, 10) || 0 }))}
            className="h-8 text-sm"
          />
        </div>
        <div>
          <Label className="text-xs">Max</Label>
          <Input
            type="number"
            value={form.maxValue}
            onChange={e => setForm(f => ({ ...f, maxValue: parseInt(e.target.value, 10) || 100 }))}
            className="h-8 text-sm"
          />
        </div>
        <div>
          <Label className="text-xs">Default</Label>
          <Input
            type="number"
            value={form.defaultValue}
            onChange={e =>
              setForm(f => ({ ...f, defaultValue: parseInt(e.target.value, 10) || 0 }))
            }
            className="h-8 text-sm"
          />
        </div>
        <div className="flex items-end pb-1">
          <label className="flex items-center gap-1 cursor-pointer text-xs">
            <Switch
              checked={form.isPerPlayer}
              onCheckedChange={v => setForm(f => ({ ...f, isPerPlayer: v }))}
            />
            Per giocatore
          </label>
        </div>
      </div>
      <div className="flex gap-2 justify-end">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel}>
          Annulla
        </Button>
        <Button type="submit" size="sm" disabled={isSaving || !form.name.trim()}>
          {isSaving ? <Loader2 className="w-3 h-3 animate-spin" /> : 'Aggiungi'}
        </Button>
      </div>
    </form>
  );
}

// ── ToolList ──────────────────────────────────────────────────────────────────

function ToolListItem({
  icon,
  label,
  isSaving,
  onRemove,
}: {
  icon: React.ReactNode;
  label: string;
  isSaving: boolean;
  onRemove: () => void;
}) {
  return (
    <div className="flex items-center justify-between py-1.5 px-2 rounded bg-stone-100 dark:bg-stone-800">
      <div className="flex items-center gap-2 text-sm">
        {icon}
        <span>{label}</span>
      </div>
      <Button
        type="button"
        variant="ghost"
        size="sm"
        onClick={onRemove}
        disabled={isSaving}
        aria-label={`Rimuovi ${label}`}
        className="h-6 w-6 p-0 text-red-500 hover:text-red-700"
      >
        <Trash2 className="w-3 h-3" />
      </Button>
    </div>
  );
}

// ── ToolkitConfiguratorContent ────────────────────────────────────────────────

function ToolkitConfiguratorContent({ gameId }: { gameId: string }) {
  const router = useRouter();
  const {
    toolkit,
    isLoading,
    isSaving,
    error,
    createToolkit,
    updateOverrides,
    addDiceTool,
    removeDiceTool,
    addCardTool,
    removeCardTool,
    addTimerTool,
    removeTimerTool,
    addCounterTool,
    removeCounterTool,
    publish,
  } = useToolkitEditor(gameId);

  // Add-form visibility state
  const [showDiceForm, setShowDiceForm] = useState(false);
  const [showCardForm, setShowCardForm] = useState(false);
  const [showTimerForm, setShowTimerForm] = useState(false);
  const [showCounterForm, setShowCounterForm] = useState(false);

  // ── Handlers ─────────────────────────────────────────────────────────────

  const handleToggle = async (
    field: 'overridesTurnOrder' | 'overridesScoreboard' | 'overridesDiceSet',
    value: boolean,
  ) => {
    try {
      await updateOverrides({ [field]: value });
    } catch {
      toast.error('Errore nel salvataggio');
    }
  };

  const handlePublish = async () => {
    try {
      await publish();
      toast.success('Toolkit pubblicato');
    } catch {
      toast.error('Errore nella pubblicazione');
    }
  };

  const handleAddDice = async (tool: DiceToolDto) => {
    try {
      await addDiceTool(tool);
      toast.success(`Dado "${tool.name}" aggiunto`);
    } catch {
      toast.error('Errore nell\'aggiunta del dado');
    }
  };

  const handleRemoveDice = async (name: string) => {
    try {
      await removeDiceTool(name);
      toast.success(`Dado rimosso`);
    } catch {
      toast.error('Errore nella rimozione');
    }
  };

  const handleAddCard = async (tool: CardToolDto) => {
    try {
      await addCardTool(tool);
      toast.success(`Mazzo "${tool.name}" aggiunto`);
    } catch {
      toast.error('Errore nell\'aggiunta del mazzo');
    }
  };

  const handleRemoveCard = async (name: string) => {
    try {
      await removeCardTool(name);
      toast.success('Mazzo rimosso');
    } catch {
      toast.error('Errore nella rimozione');
    }
  };

  const handleAddTimer = async (tool: TimerToolDto) => {
    try {
      await addTimerTool(tool);
      toast.success(`Timer "${tool.name}" aggiunto`);
    } catch {
      toast.error('Errore nell\'aggiunta del timer');
    }
  };

  const handleRemoveTimer = async (name: string) => {
    try {
      await removeTimerTool(name);
      toast.success('Timer rimosso');
    } catch {
      toast.error('Errore nella rimozione');
    }
  };

  const handleAddCounter = async (tool: CounterToolDto) => {
    try {
      await addCounterTool(tool);
      toast.success(`Contatore "${tool.name}" aggiunto`);
    } catch {
      toast.error('Errore nell\'aggiunta del contatore');
    }
  };

  const handleRemoveCounter = async (name: string) => {
    try {
      await removeCounterTool(name);
      toast.success('Contatore rimosso');
    } catch {
      toast.error('Errore nella rimozione');
    }
  };

  // ── Loading ───────────────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <Loader2 className="w-6 h-6 animate-spin text-amber-600" />
      </div>
    );
  }

  // ── Header ────────────────────────────────────────────────────────────────

  const header = (
    <div className="flex items-center justify-between mb-6">
      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="sm"
          onClick={() => router.push('/admin/games')}
          aria-label="Torna ai giochi"
        >
          <ChevronLeft className="w-4 h-4" />
          Giochi
        </Button>
        <h1 className="text-xl font-bold text-stone-900 dark:text-stone-100">
          Toolkit Configurator
        </h1>
        {toolkit && (
          <span className="inline-flex items-center gap-1 text-xs font-medium px-2 py-0.5 rounded-full bg-stone-200 dark:bg-stone-700 text-stone-600 dark:text-stone-400">
            v{toolkit.version}
            {toolkit.isPublished && (
              <BadgeCheck className="w-3 h-3 text-emerald-500" aria-label="Pubblicato" />
            )}
          </span>
        )}
      </div>

      {toolkit && (
        <Button
          onClick={() => void handlePublish()}
          disabled={isSaving}
          className="bg-emerald-600 hover:bg-emerald-700 text-white"
        >
          {isSaving ? (
            <Loader2 className="w-4 h-4 animate-spin" />
          ) : (
            <>
              <BadgeCheck className="w-4 h-4 mr-1" />
              Pubblica v{toolkit.version + 1}
            </>
          )}
        </Button>
      )}
    </div>
  );

  // ── Error banner ──────────────────────────────────────────────────────────

  const errorBanner = error && (
    <div
      role="alert"
      className="mb-4 p-3 rounded-lg bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 text-sm text-red-700 dark:text-red-300"
    >
      {error}
    </div>
  );

  // ── No toolkit yet ────────────────────────────────────────────────────────

  if (!toolkit) {
    return (
      <div className="container mx-auto p-6 max-w-4xl">
        {header}
        {errorBanner}
        <CreateToolkitPanel isSaving={isSaving} onCreate={createToolkit} />
      </div>
    );
  }

  // ── Main configurator ─────────────────────────────────────────────────────

  return (
    <div className="container mx-auto p-6 max-w-4xl space-y-6">
      {header}
      {errorBanner}

      {/* 2-column grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">

        {/* LEFT: Override base tools */}
        <section
          aria-label="Override tool base"
          className="rounded-lg border border-stone-200 dark:border-stone-700 p-4 space-y-1"
        >
          <h2 className="text-xs font-semibold uppercase tracking-wider text-stone-500 dark:text-stone-400 mb-3">
            Override Tool Base
          </h2>
          <OverrideToggle
            label="🔄 Ordine di turno"
            checked={toolkit.overridesTurnOrder}
            disabled={isSaving}
            onToggle={v => void handleToggle('overridesTurnOrder', v)}
          />
          <OverrideToggle
            label="📊 Scoreboard"
            checked={toolkit.overridesScoreboard}
            disabled={isSaving}
            onToggle={v => void handleToggle('overridesScoreboard', v)}
          />
          <OverrideToggle
            label="🎲 Set dadi"
            checked={toolkit.overridesDiceSet}
            disabled={isSaving}
            onToggle={v => void handleToggle('overridesDiceSet', v)}
          />
          <p className="text-xs text-stone-400 dark:text-stone-500 pt-2">
            La Lavagna non è mai disattivabile.
          </p>
        </section>

        {/* RIGHT: Extra tools */}
        <section
          aria-label="Tool extra"
          className="rounded-lg border border-stone-200 dark:border-stone-700 p-4 space-y-4"
        >
          <h2 className="text-xs font-semibold uppercase tracking-wider text-stone-500 dark:text-stone-400">
            Tool Extra
          </h2>

          {/* Dice tools */}
          <div>
            <div className="flex items-center justify-between mb-1">
              <span className="text-sm font-medium flex items-center gap-1">
                <Dices className="w-4 h-4" /> Dadi
              </span>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="h-7 text-xs"
                onClick={() => setShowDiceForm(v => !v)}
                disabled={isSaving}
              >
                <Plus className="w-3 h-3 mr-1" /> Aggiungi
              </Button>
            </div>
            <div className="space-y-1">
              {toolkit.diceTools.map(t => (
                <ToolListItem
                  key={t.name}
                  icon={<Dices className="w-3.5 h-3.5 text-amber-500" />}
                  label={t.name}
                  isSaving={isSaving}
                  onRemove={() => void handleRemoveDice(t.name)}
                />
              ))}
            </div>
            {showDiceForm && (
              <AddDiceToolForm
                isSaving={isSaving}
                existingNames={toolkit.diceTools.map(t => t.name)}
                onAdd={tool => handleAddDice(tool)}
                onCancel={() => setShowDiceForm(false)}
              />
            )}
          </div>

          {/* Card tools */}
          <div>
            <div className="flex items-center justify-between mb-1">
              <span className="text-sm font-medium flex items-center gap-1">
                <PlaySquare className="w-4 h-4" /> Carte
              </span>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="h-7 text-xs"
                onClick={() => setShowCardForm(v => !v)}
                disabled={isSaving}
              >
                <Plus className="w-3 h-3 mr-1" /> Aggiungi
              </Button>
            </div>
            <div className="space-y-1">
              {toolkit.cardTools.map(t => (
                <ToolListItem
                  key={t.name}
                  icon={<PlaySquare className="w-3.5 h-3.5 text-sky-500" />}
                  label={t.name}
                  isSaving={isSaving}
                  onRemove={() => void handleRemoveCard(t.name)}
                />
              ))}
            </div>
            {showCardForm && (
              <AddCardToolForm
                isSaving={isSaving}
                existingNames={toolkit.cardTools.map(t => t.name)}
                onAdd={tool => handleAddCard(tool)}
                onCancel={() => setShowCardForm(false)}
              />
            )}
          </div>

          {/* Timer tools */}
          <div>
            <div className="flex items-center justify-between mb-1">
              <span className="text-sm font-medium flex items-center gap-1">
                <Timer className="w-4 h-4" /> Timer
              </span>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="h-7 text-xs"
                onClick={() => setShowTimerForm(v => !v)}
                disabled={isSaving}
              >
                <Plus className="w-3 h-3 mr-1" /> Aggiungi
              </Button>
            </div>
            <div className="space-y-1">
              {toolkit.timerTools.map(t => (
                <ToolListItem
                  key={t.name}
                  icon={<Timer className="w-3.5 h-3.5 text-purple-500" />}
                  label={t.name}
                  isSaving={isSaving}
                  onRemove={() => void handleRemoveTimer(t.name)}
                />
              ))}
            </div>
            {showTimerForm && (
              <AddTimerToolForm
                isSaving={isSaving}
                existingNames={toolkit.timerTools.map(t => t.name)}
                onAdd={tool => handleAddTimer(tool)}
                onCancel={() => setShowTimerForm(false)}
              />
            )}
          </div>

          {/* Counter tools */}
          <div>
            <div className="flex items-center justify-between mb-1">
              <span className="text-sm font-medium flex items-center gap-1">
                <Hash className="w-4 h-4" /> Contatori
              </span>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="h-7 text-xs"
                onClick={() => setShowCounterForm(v => !v)}
                disabled={isSaving}
              >
                <Plus className="w-3 h-3 mr-1" /> Aggiungi
              </Button>
            </div>
            <div className="space-y-1">
              {toolkit.counterTools.map(t => (
                <ToolListItem
                  key={t.name}
                  icon={<Hash className="w-3.5 h-3.5 text-rose-500" />}
                  label={t.name}
                  isSaving={isSaving}
                  onRemove={() => void handleRemoveCounter(t.name)}
                />
              ))}
            </div>
            {showCounterForm && (
              <AddCounterToolForm
                isSaving={isSaving}
                existingNames={toolkit.counterTools.map(t => t.name)}
                onAdd={tool => handleAddCounter(tool)}
                onCancel={() => setShowCounterForm(false)}
              />
            )}
          </div>
        </section>
      </div>

      {/* Preview */}
      <ToolkitPreview toolkit={toolkit} />
    </div>
  );
}

// ── ToolkitConfiguratorClient (exported) ──────────────────────────────────────

export function ToolkitConfiguratorClient({ gameId }: Props) {
  const { user, loading: authLoading } = useAuth();

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <ToolkitConfiguratorContent gameId={gameId} />
    </AdminAuthGuard>
  );
}

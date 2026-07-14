'use client';

/**
 * CampaignSetupDrawer — Iter 4 (M4 in storyboard).
 *
 * 3-step setup wizard for "Nuova campagna libro game", replacing the
 * minimal `NewCampaignDialog` (1-step modal with just a title field).
 *
 * UX per storyboard `nanolith-runthrough-setup-wizard.html`:
 *   Step 1 · Name       — campaign title + group preset (Gruppo A · I ragazzi,
 *                          Gruppo B · Coppia, Custom)
 *   Step 2 · Players    — host (Aaron) + guest chips, add custom (visual)
 *   Step 3 · Confirm    — review card with ManaPips + CTA "📖 Inizia sessione"
 *   Validation          — title ≥ 3 chars
 *
 * Backend contract (Iter 1.A): POST /api/v1/gamebook/campaigns accepts
 * `{ gameId, title }` only. Preset + players are presentation-layer for now;
 * iter futuro estenderà lo schema (issue separata) — quando arriva, sostituire
 * il payload in `mutation.mutate()` senza toccare la UI.
 */

import {
  cloneElement,
  isValidElement,
  useEffect,
  useRef,
  useState,
  type MouseEvent,
  type ReactElement,
  type ReactNode,
} from 'react';

import { useMutation } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';

import {
  Drawer,
  DrawerContent,
  DrawerFooter,
  DrawerHeader,
  DrawerTitle,
} from '@/components/ui/drawer/drawer';
import { createCampaign } from '@/lib/api/gamebook-campaigns';

// ─── Types ──────────────────────────────────────────────────────────────────

type StepId = 1 | 2 | 3;

type PresetId = 'group-a' | 'group-b' | 'custom';

interface PlayerChip {
  readonly id: string;
  readonly initial: string;
  readonly name: string;
  readonly role: 'host' | 'guest';
}

interface PresetConfig {
  readonly id: PresetId;
  readonly title: string;
  readonly subtitle: string;
  readonly players: readonly PlayerChip[];
}

const PRESETS: readonly PresetConfig[] = [
  {
    id: 'group-a',
    title: 'Gruppo A · I ragazzi',
    subtitle: 'Marco, Giulia, Luca',
    players: [
      { id: 'host', initial: 'A', name: 'Aaron', role: 'host' },
      { id: 'marco', initial: 'M', name: 'Marco', role: 'guest' },
      { id: 'giulia', initial: 'G', name: 'Giulia', role: 'guest' },
      { id: 'luca', initial: 'L', name: 'Luca', role: 'guest' },
    ],
  },
  {
    id: 'group-b',
    title: 'Gruppo B · Coppia',
    subtitle: 'Solo con la fidanzata',
    players: [
      { id: 'host', initial: 'A', name: 'Aaron', role: 'host' },
      { id: 'fidanzata', initial: 'F', name: 'Fidanzata', role: 'guest' },
    ],
  },
  {
    id: 'custom',
    title: 'Custom',
    subtitle: 'Aggiungi giocatori manualmente',
    players: [{ id: 'host', initial: 'A', name: 'Aaron', role: 'host' }],
  },
];

const MIN_TITLE_LENGTH = 3;
const MAX_TITLE_LENGTH = 200;

export interface CampaignSetupDrawerProps {
  readonly gameId: string;
  readonly gameTitle: string;
  /**
   * Uncontrolled mode — render a trigger node that opens the drawer on click.
   * Pass either `trigger` (uncontrolled) OR `open`+`onOpenChange` (controlled).
   */
  readonly trigger?: ReactNode;
  /** Controlled mode — drawer open state managed by parent. */
  readonly open?: boolean;
  readonly onOpenChange?: (open: boolean) => void;
  /**
   * Storybook/test seam: initial wizard step (default 1).
   * Allows snapshot stories to render interaction-driven states statically
   * without requiring play functions. Must NOT be used in production call sites.
   */
  readonly initialStep?: StepId;
  /**
   * Storybook/test seam: initial campaign title (default 'Campagna con i ragazzi').
   * Pass a short string (< 3 chars) to render the validation-error state statically.
   */
  readonly initialTitle?: string;
  /**
   * Storybook/test seam: initial preset selection (default 'group-a').
   */
  readonly initialPresetId?: PresetId;
}

// ─── Component ──────────────────────────────────────────────────────────────

export function CampaignSetupDrawer({
  gameId,
  gameTitle,
  trigger,
  open: openProp,
  onOpenChange,
  initialStep,
  initialTitle,
  initialPresetId,
}: CampaignSetupDrawerProps): ReactElement {
  const router = useRouter();
  const [internalOpen, setInternalOpen] = useState(false);
  const isControlled = openProp !== undefined;
  const open = isControlled ? openProp : internalOpen;
  const setOpen = (next: boolean): void => {
    if (isControlled) onOpenChange?.(next);
    else setInternalOpen(next);
  };
  const [step, setStep] = useState<StepId>(initialStep ?? 1);
  const [title, setTitle] = useState(initialTitle ?? 'Campagna con i ragazzi');
  const [presetId, setPresetId] = useState<PresetId>(initialPresetId ?? 'group-a');
  const preset = PRESETS.find(p => p.id === presetId) ?? PRESETS[0];

  // #2917: real roster. Per decision #2759 (MVP), the authenticated owner is seeded
  // server-side (as a non-live Session); everyone else is a free guest name.
  const [guests, setGuests] = useState<string[]>([]);
  const [guestInput, setGuestInput] = useState('');
  const addGuest = (): void => {
    const name = guestInput.trim();
    if (name.length > 0 && !guests.includes(name)) {
      setGuests(prev => [...prev, name]);
      setGuestInput('');
    }
  };
  const removeGuest = (name: string): void => {
    setGuests(prev => prev.filter(g => g !== name));
  };

  const mutation = useMutation({
    mutationFn: () => createCampaign({ gameId, title: title.trim(), guestNames: guests }),
    onSuccess: campaign => {
      reset();
      router.push(`/library/${gameId}/play/${campaign.id}`);
    },
  });

  const stepContainerRef = useRef<HTMLDivElement>(null);
  const isInitialMountRef = useRef(true);

  useEffect(() => {
    if (isInitialMountRef.current) {
      isInitialMountRef.current = false;
      return;
    }
    const container = stepContainerRef.current;
    if (!container) return;
    const focusable = container.querySelector<HTMLElement>(
      'input:not([disabled]), button:not([disabled]):not([aria-disabled="true"]), [role="radio"]:not([aria-disabled="true"]), [tabindex="0"]'
    );
    if (focusable) {
      focusable.focus();
    } else {
      container.focus();
    }
  }, [step]);

  function reset(): void {
    setOpen(false);
    setStep(1);
    setTitle('Campagna con i ragazzi');
    setPresetId('group-a');
    setGuests([]);
    setGuestInput('');
    mutation.reset();
    isInitialMountRef.current = true;
  }

  const trimmedTitle = title.trim();
  const titleValid = trimmedTitle.length >= MIN_TITLE_LENGTH;
  const titleError =
    !titleValid && title.length > 0
      ? `Il nome deve essere almeno ${MIN_TITLE_LENGTH} caratteri.`
      : null;

  // Uncontrolled mode: inject onClick into the consumer's trigger element to
  // avoid the `<button><button/></button>` anti-pattern. Controlled mode
  // skips the trigger entirely (parent manages open state).
  const triggerNode =
    trigger == null ? null : isValidElement<{ onClick?: (e: MouseEvent) => void }>(trigger) ? (
      cloneElement(trigger, {
        onClick: (e: MouseEvent) => {
          trigger.props.onClick?.(e);
          if (!e.defaultPrevented) setOpen(true);
        },
      })
    ) : (
      <span
        role="button"
        tabIndex={0}
        onClick={() => setOpen(true)}
        onKeyDown={e => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            setOpen(true);
          }
        }}
      >
        {trigger}
      </span>
    );

  return (
    <Drawer open={open} onOpenChange={setOpen} entity="session">
      {triggerNode}
      <DrawerContent
        aria-label="Wizard nuova campagna libro game"
        data-testid="campaign-setup-drawer"
      >
        <DrawerHeader>
          <DrawerTitle>📖 Nuova campagna · {gameTitle}</DrawerTitle>
        </DrawerHeader>

        <Stepper current={step} />

        <div
          ref={stepContainerRef}
          tabIndex={-1}
          aria-label={
            step === 1 ? 'Step 1: Nome' : step === 2 ? 'Step 2: Giocatori' : 'Step 3: Conferma'
          }
          className="flex-1 overflow-y-auto px-4 py-3 outline-none"
          data-testid="campaign-setup-step-content"
        >
          {step === 1 && (
            <StepName
              title={title}
              onTitleChange={setTitle}
              titleError={titleError}
              presetId={presetId}
              onPresetChange={setPresetId}
            />
          )}
          {step === 2 && (
            <StepPlayers
              guests={guests}
              guestInput={guestInput}
              onGuestInputChange={setGuestInput}
              onAddGuest={addGuest}
              onRemoveGuest={removeGuest}
            />
          )}
          {step === 3 && (
            <StepConfirm
              gameTitle={gameTitle}
              campaignTitle={trimmedTitle}
              preset={preset}
              guests={guests}
              error={
                mutation.isError
                  ? mutation.error instanceof Error
                    ? mutation.error.message
                    : 'Errore creazione campagna'
                  : null
              }
            />
          )}
        </div>

        <DrawerFooter>
          <div className="flex w-full items-center gap-2">
            <button
              type="button"
              onClick={() => (step === 1 ? reset() : setStep((step - 1) as StepId))}
              disabled={mutation.isPending}
              className="rounded-md border border-input bg-background px-4 py-2 text-sm font-semibold text-foreground hover:bg-muted disabled:opacity-50"
              data-testid="campaign-setup-back"
            >
              {step === 1 ? 'Annulla' : '← Indietro'}
            </button>
            <div className="flex-1" />
            {step < 3 ? (
              <button
                type="button"
                onClick={() => setStep((step + 1) as StepId)}
                disabled={step === 1 ? !titleValid : false}
                className="rounded-md bg-[hsl(var(--c-session))] px-5 py-2 text-sm font-bold text-white shadow-[0_4px_14px_hsl(var(--c-session)/0.35)] hover:-translate-y-px disabled:cursor-not-allowed disabled:opacity-50"
                data-testid="campaign-setup-next"
              >
                Avanti →
              </button>
            ) : (
              <button
                type="button"
                onClick={() => mutation.mutate()}
                disabled={mutation.isPending || !titleValid}
                className="inline-flex items-center gap-1 rounded-md bg-[hsl(var(--c-session))] px-5 py-2 text-sm font-bold text-white shadow-[0_4px_14px_hsl(var(--c-session)/0.35)] hover:-translate-y-px disabled:opacity-50"
                data-testid="campaign-setup-submit"
              >
                {mutation.isPending ? 'Creazione…' : '📖 Inizia sessione'}
              </button>
            )}
          </div>
        </DrawerFooter>
      </DrawerContent>
    </Drawer>
  );
}

// ─── Stepper ────────────────────────────────────────────────────────────────

function Stepper({ current }: { current: StepId }): ReactElement {
  const steps: Array<{ id: StepId; label: string }> = [
    { id: 1, label: 'Nome' },
    { id: 2, label: 'Giocatori' },
    { id: 3, label: 'Conferma' },
  ];
  return (
    <div className="flex gap-2 border-b border-border px-4 py-2.5" aria-label="Step progress">
      {steps.map(s => {
        const state = s.id < current ? 'done' : s.id === current ? 'active' : 'pending';
        return (
          <div
            key={s.id}
            data-state={state}
            className={
              state === 'active'
                ? 'flex-1 rounded-md border border-[hsl(var(--c-session)/0.45)] bg-[hsl(var(--c-session)/0.12)] px-2 py-1 text-center text-[11px] font-bold text-[hsl(var(--c-session))]'
                : state === 'done'
                  ? 'flex-1 rounded-md border border-[hsl(var(--c-success)/0.35)] bg-[hsl(var(--c-success)/0.1)] px-2 py-1 text-center text-[11px] font-semibold text-[hsl(var(--c-success))]'
                  : 'flex-1 rounded-md border border-border bg-muted px-2 py-1 text-center text-[11px] font-semibold text-muted-foreground'
            }
          >
            <span className="mr-1 font-mono">{state === 'done' ? '✓' : s.id}</span>
            {s.label}
          </div>
        );
      })}
    </div>
  );
}

// ─── Step 1 · Name + Preset ─────────────────────────────────────────────────

interface StepNameProps {
  readonly title: string;
  readonly onTitleChange: (v: string) => void;
  readonly titleError: string | null;
  readonly presetId: PresetId;
  readonly onPresetChange: (id: PresetId) => void;
}

function StepName({
  title,
  onTitleChange,
  titleError,
  presetId,
  onPresetChange,
}: StepNameProps): ReactElement {
  return (
    <div className="grid gap-4">
      <label className="grid gap-1.5 text-sm">
        <span className="font-semibold text-foreground">Nome campagna</span>
        <input
          type="text"
          value={title}
          onChange={e => onTitleChange(e.target.value)}
          maxLength={MAX_TITLE_LENGTH}
          autoFocus
          aria-invalid={titleError != null}
          aria-describedby={titleError ? 'campaign-title-error' : 'campaign-title-hint'}
          className={
            titleError
              ? 'rounded-md border border-[hsl(var(--c-danger))] bg-background px-3 py-2 outline-none ring-[3px] ring-[hsl(var(--c-danger)/0.15)]'
              : 'rounded-md border border-input bg-background px-3 py-2 outline-none focus:border-[hsl(var(--c-session))] focus:ring-[3px] focus:ring-[hsl(var(--c-session)/0.18)]'
          }
          data-testid="campaign-setup-title"
        />
        {titleError ? (
          <span
            id="campaign-title-error"
            className="text-xs font-semibold text-[hsl(var(--c-danger))]"
          >
            ⚠ {titleError}
          </span>
        ) : (
          <span id="campaign-title-hint" className="text-xs text-muted-foreground">
            Suggerimento: usa un nome che ricordi il gruppo
          </span>
        )}
      </label>

      <div>
        <h3 className="mb-2 text-[11px] font-bold uppercase tracking-wide text-muted-foreground">
          Preset gruppo
        </h3>
        <div className="grid gap-2" role="radiogroup" aria-label="Preset gruppo giocatori">
          {PRESETS.map(p => {
            const selected = p.id === presetId;
            return (
              <button
                key={p.id}
                type="button"
                role="radio"
                aria-checked={selected}
                onClick={() => onPresetChange(p.id)}
                className={
                  selected
                    ? 'flex items-center gap-3 rounded-md border border-[hsl(var(--c-session)/0.5)] bg-[hsl(var(--c-session)/0.06)] p-3 text-left'
                    : 'flex items-center gap-3 rounded-md border border-border bg-background p-3 text-left hover:bg-muted/50'
                }
              >
                <span
                  className={
                    selected
                      ? 'relative h-[18px] w-[18px] shrink-0 rounded-full border-2 border-[hsl(var(--c-session))] after:absolute after:inset-[3px] after:rounded-full after:bg-[hsl(var(--c-session))]'
                      : 'h-[18px] w-[18px] shrink-0 rounded-full border-2 border-border'
                  }
                  aria-hidden="true"
                />
                <span className="flex-1">
                  <span className="block font-semibold text-foreground">{p.title}</span>
                  <span className="block text-xs text-muted-foreground">{p.subtitle}</span>
                </span>
                <span className="rounded-sm bg-[hsl(var(--c-player)/0.15)] px-2 py-0.5 font-mono text-[11px] font-bold text-[hsl(var(--c-player))]">
                  {p.players.length} {p.players.length === 1 ? 'giocatore' : 'giocatori'}
                </span>
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
}

// ─── Step 2 · Players ───────────────────────────────────────────────────────

interface StepPlayersProps {
  readonly guests: readonly string[];
  readonly guestInput: string;
  readonly onGuestInputChange: (v: string) => void;
  readonly onAddGuest: () => void;
  readonly onRemoveGuest: (name: string) => void;
}

function StepPlayers({
  guests,
  guestInput,
  onGuestInputChange,
  onAddGuest,
  onRemoveGuest,
}: StepPlayersProps): ReactElement {
  const partySize = guests.length + 1; // +1 host
  return (
    <div className="grid gap-4">
      <div>
        <h3 className="mb-2 text-[11px] font-bold uppercase tracking-wide text-muted-foreground">
          Party · {partySize} {partySize === 1 ? 'giocatore' : 'giocatori'}
        </h3>
        <div className="grid gap-2">
          {/* Host — the authenticated owner, seeded server-side (#2917). */}
          <div className="flex items-center gap-2 rounded-md border border-[hsl(var(--c-session)/0.55)] bg-[hsl(var(--c-session)/0.1)] px-3 py-2">
            <span
              className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-[hsl(var(--c-session))] font-mono text-xs font-bold text-white"
              aria-hidden="true"
            >
              ★
            </span>
            <span className="flex-1 truncate">
              <span className="block text-sm font-semibold text-foreground">Tu</span>
              <span className="block font-mono text-[10px] text-[hsl(var(--c-session))]">
                ⭐ Host · tu
              </span>
            </span>
          </div>

          {/* Guests */}
          {guests.map(name => (
            <div
              key={name}
              className="flex items-center gap-2 rounded-md border border-[hsl(var(--c-player)/0.35)] bg-[hsl(var(--c-player)/0.08)] px-3 py-2"
            >
              <span
                className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-[hsl(var(--c-player))] font-mono text-xs font-bold text-white"
                aria-hidden="true"
              >
                {name.charAt(0).toUpperCase()}
              </span>
              <span className="flex-1 truncate text-sm font-semibold text-foreground">{name}</span>
              <button
                type="button"
                onClick={() => onRemoveGuest(name)}
                className="rounded-sm px-2 py-0.5 text-xs font-semibold text-muted-foreground hover:text-[hsl(var(--c-danger))]"
                aria-label={`Rimuovi ${name}`}
              >
                ✕
              </button>
            </div>
          ))}
        </div>
      </div>

      <div className="grid gap-1.5">
        <label
          htmlFor="campaign-add-guest"
          className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground"
        >
          Aggiungi giocatore
        </label>
        <div className="flex gap-2">
          <input
            id="campaign-add-guest"
            type="text"
            value={guestInput}
            onChange={e => onGuestInputChange(e.target.value)}
            onKeyDown={e => {
              if (e.key === 'Enter') {
                e.preventDefault();
                onAddGuest();
              }
            }}
            placeholder="Nome giocatore"
            className="flex-1 rounded-md border border-input bg-background px-3 py-2 outline-none focus:border-[hsl(var(--c-session))] focus:ring-[3px] focus:ring-[hsl(var(--c-session)/0.18)]"
            data-testid="campaign-add-guest-input"
          />
          <button
            type="button"
            onClick={onAddGuest}
            disabled={guestInput.trim().length === 0}
            className="rounded-md border border-input bg-background px-4 py-2 text-sm font-semibold text-foreground hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50"
            data-testid="campaign-add-guest-button"
          >
            ＋ Aggiungi
          </button>
        </div>
      </div>
    </div>
  );
}

// ─── Step 3 · Confirm ───────────────────────────────────────────────────────

interface StepConfirmProps {
  readonly gameTitle: string;
  readonly campaignTitle: string;
  readonly preset: PresetConfig;
  readonly guests: readonly string[];
  readonly error: string | null;
}

function StepConfirm({
  gameTitle,
  campaignTitle,
  preset,
  guests,
  error,
}: StepConfirmProps): ReactElement {
  // #2917: summarize the real roster (host + guests), not the cosmetic preset.
  const playerNames = ['Tu', ...guests].join(' · ');
  return (
    <div className="grid gap-3">
      <article
        className="rounded-lg border border-[hsl(var(--c-session)/0.3)] bg-gradient-to-br from-[hsl(var(--c-session)/0.06)] to-[hsl(var(--c-game)/0.04)] p-4"
        data-testid="campaign-setup-review"
      >
        <div className="mb-3 flex items-center gap-3">
          <div className="relative flex h-14 w-14 shrink-0 items-center justify-center rounded-md bg-gradient-to-br from-[hsl(var(--c-session)/0.55)] to-[hsl(var(--c-game)/0.45)] text-xl">
            📖
          </div>
          <div className="flex-1">
            <h3 className="font-quicksand text-lg font-bold text-foreground">{campaignTitle}</h3>
            <p className="font-mono text-xs text-muted-foreground">
              {gameTitle} · {preset.title.replace(/^.*· /, '')}
            </p>
          </div>
        </div>
        <Row k="Preset" v={preset.title} />
        <Row k="Giocatori" v={playerNames} />
        <Row k="Lingua agente" v="Italiano" />
        <Row k="Durata stimata" v="3–4 ore" />
      </article>
      <div className="rounded-md border border-[hsl(var(--c-info)/0.25)] bg-[hsl(var(--c-info)/0.08)] p-3 text-sm text-muted-foreground">
        <strong className="text-[hsl(var(--c-info))]">ℹ️ Cosa succede ora:</strong> verrà creata una
        nuova campagna persistente. Potrai aprirla dal resume picker in qualsiasi momento.
      </div>
      {error && (
        <p className="rounded-md border border-[hsl(var(--c-danger)/0.4)] bg-[hsl(var(--c-danger)/0.08)] p-3 text-sm font-semibold text-[hsl(var(--c-danger))]">
          {error}
        </p>
      )}
    </div>
  );
}

function Row({ k, v }: { k: string; v: string }): ReactElement {
  return (
    <div className="flex items-center justify-between border-b border-dashed border-border py-1.5 text-sm last:border-b-0">
      <span className="font-mono text-[11px] uppercase tracking-wide text-muted-foreground">
        {k}
      </span>
      <span className="text-right font-semibold text-foreground">{v}</span>
    </div>
  );
}

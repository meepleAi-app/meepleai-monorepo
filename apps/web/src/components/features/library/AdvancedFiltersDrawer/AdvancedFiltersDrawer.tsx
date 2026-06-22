/**
 * AdvancedFiltersDrawer.
 *
 * SP4 mockup conformance (Issue #1585-followup, plan
 * docs/superpowers/plans/2026-06-03-library-sp4-mockup-conformance.md Task 3.3).
 * Mapped from `admin-mockups/design_files/sp4-library-desktop.jsx`
 * (AdvancedFiltersDrawer + DrawerSection, lines 312–637).
 *
 * REFACTORED from scope-conditional (5 entity variants × per-scope sections)
 * to **cross-entity hub-level** drawer with the static 7-section descriptor
 * from `DRAWER_SECTIONS`. The drawer no longer requires `entityScope`; one
 * unified `LibraryFilters` shape powers the draft state.
 *
 * Structure:
 *  - Custom header (icon box + title + active-count subtitle + close ✕)
 *  - Scrollable body with 7 `DrawerSection` accordion panels
 *    (chips-multi / select-multi / period-quick / range kinds)
 *  - Footer with Reset (transparent) + Applica (entity-agent primary, count)
 *
 * Built atop the shared `Drawer` primitive so Radix/Vaul mobile-bottom vs
 * desktop-right is handled for a11y + focus trap + Esc + backdrop, but the
 * inner markup is fully bespoke to match the mockup verbatim.
 */

'use client';

import {
  useEffect,
  useMemo,
  useState,
  type Dispatch,
  type ReactElement,
  type SetStateAction,
} from 'react';

import clsx from 'clsx';

import { Drawer, DrawerContent, DrawerDescription, DrawerTitle } from '@/components/ui/drawer';
import { useTranslation } from '@/hooks/useTranslation';

import {
  DRAWER_SECTIONS,
  isDefaultOpen,
  type ChipOption,
  type ChipsMultiSection,
  type EntityColorSlug,
  type PeriodQuickSection,
  type RangeSection,
  type SectionConfig,
  type SelectMultiSection,
} from './sections';
import { countActiveFilters, type AdvancedFiltersDrawerProps, type LibraryFilters } from './types';

type TranslateFn = ReturnType<typeof useTranslation>['t'];

export function AdvancedFiltersDrawer({
  open,
  onOpenChange,
  activeFilters,
  availableGames,
  onApply,
  onClear,
}: AdvancedFiltersDrawerProps): ReactElement {
  const { t } = useTranslation();
  const [draft, setDraft] = useState<LibraryFilters>(activeFilters);

  useEffect(() => {
    if (open) setDraft(activeFilters);
  }, [open, activeFilters]);

  const handleApply = () => {
    onApply(draft);
    onOpenChange(false);
  };

  const handleReset = () => {
    setDraft({});
    onClear();
  };

  const handleClose = () => {
    setDraft(activeFilters);
    onOpenChange(false);
  };

  const activeCount = useMemo(() => countActiveFilters(draft), [draft]);

  return (
    <Drawer open={open} onOpenChange={onOpenChange} entity="agent" direction="right">
      <DrawerContent
        data-slot="advanced-filters-drawer"
        className="flex h-full max-h-screen w-full max-w-[400px] flex-col gap-0 bg-background p-0 shadow-[-12px_0_40px_rgba(0,0,0,0.18)]"
      >
        <DrawerTitle className="sr-only">{t('pages.library.filters.title')}</DrawerTitle>
        <DrawerDescription className="sr-only">
          {t('pages.library.filters.description')}
        </DrawerDescription>

        <DrawerHeader t={t} activeCount={activeCount} onClose={handleClose} />

        <div className="flex-1 overflow-y-auto" data-slot="advanced-filters-sections">
          {DRAWER_SECTIONS.map((section, index) => (
            <DrawerSectionRenderer
              key={section.key}
              section={section}
              draft={draft}
              setDraft={setDraft}
              availableGames={availableGames}
              defaultOpen={isDefaultOpen(index)}
              t={t}
            />
          ))}
        </div>

        <DrawerFooterBar
          t={t}
          activeCount={activeCount}
          onReset={handleReset}
          onApply={handleApply}
        />
      </DrawerContent>
    </Drawer>
  );
}

// ─── Header ───────────────────────────────────────────────────────────────────

function DrawerHeader({
  t,
  activeCount,
  onClose,
}: {
  t: TranslateFn;
  activeCount: number;
  onClose: () => void;
}): ReactElement {
  return (
    <header
      data-slot="advanced-filters-header"
      className="flex items-center gap-2.5 border-b border-border px-4 py-3.5"
    >
      <span
        aria-hidden="true"
        className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-md bg-entity-agent/[0.12] text-sm text-entity-agent"
      >
        ⚙
      </span>
      <div className="flex flex-1 flex-col gap-px">
        <span className="font-display text-sm font-extrabold text-foreground">
          {t('pages.library.filters.title')}
        </span>
        <span
          data-slot="advanced-filters-active-count"
          className="font-mono text-[10px] font-bold uppercase tracking-[0.06em] text-muted-foreground"
        >
          {t('pages.library.filters.header.subtitle', { count: activeCount })}
        </span>
      </div>
      <button
        type="button"
        onClick={onClose}
        data-slot="advanced-filters-close"
        aria-label={t('pages.library.filters.closeAriaLabel')}
        className="inline-flex h-[30px] w-[30px] flex-shrink-0 items-center justify-center rounded-md border-0 bg-muted text-sm text-foreground hover:bg-muted/80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/60"
      >
        <span aria-hidden="true">✕</span>
      </button>
    </header>
  );
}

// ─── Footer ───────────────────────────────────────────────────────────────────

function DrawerFooterBar({
  t,
  activeCount,
  onReset,
  onApply,
}: {
  t: TranslateFn;
  activeCount: number;
  onReset: () => void;
  onApply: () => void;
}): ReactElement {
  return (
    <footer
      data-slot="advanced-filters-footer"
      className="flex flex-shrink-0 items-center gap-2 border-t border-border bg-card px-4 py-3"
    >
      <button
        type="button"
        onClick={onReset}
        data-slot="advanced-filters-reset"
        className="inline-flex items-center justify-center rounded-md border border-border-strong bg-transparent px-3.5 py-2 font-display text-[12.5px] font-bold text-muted-foreground hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/60"
      >
        {t('pages.library.filters.reset')}
      </button>
      <button
        type="button"
        onClick={onApply}
        data-slot="advanced-filters-apply"
        className="inline-flex flex-1 items-center justify-center rounded-md border-0 bg-entity-agent px-3.5 py-2 font-display text-[12.5px] font-extrabold text-white shadow-[0_3px_10px_hsl(var(--c-agent)/0.35)] hover:bg-entity-agent/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/60"
      >
        {activeCount > 0
          ? t('pages.library.filters.applyWithCount', { count: activeCount })
          : t('pages.library.filters.apply')}
      </button>
    </footer>
  );
}

// ─── Section renderer (accordion wrapper) ─────────────────────────────────────

interface DrawerSectionRendererProps {
  readonly section: SectionConfig;
  readonly draft: LibraryFilters;
  readonly setDraft: Dispatch<SetStateAction<LibraryFilters>>;
  readonly availableGames: AdvancedFiltersDrawerProps['availableGames'];
  readonly defaultOpen: boolean;
  readonly t: TranslateFn;
}

function DrawerSectionRenderer({
  section,
  draft,
  setDraft,
  availableGames,
  defaultOpen,
  t,
}: DrawerSectionRendererProps): ReactElement {
  const [open, setOpen] = useState(defaultOpen);
  const fillCount = countSectionFill(section, draft);

  return (
    <div
      data-slot={`advanced-filters-section-${section.key}`}
      data-testid={`advanced-filters-section-${section.key}`}
      className="border-b border-border/60"
    >
      <button
        type="button"
        onClick={() => setOpen(prev => !prev)}
        aria-expanded={open}
        aria-controls={`advanced-filters-section-body-${section.key}`}
        data-slot={`advanced-filters-section-toggle-${section.key}`}
        className="flex w-full items-center gap-2.5 border-0 bg-transparent px-[18px] py-3.5 text-left text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring/60"
      >
        <span aria-hidden="true" className="text-[13px] text-muted-foreground">
          {section.icon}
        </span>
        <span className="flex-1 font-display text-[13.5px] font-extrabold">
          {t(section.i18nLabel)}
        </span>
        {fillCount > 0 ? (
          <span
            data-slot={`advanced-filters-section-count-${section.key}`}
            className="rounded-full bg-entity-agent px-1.5 py-px font-mono text-[9px] font-extrabold text-white"
          >
            {fillCount}
          </span>
        ) : null}
        <span
          aria-hidden="true"
          className={clsx(
            'inline-block text-sm text-muted-foreground transition-transform duration-150',
            open ? 'rotate-90' : 'rotate-0'
          )}
        >
          ›
        </span>
      </button>
      {open ? (
        <div id={`advanced-filters-section-body-${section.key}`} className="px-[18px] pb-4">
          <SectionBody
            section={section}
            draft={draft}
            setDraft={setDraft}
            availableGames={availableGames}
            t={t}
          />
        </div>
      ) : null}
    </div>
  );
}

// ─── Kind-specific bodies ─────────────────────────────────────────────────────

interface SectionBodyProps {
  readonly section: SectionConfig;
  readonly draft: LibraryFilters;
  readonly setDraft: Dispatch<SetStateAction<LibraryFilters>>;
  readonly availableGames: AdvancedFiltersDrawerProps['availableGames'];
  readonly t: TranslateFn;
}

function SectionBody({
  section,
  draft,
  setDraft,
  availableGames,
  t,
}: SectionBodyProps): ReactElement {
  switch (section.kind) {
    case 'chips-multi':
      return <ChipsMultiBody section={section} draft={draft} setDraft={setDraft} t={t} />;
    case 'select-multi':
      return (
        <SelectMultiBody
          section={section}
          draft={draft}
          setDraft={setDraft}
          availableGames={availableGames}
          t={t}
        />
      );
    case 'period-quick':
      return <PeriodQuickBody section={section} draft={draft} setDraft={setDraft} t={t} />;
    case 'range':
      return <RangeBody section={section} draft={draft} setDraft={setDraft} t={t} />;
  }
}

// ─── chips-multi ───
function ChipsMultiBody({
  section,
  draft,
  setDraft,
  t,
}: {
  section: ChipsMultiSection;
  draft: LibraryFilters;
  setDraft: Dispatch<SetStateAction<LibraryFilters>>;
  t: TranslateFn;
}): ReactElement {
  const values = readStringArray(draft, section.key);

  const toggle = (val: string) => {
    setDraft(current => {
      const currentValues = readStringArray(current, section.key);
      const next = currentValues.includes(val)
        ? currentValues.filter(v => v !== val)
        : [...currentValues, val];
      return writeField(current, section.key, next.length > 0 ? next : undefined);
    });
  };

  return (
    <div data-slot={`advanced-filters-chips-${section.key}`} className="flex flex-wrap gap-1.5">
      {section.options.map(option => (
        <ChipsMultiOption
          key={option.value}
          option={option}
          active={values.includes(option.value)}
          onToggle={() => toggle(option.value)}
          label={t(option.i18nKey)}
        />
      ))}
    </div>
  );
}

function ChipsMultiOption({
  option,
  active,
  onToggle,
  label,
}: {
  option: ChipOption;
  active: boolean;
  onToggle: () => void;
  label: string;
}): ReactElement {
  const color: EntityColorSlug = option.color ?? 'game';
  return (
    <button
      type="button"
      onClick={onToggle}
      aria-pressed={active}
      data-slot={`advanced-filters-chip-${option.value}`}
      className={clsx(
        'inline-flex flex-shrink-0 items-center gap-1.5 rounded-full border px-2.5 py-1.5 font-display text-[11.5px] font-bold focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/60',
        active
          ? `${entityBgActiveClass(color)} ${entityBorderActiveClass(color)} ${entityTextClass(color)}`
          : 'border-border bg-card text-muted-foreground hover:bg-muted/30'
      )}
    >
      {option.icon ? <span aria-hidden="true">{option.icon}</span> : null}
      <span>{label}</span>
    </button>
  );
}

// ─── select-multi ───
function SelectMultiBody({
  section,
  draft,
  setDraft,
  availableGames,
  t,
}: {
  section: SelectMultiSection;
  draft: LibraryFilters;
  setDraft: Dispatch<SetStateAction<LibraryFilters>>;
  availableGames: AdvancedFiltersDrawerProps['availableGames'];
  t: TranslateFn;
}): ReactElement {
  const values = readStringArray(draft, section.key);

  const toggle = (id: string) => {
    setDraft(current => {
      const currentValues = readStringArray(current, section.key);
      const next = currentValues.includes(id)
        ? currentValues.filter(v => v !== id)
        : [...currentValues, id];
      return writeField(current, section.key, next.length > 0 ? next : undefined);
    });
  };

  const visibleOptions = (availableGames ?? []).slice(0, 5);

  return (
    <div data-slot={`advanced-filters-select-multi-${section.key}`} className="flex flex-col gap-2">
      <div
        data-slot={`advanced-filters-select-multi-placeholder-${section.key}`}
        className="rounded-md border border-border bg-card px-2.5 py-2 font-body text-xs text-muted-foreground"
      >
        {t(section.i18nPlaceholder)}
      </div>
      {visibleOptions.length === 0 ? (
        <div
          data-slot={`advanced-filters-select-multi-empty-${section.key}`}
          className="rounded-md bg-muted/30 px-2.5 py-2 font-body text-xs italic text-muted-foreground"
        >
          {t('pages.library.filters.section.game.empty')}
        </div>
      ) : (
        <div className="flex flex-wrap gap-1.5">
          {visibleOptions.map(option => {
            const active = values.includes(option.id);
            return (
              <button
                key={option.id}
                type="button"
                onClick={() => toggle(option.id)}
                aria-pressed={active}
                data-slot={`advanced-filters-select-multi-option-${section.key}-${option.id}`}
                className={clsx(
                  'inline-flex flex-shrink-0 items-center gap-1 rounded-full border px-2 py-1 font-mono text-[10.5px] font-bold focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/60',
                  active
                    ? 'border-entity-game/40 bg-entity-game/[0.12] text-entity-game'
                    : 'border-transparent bg-muted text-muted-foreground hover:bg-muted/80'
                )}
              >
                {active ? <span aria-hidden="true">✓</span> : null}
                <span>{option.title}</span>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}

// ─── period-quick ───
function PeriodQuickBody({
  section,
  draft,
  setDraft,
  t,
}: {
  section: PeriodQuickSection;
  draft: LibraryFilters;
  setDraft: Dispatch<SetStateAction<LibraryFilters>>;
  t: TranslateFn;
}): ReactElement {
  const value = readScalar(draft, section.key);

  const select = (val: string) => {
    setDraft(current => writeField(current, section.key, val));
  };

  return (
    <div
      data-slot={`advanced-filters-period-${section.key}`}
      role="radiogroup"
      aria-label={t(section.i18nLabel)}
      className="flex flex-col gap-1"
    >
      {section.options.map(option => {
        const active = value === option.value;
        return (
          <button
            key={option.value}
            type="button"
            role="radio"
            aria-checked={active}
            onClick={() => select(option.value)}
            data-slot={`advanced-filters-period-option-${option.value}`}
            className={clsx(
              'flex items-center gap-2 rounded-md border-0 px-2.5 py-1.5 text-left font-display text-[12.5px] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/60',
              active
                ? 'bg-entity-event/[0.12] font-extrabold text-entity-event'
                : 'bg-transparent font-semibold text-muted-foreground hover:bg-muted/30'
            )}
          >
            <span
              aria-hidden="true"
              className={clsx(
                'inline-block h-2.5 w-2.5 flex-shrink-0 rounded-full',
                active ? 'border-[3px] border-entity-event' : 'border-[1.5px] border-border-strong'
              )}
            />
            <span>{t(option.i18nKey)}</span>
          </button>
        );
      })}
    </div>
  );
}

// ─── range ───
function RangeBody({
  section,
  draft,
  setDraft,
  t,
}: {
  section: RangeSection;
  draft: LibraryFilters;
  setDraft: Dispatch<SetStateAction<LibraryFilters>>;
  t: TranslateFn;
}): ReactElement {
  const lo = readNumber(draft, section.minField) ?? section.defaultLo;
  const hi = readNumber(draft, section.maxField) ?? section.defaultHi;
  const total = section.max - section.min;
  const loPct = ((lo - section.min) / total) * 100;
  const hiPct = ((hi - section.min) / total) * 100;

  const setLo = (v: number) => {
    setDraft(current => writeField(current, section.minField, Math.min(v, hi)));
  };
  const setHi = (v: number) => {
    setDraft(current => writeField(current, section.maxField, Math.max(v, lo)));
  };

  return (
    <div data-slot={`advanced-filters-range-${section.key}`} className="flex flex-col gap-2">
      <div
        className="flex justify-between font-mono text-[11px] font-bold"
        data-slot={`advanced-filters-range-readout-${section.key}`}
      >
        <span className="text-entity-event">{formatRangeValue(lo, section.step)}</span>
        <span className="text-muted-foreground" aria-hidden="true">
          —
        </span>
        <span className="text-entity-event">{formatRangeValue(hi, section.step)}</span>
      </div>
      <div className="relative h-6">
        <div
          aria-hidden="true"
          className="absolute left-0 right-0 top-[11px] h-1 rounded-sm bg-muted"
        />
        <div
          aria-hidden="true"
          className="absolute top-[11px] h-1 rounded-sm bg-entity-event"
          style={{ left: `${loPct}%`, right: `${100 - hiPct}%` }}
        />
        <input
          type="range"
          min={section.min}
          max={section.max}
          step={section.step}
          value={lo}
          onChange={e => setLo(Number(e.target.value))}
          aria-label={t('pages.library.filters.section.rating.minAriaLabel')}
          className="absolute inset-x-0 top-0 h-6 w-full appearance-none bg-transparent [&::-webkit-slider-thumb]:pointer-events-auto"
          data-slot={`advanced-filters-range-min-${section.key}`}
        />
        <input
          type="range"
          min={section.min}
          max={section.max}
          step={section.step}
          value={hi}
          onChange={e => setHi(Number(e.target.value))}
          aria-label={t('pages.library.filters.section.rating.maxAriaLabel')}
          className="absolute inset-x-0 top-0 h-6 w-full appearance-none bg-transparent [&::-webkit-slider-thumb]:pointer-events-auto"
          data-slot={`advanced-filters-range-max-${section.key}`}
        />
      </div>
    </div>
  );
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function readStringArray(draft: LibraryFilters, key: string): ReadonlyArray<string> {
  const rec = draft as unknown as Record<string, unknown>;
  const v = rec[key];
  return Array.isArray(v) ? (v as ReadonlyArray<string>) : [];
}

function readScalar(draft: LibraryFilters, key: string): unknown {
  return (draft as unknown as Record<string, unknown>)[key];
}

function readNumber(draft: LibraryFilters, key: string): number | undefined {
  const v = (draft as unknown as Record<string, unknown>)[key];
  return typeof v === 'number' ? v : undefined;
}

function writeField(draft: LibraryFilters, key: string, value: unknown): LibraryFilters {
  const next = { ...(draft as unknown as Record<string, unknown>) };
  if (value === undefined) {
    delete next[key];
  } else {
    next[key] = value;
  }
  return next as unknown as LibraryFilters;
}

function countSectionFill(section: SectionConfig, draft: LibraryFilters): number {
  switch (section.kind) {
    case 'chips-multi':
    case 'select-multi':
      return readStringArray(draft, section.key).length;
    case 'period-quick': {
      const v = readScalar(draft, section.key);
      return typeof v === 'string' && v.length > 0 ? 1 : 0;
    }
    case 'range': {
      const lo = readNumber(draft, section.minField);
      const hi = readNumber(draft, section.maxField);
      return (lo !== undefined ? 1 : 0) + (hi !== undefined ? 1 : 0);
    }
  }
}

function formatRangeValue(value: number, step: number): string {
  return step >= 1 ? String(Math.round(value)) : value.toFixed(1);
}

// Entity color → Tailwind utility classes. Compile-time exhaustive map so the
// Tailwind compiler can statically extract the variants (no dynamic-string
// class generation, which would not be tree-shaken).
function entityBgActiveClass(color: EntityColorSlug): string {
  switch (color) {
    case 'game':
      return 'bg-entity-game/[0.14]';
    case 'agent':
      return 'bg-entity-agent/[0.14]';
    case 'kb':
      return 'bg-entity-kb/[0.14]';
    case 'session':
      return 'bg-entity-session/[0.14]';
    case 'chat':
      return 'bg-entity-chat/[0.14]';
    case 'event':
      return 'bg-entity-event/[0.14]';
    case 'toolkit':
      return 'bg-entity-toolkit/[0.14]';
    case 'player':
      return 'bg-entity-player/[0.14]';
  }
}

function entityBorderActiveClass(color: EntityColorSlug): string {
  switch (color) {
    case 'game':
      return 'border-entity-game/40';
    case 'agent':
      return 'border-entity-agent/40';
    case 'kb':
      return 'border-entity-kb/40';
    case 'session':
      return 'border-entity-session/40';
    case 'chat':
      return 'border-entity-chat/40';
    case 'event':
      return 'border-entity-event/40';
    case 'toolkit':
      return 'border-entity-toolkit/40';
    case 'player':
      return 'border-entity-player/40';
  }
}

function entityTextClass(color: EntityColorSlug): string {
  switch (color) {
    case 'game':
      return 'text-entity-game';
    case 'agent':
      return 'text-entity-agent';
    case 'kb':
      return 'text-entity-kb';
    case 'session':
      return 'text-entity-session';
    case 'chat':
      return 'text-entity-chat';
    case 'event':
      return 'text-entity-event';
    case 'toolkit':
      return 'text-entity-toolkit';
    case 'player':
      return 'text-entity-player';
  }
}

/**
 * AdvancedFiltersDrawer (Phase 3a #1606).
 *
 * Standalone reusable component that composes the existing `Drawer` primitive
 * with entity-conditional filter sections derived from `entityScope`. Owns its
 * own draft state internally; parent only observes via `onApply` / `onClear`
 * callbacks.
 */

'use client';

import { useState, type ReactElement } from 'react';

import {
  Drawer,
  DrawerContent,
  DrawerDescription,
  DrawerFooter,
  DrawerHeader,
  DrawerTitle,
} from '@/components/ui/drawer';
import { useTranslation } from '@/hooks/useTranslation';

import {
  getSectionsForScope,
  type CheckboxGroupSection,
  type RangeSection,
  type SectionConfig,
  type SliderSection,
  type ToggleSection,
} from './sections';

import type { AdvancedFiltersDrawerProps, LibraryFilters } from './types';

export function AdvancedFiltersDrawer({
  open,
  onOpenChange,
  entityScope,
  activeFilters,
  onApply,
  onClear,
}: AdvancedFiltersDrawerProps): ReactElement {
  const { t } = useTranslation();
  const [draft, setDraft] = useState<LibraryFilters>(activeFilters);

  const handleCancel = () => {
    setDraft(activeFilters);
    onOpenChange(false);
  };

  const sections = getSectionsForScope(entityScope);

  return (
    <Drawer open={open} onOpenChange={onOpenChange} entity={entityScope}>
      <DrawerContent data-slot="advanced-filters-drawer">
        <DrawerHeader>
          <DrawerTitle>{t('pages.library.filters.title')}</DrawerTitle>
          <DrawerDescription>{t('pages.library.filters.description')}</DrawerDescription>
        </DrawerHeader>

        <div className="flex flex-col gap-4 px-4 pb-4" data-slot="advanced-filters-sections">
          {sections.map(section => (
            <SectionRenderer
              key={section.key}
              section={section}
              draft={draft}
              onChange={setDraft}
              t={t}
            />
          ))}
        </div>

        <DrawerFooter>
          <button
            type="button"
            data-slot="advanced-filters-cancel"
            onClick={handleCancel}
            className="rounded-md border border-border bg-card px-3 py-1.5 text-sm font-medium text-foreground hover:bg-muted/40"
          >
            {t('common.cancel')}
          </button>
          {/* Clear + Apply buttons added in Task 7 */}
        </DrawerFooter>
      </DrawerContent>
    </Drawer>
  );

  // Silence unused warnings for symbols Task 7 will consume
  void onApply;
  void onClear;
}

// ─── Section renderer ────────────────────────────────────────────────────────

type TranslateFn = ReturnType<typeof useTranslation>['t'];

interface SectionRendererProps {
  readonly section: SectionConfig;
  readonly draft: LibraryFilters;
  readonly onChange: (next: LibraryFilters) => void;
  readonly t: TranslateFn;
}

function SectionRenderer({ section, draft, onChange, t }: SectionRendererProps): ReactElement {
  switch (section.kind) {
    case 'checkbox-group':
      return <CheckboxGroupRenderer section={section} draft={draft} onChange={onChange} t={t} />;
    case 'toggle':
      return <ToggleRenderer section={section} draft={draft} onChange={onChange} t={t} />;
    case 'slider':
      return <SliderRenderer section={section} draft={draft} onChange={onChange} t={t} />;
    case 'range':
      return <RangeRenderer section={section} draft={draft} onChange={onChange} t={t} />;
  }
}

function CheckboxGroupRenderer({
  section,
  draft,
  onChange,
  t,
}: {
  section: CheckboxGroupSection;
  draft: LibraryFilters;
  onChange: (next: LibraryFilters) => void;
  t: TranslateFn;
}): ReactElement {
  const draftValues = readArrayField(draft, section.key);
  const toggle = (val: string) => {
    const next = draftValues.includes(val)
      ? draftValues.filter(v => v !== val)
      : [...draftValues, val];
    onChange(writeArrayField(draft, section.key, next));
  };
  return (
    <div
      data-slot={`advanced-filters-section-${section.key}`}
      data-testid={`advanced-filters-section-${section.key}`}
      className="flex flex-col gap-2"
    >
      <span className="text-sm font-medium text-foreground">{t(section.i18nLabel)}</span>
      <div className="flex flex-wrap gap-2">
        {section.options.map(opt => (
          <label key={opt.value} className="inline-flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={draftValues.includes(opt.value)}
              onChange={() => toggle(opt.value)}
              aria-label={t(opt.i18nKey)}
              className="h-4 w-4 rounded border-border"
            />
            <span>{t(opt.i18nKey)}</span>
          </label>
        ))}
      </div>
    </div>
  );
}

function ToggleRenderer({
  section,
  draft,
  onChange,
  t,
}: {
  section: ToggleSection;
  draft: LibraryFilters;
  onChange: (next: LibraryFilters) => void;
  t: TranslateFn;
}): ReactElement {
  const checked = Boolean(readScalarField(draft, section.key));
  return (
    <div
      data-slot={`advanced-filters-section-${section.key}`}
      data-testid={`advanced-filters-section-${section.key}`}
      className="flex items-center justify-between"
    >
      <span className="text-sm font-medium text-foreground">{t(section.i18nLabel)}</span>
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        aria-label={t(section.i18nLabel)}
        onClick={() => onChange(writeScalarField(draft, section.key, !checked))}
        className={`relative h-6 w-11 rounded-full transition-colors ${checked ? 'bg-primary' : 'bg-muted'}`}
      >
        <span
          aria-hidden="true"
          className={`absolute top-0.5 h-5 w-5 rounded-full bg-card transition-transform ${checked ? 'translate-x-5' : 'translate-x-0.5'}`}
        />
      </button>
    </div>
  );
}

function SliderRenderer({
  section,
  draft,
  onChange,
  t,
}: {
  section: SliderSection;
  draft: LibraryFilters;
  onChange: (next: LibraryFilters) => void;
  t: TranslateFn;
}): ReactElement {
  const fieldKey = sliderFieldKey(section.key);
  const value = readNumberField(draft, fieldKey) ?? section.min;
  return (
    <div
      data-slot={`advanced-filters-section-${section.key}`}
      data-testid={`advanced-filters-section-${section.key}`}
      className="flex flex-col gap-2"
    >
      <span className="text-sm font-medium text-foreground">
        {t(section.i18nLabel)} <span className="text-muted-foreground">({value})</span>
      </span>
      <input
        type="range"
        role="slider"
        aria-label={t(section.i18nLabel)}
        aria-valuemin={section.min}
        aria-valuemax={section.max}
        aria-valuenow={value}
        min={section.min}
        max={section.max}
        step={section.step}
        value={value}
        onChange={e => onChange(writeNumberField(draft, fieldKey, Number(e.target.value)))}
        className="w-full"
      />
    </div>
  );
}

function RangeRenderer({
  section,
  draft,
  onChange,
  t,
}: {
  section: RangeSection;
  draft: LibraryFilters;
  onChange: (next: LibraryFilters) => void;
  t: TranslateFn;
}): ReactElement {
  const [minField, maxField] = rangeFieldKeys(section.key);
  const minVal = readNumberField(draft, minField) ?? section.min;
  const maxVal = readNumberField(draft, maxField) ?? section.max;
  return (
    <div
      data-slot={`advanced-filters-section-${section.key}`}
      data-testid={`advanced-filters-section-${section.key}`}
      className="flex flex-col gap-2"
    >
      <span className="text-sm font-medium text-foreground">
        {t(section.i18nLabel)}{' '}
        <span className="text-muted-foreground">
          ({minVal}–{maxVal})
        </span>
      </span>
      <div className="grid grid-cols-2 gap-2">
        <input
          type="number"
          aria-label={`${t(section.i18nLabel)} min`}
          min={section.min}
          max={section.max}
          step={section.step}
          value={minVal}
          onChange={e => onChange(writeNumberField(draft, minField, Number(e.target.value)))}
          className="rounded border border-border bg-background px-2 py-1 text-sm"
        />
        <input
          type="number"
          aria-label={`${t(section.i18nLabel)} max`}
          min={section.min}
          max={section.max}
          step={section.step}
          value={maxVal}
          onChange={e => onChange(writeNumberField(draft, maxField, Number(e.target.value)))}
          className="rounded border border-border bg-background px-2 py-1 text-sm"
        />
      </div>
    </div>
  );
}

// ─── Field helpers (scope-aware read/write into LibraryFilters union) ───────

function readArrayField(draft: LibraryFilters, key: string): ReadonlyArray<string> {
  const rec = draft as unknown as Record<string, unknown>;
  const v = rec[key];
  return Array.isArray(v) ? (v as ReadonlyArray<string>) : [];
}

function writeArrayField(
  draft: LibraryFilters,
  key: string,
  value: ReadonlyArray<string>
): LibraryFilters {
  const merged = { ...(draft as unknown as Record<string, unknown>), [key]: value };
  return merged as unknown as LibraryFilters;
}

function readScalarField(draft: LibraryFilters, key: string): unknown {
  return (draft as unknown as Record<string, unknown>)[key];
}

function writeScalarField(draft: LibraryFilters, key: string, value: unknown): LibraryFilters {
  const merged = { ...(draft as unknown as Record<string, unknown>), [key]: value };
  return merged as unknown as LibraryFilters;
}

function readNumberField(draft: LibraryFilters, key: string): number | undefined {
  const v = (draft as unknown as Record<string, unknown>)[key];
  return typeof v === 'number' ? v : undefined;
}

function writeNumberField(draft: LibraryFilters, key: string, value: number): LibraryFilters {
  const merged = { ...(draft as unknown as Record<string, unknown>), [key]: value };
  return merged as unknown as LibraryFilters;
}

function sliderFieldKey(sectionKey: string): string {
  if (sectionKey === 'rating') return 'ratingMin';
  if (sectionKey === 'playerCount') return 'playerCountMin';
  if (sectionKey === 'messageCountMin') return 'messageCountMin';
  return sectionKey;
}

function rangeFieldKeys(sectionKey: string): readonly [string, string] {
  if (sectionKey === 'players') return ['playersMin', 'playersMax'];
  if (sectionKey === 'year') return ['yearMin', 'yearMax'];
  return [`${sectionKey}Min`, `${sectionKey}Max`];
}

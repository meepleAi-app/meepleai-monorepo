/**
 * FilterAccordion — Phase 3 #1737 / Issue #1482 component #4.
 *
 * Three server-side facets backed by BE PR #1730 (#1686):
 *   - DocType  : multi-select, 7-value allowlist (D-6 #1686); drops `faq` per DEC-7
 *   - GameId   : multi-select, dropdown of accessible games (DEC-1: derived from useUserKbDocs)
 *   - Language : single-select, 5-value ISO 639-1 allowlist (D-7 #1686)
 *
 * Controlled component — parent owns `selected` state (URL SSOT in KbGlobaleView)
 * and reacts to `onChange`.
 *
 * Empty selection = no filter (D-3/D-5 #1686, byte-identical baseline).
 *
 * a11y: each facet section is a <details>+<summary> for native accordion;
 * checkboxes/radios for inputs; visible clearAll button at top.
 */
'use client';

import { type JSX } from 'react';

import type { GlobalKbSearchFilters } from '@/lib/api/schemas/kb-globale.schemas';

export interface FilterAccordionLabels {
  heading: string;
  docTypeLabel: string;
  gameIdLabel: string;
  languageLabel: string;
  clearAll: string;
  /** Map: BE enum string → display label */
  docTypeOptions: Readonly<Record<string, string>>;
  languageOptions: Readonly<Record<string, string>>;
}

export interface GameOption {
  id: string;
  name: string;
}

export interface FilterAccordionProps {
  availableGames: readonly GameOption[];
  selected: GlobalKbSearchFilters;
  onChange: (next: GlobalKbSearchFilters) => void;
  labels: FilterAccordionLabels;
}

const DOC_TYPE_ALLOWLIST = [
  'Rulebook',
  'Expansion',
  'Errata',
  'QuickStart',
  'Reference',
  'PlayerAid',
  'Other',
] as const;

const LANGUAGE_ALLOWLIST = ['en', 'it', 'de', 'fr', 'es'] as const;

export function FilterAccordion(props: FilterAccordionProps): JSX.Element {
  const { availableGames, selected, onChange, labels } = props;
  const selectedDocTypes = selected.docType ?? [];
  const selectedGameIds = selected.gameId ?? [];
  const selectedLanguage = selected.language ?? '';

  const toggleDocType = (value: string) => {
    const next = selectedDocTypes.includes(value)
      ? selectedDocTypes.filter(v => v !== value)
      : [...selectedDocTypes, value];
    onChange({ ...selected, docType: next.length > 0 ? next : undefined });
  };

  const toggleGameId = (id: string) => {
    const next = selectedGameIds.includes(id)
      ? selectedGameIds.filter(v => v !== id)
      : [...selectedGameIds, id];
    onChange({ ...selected, gameId: next.length > 0 ? next : undefined });
  };

  const setLanguage = (lang: string) => {
    onChange({ ...selected, language: lang || undefined });
  };

  const clearAll = () => onChange({});

  const hasAnyFilter =
    selectedDocTypes.length > 0 || selectedGameIds.length > 0 || selectedLanguage !== '';

  return (
    <aside
      className="bg-card border border-border rounded-lg p-4 flex flex-col gap-3"
      aria-label={labels.heading}
    >
      <div className="flex items-center justify-between">
        <h2 className="font-semibold text-foreground">{labels.heading}</h2>
        {hasAnyFilter && (
          <button
            type="button"
            onClick={clearAll}
            className="text-sm text-muted-foreground hover:text-foreground underline"
          >
            {labels.clearAll}
          </button>
        )}
      </div>

      {/* DocType facet */}
      <details className="border-t border-border-strong pt-2">
        {/* role="button" is the HTML spec implicit role for <summary>; explicit for jsdom compat */}
        <summary role="button" className="cursor-pointer font-medium text-sm text-foreground">
          {labels.docTypeLabel}
          {selectedDocTypes.length > 0 && (
            <span className="ml-2 text-muted-foreground">({selectedDocTypes.length})</span>
          )}
        </summary>
        <div className="mt-2 flex flex-col gap-1">
          {DOC_TYPE_ALLOWLIST.map(opt => (
            <label key={opt} className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={selectedDocTypes.includes(opt)}
                onChange={() => toggleDocType(opt)}
              />
              <span>{labels.docTypeOptions[opt] ?? opt}</span>
            </label>
          ))}
        </div>
      </details>

      {/* GameId facet */}
      <details className="border-t border-border-strong pt-2">
        {/* role="button" is the HTML spec implicit role for <summary>; explicit for jsdom compat */}
        <summary role="button" className="cursor-pointer font-medium text-sm text-foreground">
          {labels.gameIdLabel}
          {selectedGameIds.length > 0 && (
            <span className="ml-2 text-muted-foreground">({selectedGameIds.length})</span>
          )}
        </summary>
        <div className="mt-2 flex flex-col gap-1 max-h-60 overflow-auto">
          {availableGames.map(g => (
            <label key={g.id} className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={selectedGameIds.includes(g.id)}
                onChange={() => toggleGameId(g.id)}
              />
              <span>{g.name}</span>
            </label>
          ))}
        </div>
      </details>

      {/* Language facet */}
      <details className="border-t border-border-strong pt-2">
        {/* role="button" is the HTML spec implicit role for <summary>; explicit for jsdom compat */}
        <summary role="button" className="cursor-pointer font-medium text-sm text-foreground">
          {labels.languageLabel}
          {selectedLanguage && (
            <span className="ml-2 text-muted-foreground">
              ({labels.languageOptions[selectedLanguage] ?? selectedLanguage})
            </span>
          )}
        </summary>
        <div className="mt-2 flex flex-col gap-1" role="radiogroup">
          <label className="flex items-center gap-2 text-sm">
            <input
              type="radio"
              name="kb-globale-language"
              checked={selectedLanguage === ''}
              onChange={() => setLanguage('')}
            />
            <span className="text-muted-foreground">—</span>
          </label>
          {LANGUAGE_ALLOWLIST.map(lang => (
            <label key={lang} className="flex items-center gap-2 text-sm">
              <input
                type="radio"
                name="kb-globale-language"
                checked={selectedLanguage === lang}
                onChange={() => setLanguage(lang)}
              />
              <span>{labels.languageOptions[lang] ?? lang}</span>
            </label>
          ))}
        </div>
      </details>
    </aside>
  );
}

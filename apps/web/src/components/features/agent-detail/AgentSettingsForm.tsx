/**
 * AgentSettingsForm - v2 Wave C.2 (Issue #581) → real editor (Issue #2732)
 *
 * Mapped from `admin-mockups/design_files/sp4-agent-detail.jsx` (SettingsTab).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 *
 * Issue #2732: the Settings tab is now a REAL per-game AI config editor. The
 * field UI is shared with the library `AgentConfigModal` via the extracted
 * `AgentConfigFields` component. The form owns a local editable draft seeded
 * from the incoming config and reports the edited config through `onSave`.
 *
 * 4-state discriminated union:
 *   - `loading`: shimmer skeleton
 *   - `error`: error message + retry button
 *   - `editable`: active agent w/ game — editable fields + per-game note + Save/Cancel
 *   - `read-only`: archived OR standalone agent — disabled fields + reason banner (no Save)
 *
 * A11y: read-only banner has `role="status"`; the per-game note has `role="note"`.
 */

'use client';

import { useEffect, useState, type ReactElement } from 'react';

import clsx from 'clsx';

import { AgentConfigFields, type AgentConfigFieldsValue } from '@/components/agent/config';

/** Config shape edited by this form — shared with the library AgentConfigModal. */
export type AgentConfig = AgentConfigFieldsValue;

export interface AgentSettingsFormLabels {
  readonly title: string;
  readonly strategyLabel: string;
  readonly parametersLabel: string;
  readonly readOnlyBanner: string;
  readonly perGameNote: string;
  readonly standaloneBanner: string;
  readonly saveCta: string;
  readonly cancelCta: string;
  readonly saveSuccess: string;
  readonly saveError: string;
  readonly loadingLabel: string;
  readonly errorLabel: string;
  readonly retryLabel: string;
}

/**
 * Discriminated union.
 * - `editable`: active agent with a game — can save changes
 * - `read-only`: archived (`reason: 'archived'`) OR standalone (`reason: 'standalone'`)
 *   agent — display only, no Save CTA
 */
export type SettingsState =
  | { kind: 'loading' }
  | { kind: 'error'; retry: () => void }
  | { kind: 'editable'; config: AgentConfigFieldsValue }
  | { kind: 'read-only'; config: AgentConfigFieldsValue; reason: 'archived' | 'standalone' };

export interface AgentSettingsFormProps {
  readonly state: SettingsState;
  readonly labels: AgentSettingsFormLabels;
  readonly onSave: (config: AgentConfigFieldsValue) => void;
  readonly onCancel: () => void;
  readonly className?: string;
}

export function AgentSettingsForm(props: AgentSettingsFormProps): ReactElement {
  const { state, labels, onSave, onCancel, className } = props;

  // The seed config exists only in the editable/read-only states.
  const seed = state.kind === 'editable' || state.kind === 'read-only' ? state.config : null;

  const [draft, setDraft] = useState<AgentConfigFieldsValue | null>(seed);

  // Resync the local draft whenever the *incoming* config actually changes.
  // Object identity churns every render, so compare structurally via
  // JSON.stringify: in-flight edits survive re-renders but reset after a real
  // config change (refetch, tab reload, variant switch).
  const seedKey = JSON.stringify(seed);
  useEffect(() => {
    setDraft(seed);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- structural key, not identity
  }, [seedKey]);

  return (
    <section
      data-slot="agent-detail-settings-form"
      data-settings-kind={state.kind}
      className={clsx('flex flex-col gap-4', className)}
    >
      {/* Section header */}
      <h3 className="font-display text-[15px] font-extrabold text-foreground">{labels.title}</h3>

      {/* Loading state */}
      {state.kind === 'loading' ? (
        <div className="flex flex-col gap-4" aria-label={labels.loadingLabel} aria-busy="true">
          {[0, 1, 2].map(i => (
            <div key={i} className="h-16 animate-pulse rounded-xl bg-muted" aria-hidden="true" />
          ))}
        </div>
      ) : null}

      {/* Error state */}
      {state.kind === 'error' ? (
        <div className="flex flex-col items-center gap-3 rounded-xl border border-rose-200 bg-rose-50 px-6 py-8 text-center dark:border-rose-900/40 dark:bg-rose-950/20">
          <span aria-hidden="true" className="text-2xl">
            ⚠
          </span>
          <p className="font-display text-[13px] font-semibold text-rose-700 dark:text-rose-300">
            {labels.errorLabel}
          </p>
          <button
            type="button"
            onClick={state.retry}
            className="inline-flex items-center gap-1.5 rounded-lg bg-rose-700 px-4 py-2 font-display text-[12px] font-bold text-white hover:bg-rose-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-700"
          >
            {labels.retryLabel}
          </button>
        </div>
      ) : null}

      {/* Read-only state (archived or standalone) */}
      {state.kind === 'read-only' ? (
        <div className="flex flex-col gap-4">
          <div
            role="status"
            className="flex items-center gap-2.5 rounded-xl border border-border bg-muted px-4 py-3"
          >
            <span aria-hidden="true" className="text-base">
              🔒
            </span>
            <p className="font-display text-[12.5px] font-semibold text-muted-foreground">
              {state.reason === 'standalone' ? labels.standaloneBanner : labels.readOnlyBanner}
            </p>
          </div>
          <AgentConfigFields
            value={draft ?? state.config}
            onChange={() => {}}
            disabled
            idPrefix="agent-detail-ro"
          />
        </div>
      ) : null}

      {/* Editable state (active + game) */}
      {state.kind === 'editable' ? (
        <div className="flex flex-col gap-5">
          <div className="rounded-xl border border-border bg-card px-5 py-5 shadow-sm">
            <AgentConfigFields
              value={draft ?? state.config}
              onChange={setDraft}
              idPrefix="agent-detail"
            />
          </div>

          {/* Per-game scope note */}
          <p role="note" className="font-display text-[12.5px] font-semibold text-muted-foreground">
            {labels.perGameNote}
          </p>

          {/* Action bar */}
          <div className="flex items-center justify-end gap-3">
            <button
              type="button"
              onClick={() => {
                setDraft(seed);
                onCancel();
              }}
              className="inline-flex items-center rounded-lg border border-border px-4 py-2.5 font-display text-[13px] font-bold text-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              {labels.cancelCta}
            </button>
            <button
              type="button"
              onClick={() => onSave(draft ?? state.config)}
              className="inline-flex items-center gap-1.5 rounded-lg bg-violet-700 px-4 py-2.5 font-display text-[13px] font-extrabold text-white shadow-sm hover:bg-violet-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-700 focus-visible:ring-offset-2"
            >
              {labels.saveCta}
            </button>
          </div>
        </div>
      ) : null}
    </section>
  );
}

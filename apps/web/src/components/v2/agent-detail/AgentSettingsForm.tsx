/**
 * AgentSettingsForm - v2 Wave C.2 (Issue #581)
 *
 * Mapped from `admin-mockups/design_files/sp4-agent-detail.jsx` (SettingsTab).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #581)
 *
 * Pure presentational component — no hooks, no i18n calls, no data fetching.
 * Variant-aware: archived agent → read-only (no Save CTA).
 *
 * 4-state discriminated union per Phase 0.5 contract sez. 4.3:
 *   - `loading`: shimmer skeleton
 *   - `error`: error message + retry button
 *   - `editable`: active agent — form fields + Save/Cancel buttons
 *   - `read-only`: archived agent — display-only + read-only banner (no Save)
 *
 * A11y: read-only banner has `role="status"` (informational).
 *
 * AC: T A V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface AgentConfig {
  readonly strategy: string;
  readonly parameters: Record<string, unknown>;
}

export interface AgentSettingsFormLabels {
  readonly title: string;
  readonly strategyLabel: string;
  readonly parametersLabel: string;
  readonly readOnlyBanner: string;
  readonly saveCta: string;
  readonly cancelCta: string;
  readonly saveSuccess: string;
  readonly saveError: string;
  readonly loadingLabel: string;
  readonly errorLabel: string;
  readonly retryLabel: string;
}

/**
 * Discriminated union per Phase 0.5 contract sez. 4.3.
 * - `editable`: active agent — can save changes
 * - `read-only`: archived agent — display only, no Save CTA
 */
export type SettingsState =
  | { kind: 'loading' }
  | { kind: 'error'; retry: () => void }
  | { kind: 'editable'; config: AgentConfig }
  | { kind: 'read-only'; config: AgentConfig };

export interface AgentSettingsFormProps {
  readonly state: SettingsState;
  readonly labels: AgentSettingsFormLabels;
  readonly onSave: (config: AgentConfig) => void;
  readonly onCancel: () => void;
  readonly className?: string;
}

export function AgentSettingsForm(props: AgentSettingsFormProps): ReactElement {
  const { state, labels, onSave, onCancel, className } = props;

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

      {/* Read-only state (archived) */}
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
              {labels.readOnlyBanner}
            </p>
          </div>
          <ConfigDisplay config={state.config} labels={labels} />
        </div>
      ) : null}

      {/* Editable state (active) */}
      {state.kind === 'editable' ? (
        <div className="flex flex-col gap-5">
          <div className="rounded-xl border border-border bg-card px-5 py-5 shadow-sm">
            <ConfigDisplay config={state.config} labels={labels} />
          </div>

          {/* Action bar */}
          <div className="flex items-center justify-end gap-3">
            <button
              type="button"
              onClick={onCancel}
              className="inline-flex items-center rounded-lg border border-border px-4 py-2.5 font-display text-[13px] font-bold text-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              {labels.cancelCta}
            </button>
            <button
              type="button"
              onClick={() => onSave(state.config)}
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

/** Internal pure helper — renders config fields. */
function ConfigDisplay({
  config,
  labels,
}: {
  config: AgentConfig;
  labels: AgentSettingsFormLabels;
}): ReactElement {
  return (
    <div className="flex flex-col gap-4">
      <div>
        <label className="mb-1.5 block font-display text-[12px] font-extrabold uppercase tracking-[0.06em] text-muted-foreground">
          {labels.strategyLabel}
        </label>
        <p className="rounded-lg border border-border bg-muted/30 px-3.5 py-2.5 font-mono text-[13px] text-foreground">
          {config.strategy}
        </p>
      </div>
      <div>
        <label className="mb-1.5 block font-display text-[12px] font-extrabold uppercase tracking-[0.06em] text-muted-foreground">
          {labels.parametersLabel}
        </label>
        <pre className="overflow-x-auto rounded-lg border border-border bg-muted/30 px-3.5 py-2.5 font-mono text-[11px] text-foreground [white-space:pre-wrap]">
          {JSON.stringify(config.parameters, null, 2)}
        </pre>
      </div>
    </div>
  );
}

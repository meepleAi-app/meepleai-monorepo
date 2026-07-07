/**
 * AgentSettingsForm - v2 Wave C.2 (Issue #581), editable via AgentConfigFields (Issue #2732).
 *
 * The /agents/[id] Settings tab editor for the per-game AI agent config. Uses
 * the shared `AgentConfigFields` (same UI as the library modal).
 *
 * 4-state discriminated union:
 *   - `loading`: shimmer skeleton
 *   - `error`: error message + retry button
 *   - `editable`: active agent with a game — editable fields + per-game note + Save/Cancel
 *   - `read-only`: archived OR standalone agent — disabled fields + reason banner (no Save)
 *
 * A11y: banners use `role="status"` (informational).
 */

'use client';

import { useEffect, useRef, useState, type ReactElement } from 'react';

import clsx from 'clsx';

import { AgentConfigFields, type AgentConfigFieldsValue } from '@/components/agent/config';

export interface AgentSettingsFormLabels {
  readonly title: string;
  readonly strategyLabel: string;
  readonly parametersLabel: string;
  readonly readOnlyBanner: string;
  readonly readOnlyStandalone: string;
  readonly perGameNote: string;
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
 * - `read-only`: archived (reason='archived') or standalone (reason='standalone')
 */
export type SettingsState =
  | { kind: 'loading' }
  | { kind: 'error'; retry: () => void }
  | { kind: 'editable'; value: AgentConfigFieldsValue }
  | { kind: 'read-only'; value: AgentConfigFieldsValue; readOnlyReason: 'archived' | 'standalone' };

export interface AgentSettingsFormProps {
  readonly state: SettingsState;
  readonly labels: AgentSettingsFormLabels;
  readonly onSave: (value: AgentConfigFieldsValue) => void;
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
      <h3 className="font-display text-[15px] font-extrabold text-foreground">{labels.title}</h3>

      {state.kind === 'loading' ? (
        <div className="flex flex-col gap-4" aria-label={labels.loadingLabel} aria-busy="true">
          {[0, 1, 2].map(i => (
            <div key={i} className="h-16 animate-pulse rounded-xl bg-muted" aria-hidden="true" />
          ))}
        </div>
      ) : null}

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

      {state.kind === 'read-only' ? (
        <ReadOnlyView value={state.value} reason={state.readOnlyReason} labels={labels} />
      ) : null}

      {state.kind === 'editable' ? (
        <EditableView value={state.value} labels={labels} onSave={onSave} onCancel={onCancel} />
      ) : null}
    </section>
  );
}

/** Read-only view: disabled fields + reason-specific banner. */
function ReadOnlyView({
  value,
  reason,
  labels,
}: {
  value: AgentConfigFieldsValue;
  reason: 'archived' | 'standalone';
  labels: AgentSettingsFormLabels;
}): ReactElement {
  const banner = reason === 'standalone' ? labels.readOnlyStandalone : labels.readOnlyBanner;
  return (
    <div className="flex flex-col gap-4">
      <div
        role="status"
        className="flex items-center gap-2.5 rounded-xl border border-border bg-muted px-4 py-3"
      >
        <span aria-hidden="true" className="text-base">
          🔒
        </span>
        <p className="font-display text-[12.5px] font-semibold text-muted-foreground">{banner}</p>
      </div>
      <div className="rounded-xl border border-border bg-card px-5 py-5 shadow-sm">
        <AgentConfigFields value={value} onChange={() => {}} disabled />
      </div>
    </div>
  );
}

/** Editable view: owns local edit state, resynced when the source config changes. */
function EditableView({
  value,
  labels,
  onSave,
  onCancel,
}: {
  value: AgentConfigFieldsValue;
  labels: AgentSettingsFormLabels;
  onSave: (value: AgentConfigFieldsValue) => void;
  onCancel: () => void;
}): ReactElement {
  const [edited, setEdited] = useState<AgentConfigFieldsValue>(value);
  // True once the user has touched a field: guards against a background refetch
  // (refetchOnWindowFocus) clobbering unsaved edits. Reset on save/cancel.
  const dirtyRef = useRef(false);

  // Resync when the loaded config content changes (e.g. after a refetch). Depend
  // on the primitive fields — the `value` prop is a fresh object each render.
  const { llmModel, temperature, maxTokens, personality, detailLevel, personalNotes } = value;
  useEffect(() => {
    if (dirtyRef.current) return; // preserve in-progress edits
    setEdited({ llmModel, temperature, maxTokens, personality, detailLevel, personalNotes });
  }, [llmModel, temperature, maxTokens, personality, detailLevel, personalNotes]);

  return (
    <div className="flex flex-col gap-5">
      <div
        role="status"
        className="flex items-center gap-2.5 rounded-xl border border-border bg-muted px-4 py-3"
      >
        <span aria-hidden="true" className="text-base">
          ℹ️
        </span>
        <p className="font-display text-[12.5px] font-semibold text-muted-foreground">
          {labels.perGameNote}
        </p>
      </div>

      <div className="rounded-xl border border-border bg-card px-5 py-5 shadow-sm">
        <AgentConfigFields
          value={edited}
          onChange={patch => {
            dirtyRef.current = true;
            setEdited(prev => ({ ...prev, ...patch }));
          }}
        />
      </div>

      <div className="flex items-center justify-end gap-3">
        <button
          type="button"
          onClick={() => {
            dirtyRef.current = false;
            setEdited(value);
            onCancel();
          }}
          className="inline-flex items-center rounded-lg border border-border px-4 py-2.5 font-display text-[13px] font-bold text-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {labels.cancelCta}
        </button>
        <button
          type="button"
          onClick={() => {
            dirtyRef.current = false;
            onSave(edited);
          }}
          className="inline-flex items-center gap-1.5 rounded-lg bg-violet-700 px-4 py-2.5 font-display text-[13px] font-extrabold text-white shadow-sm hover:bg-violet-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-700 focus-visible:ring-offset-2"
        >
          {labels.saveCta}
        </button>
      </div>
    </div>
  );
}

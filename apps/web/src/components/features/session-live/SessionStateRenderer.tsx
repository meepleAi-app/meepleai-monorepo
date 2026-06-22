/**
 * SessionStateRenderer — G7 polymorphic discriminated-union primitive.
 *
 * Renders one of 4 canonical panel-local session-live states (per spec
 * `2026-06-14-issue-2281-session-skeleton-g2-g4-g7-scope.md` §G7):
 *
 *   - default → children passthrough (no wrapper)
 *   - empty   → existing `EmptyState` component
 *   - loading → minimal inline skeleton
 *   - error   → minimal inline error banner + retry CTA
 *
 * SSE connection lost is **NOT** a panel state — per ADR-071 follow-up (#2383),
 * the SSE disconnect banner is rendered at orchestrator level (see
 * `SessionLiveView.tsx` `showConnectionBanner` flag + `ConnectionLostBanner`
 * page-level mount). Splitting the banner across panels would multiply sources
 * of truth for `ConnectionLostBannerLabels` (label prop drilling).
 *
 * Exhaustiveness is enforced by TypeScript: adding a 5th state requires
 * a code change AND a type change (the `assertNever` default catches drift).
 *
 * @see docs/superpowers/specs/2026-06-14-issue-2281-session-skeleton-g2-g4-g7-scope.md §G7
 * @see docs/for-claude/architecture/adr/adr-071-live-session-5-state-fsm.md (Option B)
 * @see Issue #2356 (G7 primitive), #2383 (ADR-071 refactor), parent #2342 (umbrella)
 */

import type { ReactElement, ReactNode } from 'react';

import { AlertCircle, RefreshCw } from 'lucide-react';

import { EmptyState } from '@/components/empty-state/EmptyState';

// ─── Types ────────────────────────────────────────────────────────────────────

export type SessionLiveState =
  | { kind: 'default'; children: ReactNode }
  | {
      kind: 'empty';
      title: string;
      description?: string;
      emptyCta?: { label: string; onClick: () => void };
    }
  | { kind: 'loading'; loadingAriaLabel: string }
  | {
      kind: 'error';
      error: Error;
      onRetry: () => void;
      errorTitle: string;
      retryLabel: string;
    };

export interface SessionStateRendererProps {
  readonly state: SessionLiveState;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function SessionStateRenderer({ state }: SessionStateRendererProps): ReactElement {
  switch (state.kind) {
    case 'default':
      return <>{state.children}</>;

    case 'empty':
      return (
        <div data-slot="session-state-empty">
          <EmptyState
            title={state.title}
            description={state.description}
            action={
              state.emptyCta
                ? { label: state.emptyCta.label, onClick: state.emptyCta.onClick }
                : undefined
            }
          />
        </div>
      );

    case 'loading':
      return (
        <div
          data-slot="session-state-loading"
          role="status"
          aria-live="polite"
          aria-label={state.loadingAriaLabel}
          className="space-y-3 p-6"
        >
          <div className="h-6 w-1/3 animate-pulse rounded-md bg-muted/50" />
          <div className="h-32 animate-pulse rounded-lg bg-muted/40" />
          <div className="h-6 w-2/3 animate-pulse rounded-md bg-muted/50" />
        </div>
      );

    case 'error':
      return (
        <div
          data-slot="session-state-error"
          role="alert"
          className="flex flex-col gap-3 rounded-lg border border-destructive/40 bg-destructive/5 p-4"
        >
          <div className="flex items-center gap-2 text-destructive">
            <AlertCircle className="h-5 w-5" aria-hidden="true" />
            <span className="text-sm font-medium">{state.errorTitle}</span>
          </div>
          <p className="text-xs text-muted-foreground">{state.error.message}</p>
          <button
            type="button"
            data-slot="session-state-error-retry"
            onClick={state.onRetry}
            className="inline-flex w-fit items-center gap-1.5 rounded-md border border-border
              bg-card px-3 py-1.5 text-xs font-medium text-foreground
              hover:bg-muted focus-visible:outline-none focus-visible:ring-2
              focus-visible:ring-ring"
          >
            <RefreshCw className="h-3.5 w-3.5" aria-hidden="true" />
            {state.retryLabel}
          </button>
        </div>
      );

    default:
      // Exhaustiveness check — TypeScript will error if a new kind is added without handling.
      return assertNever(state);
  }
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function assertNever(value: never): never {
  throw new Error(
    `SessionStateRenderer: unhandled state kind "${(value as { kind: string }).kind}". ` +
      'Add a case to the switch in SessionStateRenderer.'
  );
}

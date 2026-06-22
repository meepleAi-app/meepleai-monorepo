/**
 * Presentational mock for TranslateViewer wake-lock state.
 *
 * forward-refactor: Wake Lock API (`navigator.wakeLock.request('screen')`) is not
 * implemented in the real TranslateViewer as of DS-17 Phase D-2 (2026-06-22).
 * The mockup state I describes a sticky badge showing wake-lock status
 * (active / disabled / not-supported), which is a planned aspirational feature.
 *
 * This mock replicates the visual design of the badge in all 3 sub-variants
 * (active, disabled, not-supported) as a forward-refactor placeholder.
 * Replace with the real WakeLockBadge primitive when it is implemented.
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-translate-viewer.html
 *   - State I · wake-lock (badge sticky bottom-right, 3 variants)
 *
 * Refs: DS-17 Phase D-2, umbrella #2063, sub-issue #2174.
 */

import type { ReactElement } from 'react';

export type WakeLockStatus = 'active' | 'disabled' | 'not-supported';

export interface TranslateViewerWakeLockMockProps {
  /** Which wake-lock badge sub-variant to display. */
  readonly status?: WakeLockStatus;
}

const BADGE_CONFIG: Record<
  WakeLockStatus,
  { icon: string; label: string; badgeClass: string; desc: string }
> = {
  active: {
    icon: '☀️',
    label: 'Schermo attivo',
    badgeClass: 'bg-emerald-100 text-emerald-900 ring-1 ring-emerald-200',
    desc: 'Wake lock attivo — lo schermo rimane acceso durante la lettura.',
  },
  disabled: {
    icon: '💤',
    label: 'Wake lock disabilitato',
    badgeClass: 'bg-amber-100 text-amber-900 ring-1 ring-amber-200',
    desc: 'Tap per riabilitare il wake lock.',
  },
  'not-supported': {
    icon: '⚠️',
    label: 'Non supportato',
    badgeClass: 'bg-muted text-muted-foreground ring-1 ring-border',
    desc: 'Wake Lock API non disponibile su questo browser (Safari < 16.4).',
  },
};

export function TranslateViewerWakeLockMock({
  status = 'active',
}: TranslateViewerWakeLockMockProps): ReactElement {
  const cfg = BADGE_CONFIG[status];

  return (
    <div
      data-slot="translate-viewer-wake-lock-mock"
      className="relative min-h-[480px] bg-background p-4"
      data-theme="light"
    >
      {/* Minimal viewer shell to give context to the badge */}
      <header className="mb-4 flex items-center justify-between">
        <h1 className="text-xl font-semibold text-[var(--c-game,hsl(30,60%,40%))]">
          Traduci pagina libro game
        </h1>
        <span className="rounded-full bg-muted px-2 py-0.5 font-mono text-xs text-muted-foreground">
          §147 tradotto
        </span>
      </header>

      <div className="rounded-lg border border-[var(--c-game,hsl(30,60%,40%))]/20 bg-background p-4">
        <p className="text-sm leading-relaxed text-foreground">
          Una sentinella si fa avanti, torcia in mano. La sua armatura porta il sigillo del
          Consiglio di Nanolith — tre anelli intrecciati. &quot;Nessuno passa senza il sigillo del
          Consiglio.&quot;
        </p>
      </div>

      {/* Wake-lock badge — sticky bottom-right in real implementation */}
      <div
        role="status"
        aria-label={cfg.desc}
        title={cfg.desc}
        className={`absolute bottom-4 right-4 inline-flex cursor-pointer items-center gap-1.5 rounded-full px-3 py-1 text-xs font-medium ${cfg.badgeClass}`}
        data-testid="wake-lock-badge"
        data-status={status}
      >
        <span aria-hidden="true">{cfg.icon}</span>
        {cfg.label}
      </div>
    </div>
  );
}

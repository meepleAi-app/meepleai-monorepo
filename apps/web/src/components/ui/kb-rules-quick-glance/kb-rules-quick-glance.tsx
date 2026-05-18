import type { JSX } from 'react';

export type KBRulesQuickGlanceStatus = 'ok' | 'loading' | 'error';

export interface KBRule {
  readonly icon?: string;
  readonly text: string;
  readonly src?: string;
}

export interface KBRulesQuickGlanceProps {
  readonly status?: KBRulesQuickGlanceStatus;
  readonly rules?: ReadonlyArray<KBRule>;
  readonly title?: string;
  readonly loadingHint?: string;
  readonly errorTitle?: string;
  readonly errorMessage?: string;
  readonly onRetry?: () => void;
  readonly onProceed?: () => void;
  readonly className?: string;
}

export function KBRulesQuickGlance({
  status = 'ok',
  rules = [],
  title = 'Rules quick-glance',
  loadingHint,
  errorTitle = 'Rules non disponibili offline',
  errorMessage = 'Tu sei offline o la KB non è ancora indicizzata. Procedi senza quick-glance, oppure riprova.',
  onRetry,
  onProceed,
  className,
}: KBRulesQuickGlanceProps): JSX.Element {
  const wrapperClass = ['flex flex-col gap-1.5', className ?? ''].filter(Boolean).join(' ');

  if (status === 'loading') {
    return (
      <div className={wrapperClass} aria-busy="true" aria-live="polite">
        <div className="flex items-center gap-1.5">
          <span aria-hidden="true" className="text-[13px] leading-none">
            📄
          </span>
          <span className="font-mono text-[10px] font-extrabold uppercase tracking-wider text-entity-document">
            {title} · loading…
          </span>
        </div>
        <div className="flex flex-col gap-1.5">
          {[0, 1, 2].map(i => (
            <div key={i} aria-hidden="true" className="h-7 rounded-sm bg-muted animate-pulse" />
          ))}
        </div>
        {loadingHint ? (
          <span className="font-mono text-[9px] font-bold text-muted-foreground">
            {loadingHint}
          </span>
        ) : null}
      </div>
    );
  }

  if (status === 'error') {
    return (
      <div className={wrapperClass}>
        <div className="flex items-center gap-1.5">
          <span aria-hidden="true" className="text-[13px] leading-none">
            📄
          </span>
          <span className="font-mono text-[10px] font-extrabold uppercase tracking-wider text-entity-document">
            {title}
          </span>
        </div>
        <div
          role="alert"
          className={[
            'flex items-start gap-2.5 rounded-md px-3 py-2.5',
            'bg-[hsl(var(--c-danger)/0.06)]',
            'border border-dashed border-[hsl(var(--c-danger)/0.35)]',
          ].join(' ')}
        >
          <span
            aria-hidden="true"
            className="mt-px text-base leading-none text-[hsl(var(--c-danger))]"
          >
            ⚠
          </span>
          <div className="min-w-0 flex-1">
            <div className="font-display text-xs font-extrabold text-foreground">{errorTitle}</div>
            <p className="mt-0.5 text-[11px] leading-snug text-muted-foreground">{errorMessage}</p>
            <div className="mt-1.5 flex gap-1.5">
              <button
                type="button"
                onClick={onRetry}
                className={[
                  'rounded-pill px-2.5 py-0.5 border-0 cursor-pointer',
                  'font-mono text-[9.5px] font-extrabold uppercase tracking-wider',
                  'bg-[hsl(var(--c-danger))] text-white',
                ].join(' ')}
              >
                ↻ Riprova
              </button>
              <button
                type="button"
                onClick={onProceed}
                className={[
                  'rounded-pill px-2.5 py-0.5 border border-border bg-transparent cursor-pointer',
                  'font-mono text-[9.5px] font-bold uppercase tracking-wider',
                  'text-muted-foreground',
                ].join(' ')}
              >
                Procedi senza
              </button>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // status === 'ok'
  return (
    <div className={wrapperClass}>
      <div className="flex items-center gap-1.5">
        <span aria-hidden="true" className="text-[13px] leading-none">
          📄
        </span>
        <span className="font-mono text-[10px] font-extrabold uppercase tracking-wider text-entity-document">
          {title} · {rules.length} bullet
        </span>
      </div>
      <ul className="flex flex-col gap-1.5 m-0 p-0 list-none">
        {rules.map((rule, i) => (
          <li
            key={i}
            className={[
              'flex items-center gap-2 rounded-sm px-2.5 py-1.5',
              'bg-[hsl(var(--c-kb)/0.06)] border border-[hsl(var(--c-kb)/0.22)]',
            ].join(' ')}
          >
            {rule.icon ? (
              <span aria-hidden="true" className="shrink-0 text-[13px] leading-none">
                {rule.icon}
              </span>
            ) : null}
            <span className="min-w-0 flex-1 font-body text-xs font-semibold leading-snug text-foreground">
              {rule.text}
            </span>
            {rule.src ? (
              <span
                className={[
                  'shrink-0 rounded-pill px-1.5 py-px',
                  'font-mono text-[9px] font-extrabold tracking-wider',
                  'bg-[hsl(var(--c-kb)/0.12)] text-entity-document',
                ].join(' ')}
              >
                {rule.src}
              </span>
            ) : null}
          </li>
        ))}
      </ul>
    </div>
  );
}

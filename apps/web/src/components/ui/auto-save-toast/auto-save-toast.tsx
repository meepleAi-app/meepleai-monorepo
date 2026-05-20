import type { JSX } from 'react';

export interface AutoSaveToastProps {
  readonly visible: boolean;
  readonly timestamp: string;
  readonly nextInSeconds?: number;
  readonly label?: string;
  readonly className?: string;
}

export function AutoSaveToast({
  visible,
  timestamp,
  nextInSeconds = 60,
  label = 'Auto-salvato',
  className,
}: AutoSaveToastProps): JSX.Element | null {
  if (!visible) return null;

  const subline = `${timestamp} · prossimo tra ${nextInSeconds}s`;

  return (
    <div
      role="status"
      aria-live="polite"
      aria-atomic="true"
      className={[
        'fixed bottom-[18px] right-[18px] z-40',
        'flex items-center gap-2.5',
        'px-3.5 py-2.5 pl-3',
        'rounded-pill',
        'bg-entity-toolkit/10 border border-entity-toolkit/30',
        'backdrop-blur-md shadow-lg',
        'motion-safe:animate-in motion-safe:slide-in-from-bottom-2 motion-safe:fade-in-0 motion-safe:duration-300',
        className ?? '',
      ]
        .filter(Boolean)
        .join(' ')}
      style={{ boxShadow: 'var(--shadow-lg), 0 0 0 4px hsl(var(--c-toolkit) / 0.06)' }}
    >
      <span
        aria-hidden="true"
        className="inline-flex h-[22px] w-[22px] items-center justify-center rounded-full bg-entity-toolkit text-white text-xs font-extrabold"
      >
        ✓
      </span>
      <div className="flex min-w-0 flex-col gap-px">
        <span className="font-display text-xs font-extrabold text-entity-toolkit-text">
          {label}
        </span>
        <span className="font-mono text-[9px] font-bold uppercase tracking-wider text-muted-foreground">
          {subline}
        </span>
      </div>
    </div>
  );
}

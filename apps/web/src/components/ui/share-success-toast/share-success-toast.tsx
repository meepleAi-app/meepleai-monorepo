import type { JSX } from 'react';

export interface ShareSuccessToastProps {
  readonly visible: boolean;
  readonly title: string;
  readonly subline?: string;
  readonly icon?: string;
  readonly className?: string;
}

export function ShareSuccessToast({
  visible,
  title,
  subline,
  icon = '🔗',
  className,
}: ShareSuccessToastProps): JSX.Element | null {
  if (!visible) return null;

  return (
    <div
      role="status"
      aria-live="polite"
      aria-atomic="true"
      className={[
        'fixed bottom-5 right-5 z-40 max-w-[280px]',
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
        className="inline-flex h-[26px] w-[26px] shrink-0 items-center justify-center rounded-full bg-entity-toolkit text-white text-[13px] font-extrabold"
      >
        {icon}
      </span>
      <div className="flex min-w-0 flex-col gap-px">
        <span className="font-display text-[12.5px] font-extrabold text-entity-toolkit-text">
          {title}
        </span>
        {subline ? (
          <span className="font-mono text-[9px] font-bold uppercase tracking-wider text-muted-foreground">
            {subline}
          </span>
        ) : null}
      </div>
    </div>
  );
}

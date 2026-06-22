import type { JSX, ReactNode } from 'react';

export interface ArchivedBannerProps {
  readonly title: string;
  readonly subtitle?: string;
  readonly icon?: string;
  readonly action?: ReactNode;
  readonly className?: string;
}

export function ArchivedBanner({
  title,
  subtitle,
  icon = '📦',
  action,
  className,
}: ArchivedBannerProps): JSX.Element {
  return (
    <div
      role="status"
      className={[
        'flex flex-wrap items-center gap-3',
        'rounded-lg border border-border bg-muted',
        'px-4 py-3.5',
        className ?? '',
      ]
        .filter(Boolean)
        .join(' ')}
    >
      <span
        aria-hidden="true"
        className={[
          'inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-full',
          'border border-border-strong bg-card',
          'text-base text-muted-foreground',
        ].join(' ')}
      >
        {icon}
      </span>
      <div className="min-w-0 flex-1">
        <span className="font-mono text-[10px] font-extrabold uppercase tracking-wider text-muted-foreground">
          {title}
        </span>
        {subtitle ? (
          <div className="mt-px font-display text-[13px] font-extrabold text-foreground">
            {subtitle}
          </div>
        ) : null}
      </div>
      {action ? <div className="shrink-0">{action}</div> : null}
    </div>
  );
}

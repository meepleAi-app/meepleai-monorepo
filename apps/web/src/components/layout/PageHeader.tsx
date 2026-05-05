'use client';

import Link from 'next/link';

import { cn } from '@/lib/utils';

export interface PageHeaderTab {
  id: string;
  label: string;
  href: string;
  count?: number;
}

export interface PageHeaderAction {
  label: string;
  onClick: () => void;
  icon?: React.ReactNode;
}

export interface PageHeaderProps {
  title: string;
  subtitle?: string;
  parentHref?: string;
  parentLabel?: string;
  primaryAction?: PageHeaderAction;
  tabs?: PageHeaderTab[];
  activeTabId?: string;
  className?: string;
}

export function PageHeader({
  title,
  subtitle,
  parentHref,
  parentLabel,
  primaryAction,
  tabs,
  activeTabId,
  className,
}: PageHeaderProps) {
  const showBackLink = Boolean(parentHref && parentLabel);
  const hasTabs = tabs && tabs.length > 0;

  return (
    <div className={cn('w-full', className)}>
      {/* Back link */}
      {showBackLink && (
        <div className="mb-1">
          <Link
            href={parentHref!}
            className="inline-flex items-center gap-1 text-sm font-medium"
            style={{ color: '#4ecdc4' }}
          >
            <span aria-hidden="true">←</span>
            <span>{parentLabel}</span>
          </Link>
        </div>
      )}

      {/* Title row */}
      <div className="flex items-center justify-between gap-4">
        <div className="min-w-0">
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground truncate">
            {title}
          </h1>
          {subtitle && <p className="mt-0.5 text-sm text-muted-foreground">{subtitle}</p>}
        </div>

        {primaryAction && (
          <button
            type="button"
            onClick={primaryAction.onClick}
            className="inline-flex shrink-0 items-center gap-2 rounded-md px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2"
            style={{ backgroundColor: 'hsl(25, 95%, 38%)' }}
          >
            {primaryAction.icon && <span className="shrink-0">{primaryAction.icon}</span>}
            {primaryAction.label}
          </button>
        )}
      </div>

      {/* Tabs */}
      {hasTabs && (
        <nav className="mt-4 flex gap-0 border-b border-border" aria-label="Page tabs">
          {tabs!.map(tab => {
            const isActive = tab.id === activeTabId;
            return (
              <Link
                key={tab.id}
                href={tab.href}
                aria-current={isActive ? 'page' : undefined}
                className={cn(
                  'inline-flex items-center gap-1.5 border-b-2 px-3 pb-3 pt-1 text-sm font-medium transition-colors',
                  isActive
                    ? 'border-[hsl(25,95%,45%)] text-[hsl(25,95%,45%)]'
                    : 'border-transparent text-muted-foreground hover:border-border hover:text-foreground'
                )}
              >
                {tab.label}
                {tab.count !== undefined && (
                  <span
                    className={cn(
                      'rounded-full px-1.5 py-0.5 text-xs font-semibold tabular-nums',
                      isActive
                        ? 'bg-[hsl(25,95%,45%)] text-white'
                        : 'bg-muted text-muted-foreground'
                    )}
                  >
                    {tab.count}
                  </span>
                )}
              </Link>
            );
          })}
        </nav>
      )}
    </div>
  );
}

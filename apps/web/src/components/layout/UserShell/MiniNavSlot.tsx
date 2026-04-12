'use client';

import Link from 'next/link';

import { useMiniNavConfigStore } from '@/lib/stores/mini-nav-config-store';
import { cn } from '@/lib/utils';

import { RecentsBar } from './RecentsBar';

/**
 * MiniNavSlot — renders the mini-nav registered by the current page.
 * Hidden when no config is set.
 */
export function MiniNavSlot() {
  const config = useMiniNavConfigStore(s => s.config);
  if (!config) return null;

  return (
    <div
      data-testid="mini-nav-slot"
      className="h-12 flex items-center gap-1 px-7 pl-[104px] border-b border-[var(--nh-border-default)] bg-[var(--nh-bg-base)]"
    >
      <div className="text-xs font-semibold text-[var(--nh-text-muted)] mr-5">
        <span aria-hidden>›</span> {config.breadcrumb}
      </div>
      {config.tabs.map(tab => {
        const active = tab.id === config.activeTabId;
        return (
          <Link
            key={tab.id}
            href={tab.href}
            aria-current={active ? 'page' : undefined}
            className={cn(
              'relative px-3.5 py-2 rounded-lg text-[0.78rem] font-bold flex items-center gap-1.5 transition-colors',
              active
                ? 'text-[var(--nh-text-primary)]'
                : 'text-[var(--nh-text-secondary)] hover:bg-[var(--nh-bg-surface)]'
            )}
          >
            {tab.label}
            {tab.count !== undefined && (
              <span className="px-1.5 py-0.5 rounded text-[10px] font-extrabold bg-[rgba(160,120,60,0.1)] text-[var(--nh-text-secondary)]">
                {tab.count}
              </span>
            )}
            {active && (
              <span
                aria-hidden
                className="absolute bottom-[-12px] left-3.5 right-3.5 h-0.5 rounded-t"
                style={{ background: 'hsl(25 95% 45%)' }}
              />
            )}
          </Link>
        );
      })}
      <div className="flex-1" />
      <RecentsBar />
      {config.primaryAction && (
        <button
          type="button"
          onClick={config.primaryAction.onClick}
          className="px-3.5 py-2 rounded-[10px] text-[0.78rem] font-bold text-white border-none cursor-pointer flex items-center gap-1.5 transition-all hover:-translate-y-px"
          style={{
            background: 'linear-gradient(135deg, hsl(25 95% 48%), hsl(25 95% 40%))',
            boxShadow: '0 2px 6px hsla(25, 95%, 45%, 0.3)',
          }}
        >
          {config.primaryAction.icon && <span aria-hidden>{config.primaryAction.icon}</span>}
          {config.primaryAction.label}
        </button>
      )}
    </div>
  );
}

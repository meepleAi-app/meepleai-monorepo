'use client';

import { useEffect, useRef, useState, type ReactNode } from 'react';

import Link from 'next/link';

import { cn } from '@/lib/utils';

export interface HubTab {
  id: string;
  label: string;
  href: string;
  icon?: ReactNode;
}

interface AdminHubTabBarProps {
  tabs: readonly HubTab[];
  activeTab: string;
}

/**
 * Shared horizontal tab bar for admin hub pages.
 * Mobile-first: horizontally scrollable with fade edges.
 * Desktop: wraps naturally with comfortable spacing.
 */
export function AdminHubTabBar({ tabs, activeTab }: AdminHubTabBarProps) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const [showLeftFade, setShowLeftFade] = useState(false);
  const [showRightFade, setShowRightFade] = useState(false);

  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;

    function updateFades() {
      if (!el) return;
      setShowLeftFade(el.scrollLeft > 4);
      setShowRightFade(el.scrollLeft < el.scrollWidth - el.clientWidth - 4);
    }

    updateFades();
    el.addEventListener('scroll', updateFades, { passive: true });
    const ro = new ResizeObserver(updateFades);
    ro.observe(el);

    return () => {
      el.removeEventListener('scroll', updateFades);
      ro.disconnect();
    };
  }, []);

  // Scroll active tab into view on mount
  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    const activeEl = el.querySelector('[data-active="true"]') as HTMLElement | null;
    if (activeEl) {
      activeEl.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
    }
  }, [activeTab]);

  return (
    <div className="relative">
      {/* Left fade */}
      <div
        className={cn(
          'pointer-events-none absolute left-0 top-0 bottom-0 z-10 w-8',
          'bg-gradient-to-r from-slate-50 dark:from-zinc-950 to-transparent',
          'transition-opacity duration-200',
          showLeftFade ? 'opacity-100' : 'opacity-0'
        )}
      />

      {/* Scrollable tab area */}
      <div
        ref={scrollRef}
        className="flex gap-1 overflow-x-auto scrollbar-none pb-px -mb-px md:flex-wrap md:overflow-x-visible"
        role="tablist"
      >
        {tabs.map(t => (
          <Link
            key={t.id}
            href={t.href}
            role="tab"
            aria-selected={activeTab === t.id}
            data-active={activeTab === t.id}
            className={cn(
              'relative flex items-center gap-1.5 whitespace-nowrap rounded-lg px-3 py-2 text-sm font-medium',
              'transition-all duration-200 shrink-0',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50 focus-visible:ring-offset-1',
              activeTab === t.id
                ? [
                    'bg-white dark:bg-zinc-800 text-foreground',
                    'shadow-sm shadow-black/5 dark:shadow-black/20',
                    'border border-slate-200/80 dark:border-zinc-700/60',
                  ]
                : [
                    'text-muted-foreground',
                    'hover:text-foreground hover:bg-white/50 dark:hover:bg-zinc-800/50',
                    'border border-transparent',
                  ]
            )}
          >
            {t.icon && (
              <span
                className={cn(
                  'shrink-0 [&>svg]:h-3.5 [&>svg]:w-3.5',
                  activeTab === t.id ? 'text-primary' : 'text-muted-foreground/70'
                )}
              >
                {t.icon}
              </span>
            )}
            {t.label}
          </Link>
        ))}
      </div>

      {/* Right fade */}
      <div
        className={cn(
          'pointer-events-none absolute right-0 top-0 bottom-0 z-10 w-8',
          'bg-gradient-to-l from-slate-50 dark:from-zinc-950 to-transparent',
          'transition-opacity duration-200',
          showRightFade ? 'opacity-100' : 'opacity-0'
        )}
      />

      {/* Bottom border */}
      <div className="border-b border-border/40 -mt-px" />
    </div>
  );
}

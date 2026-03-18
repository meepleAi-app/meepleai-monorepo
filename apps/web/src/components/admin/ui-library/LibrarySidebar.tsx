'use client';

import type { ComponentCategory } from '@/components/showcase/types';
import type { AppArea } from '@/config/component-registry';
import { cn } from '@/lib/utils';

import type { LibraryFilters } from './SearchFilter';

interface SidebarSection<T extends string> {
  label: string;
  items: { value: T; count: number }[];
  active: T | undefined;
  onSelect: (value: T | undefined) => void;
}

function SidebarGroup<T extends string>({ label, items, active, onSelect }: SidebarSection<T>) {
  return (
    <div>
      <p className="mb-1.5 px-2 font-quicksand text-xs font-semibold uppercase tracking-wider text-muted-foreground">
        {label}
      </p>
      <ul className="space-y-0.5">
        {items.map(({ value, count }) => {
          const isActive = active === value;
          return (
            <li key={value}>
              <button
                type="button"
                onClick={() => onSelect(isActive ? undefined : value)}
                className={cn(
                  'flex w-full items-center justify-between rounded-md px-2 py-1.5 font-nunito text-sm transition-colors',
                  isActive
                    ? 'bg-amber-100 text-amber-900 dark:bg-amber-950/40 dark:text-amber-300 font-medium'
                    : 'text-muted-foreground hover:bg-muted hover:text-foreground'
                )}
                aria-pressed={isActive}
              >
                <span className="truncate">{value}</span>
                <span
                  className={cn(
                    'ml-2 shrink-0 rounded-full px-1.5 py-0.5 text-[10px] font-medium',
                    isActive
                      ? 'bg-amber-200 text-amber-900 dark:bg-amber-800/50 dark:text-amber-200'
                      : 'bg-muted text-muted-foreground'
                  )}
                >
                  {count}
                </span>
              </button>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

interface LibrarySidebarProps {
  categories: { category: ComponentCategory; count: number }[];
  areas: { area: AppArea; count: number }[];
  filters: LibraryFilters;
  onChange: (filters: LibraryFilters) => void;
}

export function LibrarySidebar({ categories, areas, filters, onChange }: LibrarySidebarProps) {
  return (
    <aside className="w-60 shrink-0 space-y-6 py-2" aria-label="Component library filters">
      <SidebarGroup<ComponentCategory>
        label="Category"
        items={categories.map(({ category, count }) => ({ value: category, count }))}
        active={filters.category}
        onSelect={value => onChange({ ...filters, category: value })}
      />

      <SidebarGroup<AppArea>
        label="Area"
        items={areas.map(({ area, count }) => ({ value: area, count }))}
        active={filters.area}
        onSelect={value => onChange({ ...filters, area: value })}
      />

      <div>
        <p className="mb-1.5 px-2 font-quicksand text-xs font-semibold uppercase tracking-wider text-muted-foreground">
          Tier
        </p>
        <ul className="space-y-0.5">
          {(['interactive', 'static'] as const).map(tier => {
            const isActive = filters.tier === tier;
            return (
              <li key={tier}>
                <button
                  type="button"
                  onClick={() => onChange({ ...filters, tier: isActive ? undefined : tier })}
                  className={cn(
                    'flex w-full items-center rounded-md px-2 py-1.5 font-nunito text-sm capitalize transition-colors',
                    isActive
                      ? 'bg-amber-100 text-amber-900 dark:bg-amber-950/40 dark:text-amber-300 font-medium'
                      : 'text-muted-foreground hover:bg-muted hover:text-foreground'
                  )}
                  aria-pressed={isActive}
                >
                  {tier}
                </button>
              </li>
            );
          })}
        </ul>
      </div>
    </aside>
  );
}

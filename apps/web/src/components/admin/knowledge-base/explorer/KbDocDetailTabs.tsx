'use client';

import Link from 'next/link';

export type KbDocTabKey = 'overview' | 'ingestion' | 'used-by';

interface KbDocDetailTabsProps {
  readonly docId: string;
  readonly activeTab: KbDocTabKey;
}

const TABS: ReadonlyArray<{ readonly key: KbDocTabKey; readonly label: string }> = [
  { key: 'overview', label: 'Overview' },
  { key: 'ingestion', label: 'Ingestion log' },
  { key: 'used-by', label: 'Used by' },
];

/**
 * Inner tab nav for `KbDocDetailPanel`. URL-driven via `?tab=overview` (default)
 * | `?tab=ingestion` | `?tab=used-by`. Preserves `docId` in each link.
 * Issues #1650 (Ingestion log) + #1651 (Used by). Future follow-ups #1653/#1654.
 */
export function KbDocDetailTabs({ docId, activeTab }: KbDocDetailTabsProps) {
  return (
    <nav
      aria-label="Sezione documento"
      className="border-b border-border/60 -mx-4 px-4 mb-4 overflow-x-auto"
    >
      <ul className="flex gap-1 min-w-max m-0 p-0 list-none">
        {TABS.map(tab => {
          const isActive = activeTab === tab.key;
          const params = new URLSearchParams({ docId });
          if (tab.key !== 'overview') params.set('tab', tab.key);
          const href = `/admin/knowledge-base?${params.toString()}`;
          return (
            <li key={tab.key}>
              <Link
                href={href}
                aria-current={isActive ? 'page' : undefined}
                className={[
                  'inline-flex items-center px-3 py-2 text-sm font-medium border-b-2 -mb-px transition-colors',
                  isActive
                    ? 'border-amber-500 text-foreground'
                    : 'border-transparent text-muted-foreground hover:text-foreground hover:border-border',
                ].join(' ')}
              >
                {tab.label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}

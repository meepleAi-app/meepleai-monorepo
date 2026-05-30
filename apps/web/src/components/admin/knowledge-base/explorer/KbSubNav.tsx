/* eslint-disable local/no-hardcoded-color-utility -- admin KB explorer: amber accent + zinc dark palette (admin convention, DS-13c scope deferred to DS-16) */
'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { useKbNavCounts } from '@/hooks/admin/useKbNavCounts';

import { KbCountBadge } from './KbCountBadge';

const KB_BASE = '/admin/knowledge-base';

type KbTabKind = 'queue' | 'feedback';

interface KbTab {
  readonly label: string;
  readonly href: string;
  readonly kind?: KbTabKind;
}

const TABS: ReadonlyArray<KbTab> = [
  { label: 'Explorer', href: KB_BASE },
  { label: 'Vector Collections', href: `${KB_BASE}/vectors` },
  { label: 'Processing Queue', href: `${KB_BASE}/queue`, kind: 'queue' },
  { label: 'RAG Pipeline', href: `${KB_BASE}/pipeline` },
  { label: 'Embedding', href: `${KB_BASE}/embedding` },
  { label: 'Feedback', href: `${KB_BASE}/feedback`, kind: 'feedback' },
  { label: 'Settings', href: `${KB_BASE}/settings` },
  { label: 'Snapshots', href: `${KB_BASE}/snapshots` },
];

const TOOLTIPS: Record<KbTabKind, string> = {
  queue: 'Job attivi (queued, in elaborazione o falliti)',
  feedback: 'Feedback ricevuti negli ultimi 7 giorni',
};

function isActive(tabHref: string, pathname: string): boolean {
  // Explorer is active ONLY on exact /admin/knowledge-base (otherwise it
  // would match all sub-routes via startsWith).
  if (tabHref === KB_BASE) return pathname === KB_BASE;
  return pathname === tabHref || pathname.startsWith(`${tabHref}/`);
}

/**
 * KB sub-nav: 8 tab-link a route reali. La sezione attiva è derivata dal
 * pathname corrente (App Router). Vive dentro `knowledge-base/layout.tsx` e
 * wrappa Explorer + le 7 tool-page esistenti.
 *
 * Issue #1655: wired count badges on Processing Queue + Feedback tabs.
 */
export function KbSubNav() {
  const pathname = usePathname();
  const { queue, feedback, loading, isError } = useKbNavCounts();

  return (
    <nav
      aria-label="Knowledge Base sezioni"
      className="border-b border-border/60 dark:border-zinc-700/60 -mx-6 px-6 mb-6 overflow-x-auto"
    >
      <ul className="flex gap-1 min-w-max">
        {TABS.map(tab => {
          const active = isActive(tab.href, pathname);
          const count =
            tab.kind === 'queue' ? queue : tab.kind === 'feedback' ? feedback : undefined;
          return (
            <li key={tab.href}>
              <Link
                href={tab.href}
                aria-current={active ? 'page' : undefined}
                className={[
                  'inline-flex items-center px-3 py-2 text-sm font-medium border-b-2 -mb-px transition-colors',
                  active
                    ? 'border-amber-500 text-foreground'
                    : 'border-transparent text-muted-foreground hover:text-foreground hover:border-border',
                ].join(' ')}
              >
                {tab.label}
                {tab.kind && (
                  <KbCountBadge
                    count={count}
                    loading={loading}
                    isError={isError}
                    tooltip={TOOLTIPS[tab.kind]}
                    testId={`kb-nav-badge-${tab.kind}`}
                  />
                )}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}

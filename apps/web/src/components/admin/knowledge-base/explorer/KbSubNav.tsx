/* eslint-disable local/no-hardcoded-color-utility -- admin KB explorer: amber accent + zinc dark palette (admin convention, DS-13c scope deferred to DS-16) */
'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

const KB_BASE = '/admin/knowledge-base';

interface KbTab {
  readonly label: string;
  readonly href: string;
}

const TABS: ReadonlyArray<KbTab> = [
  { label: 'Explorer', href: KB_BASE },
  { label: 'Vector Collections', href: `${KB_BASE}/vectors` },
  { label: 'Processing Queue', href: `${KB_BASE}/queue` },
  { label: 'RAG Pipeline', href: `${KB_BASE}/pipeline` },
  { label: 'Embedding', href: `${KB_BASE}/embedding` },
  { label: 'Feedback', href: `${KB_BASE}/feedback` },
  { label: 'Settings', href: `${KB_BASE}/settings` },
  { label: 'Snapshots', href: `${KB_BASE}/snapshots` },
];

function isActive(tabHref: string, pathname: string): boolean {
  // Explorer is active ONLY on exact /admin/knowledge-base (otherwise it
  // would match all sub-routes via startsWith).
  if (tabHref === KB_BASE) return pathname === KB_BASE;
  return pathname === tabHref || pathname.startsWith(`${tabHref}/`);
}

/**
 * KB sub-nav: 8 tab-link a route reali. La sezione attiva è derivata dal
 * pathname corrente (App Router). Vive dentro `knowledge-base/layout.tsx` e
 * wrappa Explorer + le 7 tool-page esistenti (vectors, queue, pipeline,
 * embedding, feedback, settings, snapshots).
 *
 * Pattern visivo SP5: `.admin-tabs` orizzontale; bordo bottom token-tinted
 * sull'attivo. Niente badge count in F3.1 (deferred a follow-up).
 */
export function KbSubNav() {
  const pathname = usePathname();

  return (
    <nav
      aria-label="Knowledge Base sezioni"
      className="border-b border-border/60 dark:border-zinc-700/60 -mx-6 px-6 mb-6 overflow-x-auto"
    >
      <ul className="flex gap-1 min-w-max">
        {TABS.map(tab => {
          const active = isActive(tab.href, pathname);
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
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}

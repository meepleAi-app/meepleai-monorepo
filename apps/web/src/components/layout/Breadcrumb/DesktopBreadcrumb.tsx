/**
 * DesktopBreadcrumb Component
 * Navigability fix — docs/frontend/navigability-analysis.md R2
 *
 * Path-based breadcrumb trail for desktop (md+).
 * Renders an inline "Dashboard > Sezione > Pagina" path from the current URL.
 * Complementary to the mobile floating-pill Breadcrumb.
 */

'use client';

import { ChevronRight, Home } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';

// ─── Segment label map ────────────────────────────────────────────────────────

const SEGMENT_LABELS: Record<string, string> = {
  dashboard: 'Dashboard',
  library: 'Libreria',
  favorites: 'Preferiti',
  wishlist: 'Wishlist',
  archived: 'Archiviati',
  private: 'Giochi privati',
  proposals: 'Proposte',
  games: 'Catalogo',
  chat: 'Chat',
  'play-records': 'Sessioni recenti',
  players: 'Giocatori',
  profile: 'Profilo',
  settings: 'Impostazioni',
  agents: 'Agenti',
  sessions: 'Sessioni',
  'knowledge-base': 'Knowledge Base',
  admin: 'Admin',
  overview: 'Panoramica',
  analytics: 'Analisi',
  'ai-usage': 'Uso AI',
  'api-keys': 'Chiavi API',
  'audit-log': 'Registro audit',
  'feature-flags': 'Feature flags',
  'agent-definitions': 'Definizioni agenti',
  'agent-typologies': 'Tipologie agenti',
  'ai-lab': 'AI Lab',
  alerts: 'Avvisi',
  cache: 'Cache',
  configuration: 'Configurazione',
  faqs: 'FAQ',
  editor: 'Editor',
};

// ─── Types ────────────────────────────────────────────────────────────────────

interface BreadcrumbItem {
  label: string;
  href: string | null;
  isCurrent: boolean;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

/** Detects whether a URL segment is likely a dynamic ID (UUID or numeric). */
function isLikelyId(segment: string): boolean {
  // UUID (standard format used throughout this codebase)
  if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(segment)) return true;
  // Numeric ID
  if (/^\d+$/.test(segment)) return true;
  return false;
}

/** Converts an unrecognised segment to a human-readable label. */
function formatFallbackLabel(segment: string): string {
  return segment
    .split('-')
    .map(word => word.charAt(0).toUpperCase() + word.slice(1))
    .join(' ');
}

/**
 * Builds a breadcrumb trail from the current pathname.
 * Always prepends Dashboard as the root for authenticated pages.
 */
function buildBreadcrumbs(pathname: string): BreadcrumbItem[] {
  // Root — dashboard itself
  if (pathname === '/dashboard') {
    return [{ label: 'Dashboard', href: '/dashboard', isCurrent: true }];
  }

  const crumbs: BreadcrumbItem[] = [
    { label: 'Dashboard', href: '/dashboard', isCurrent: false },
  ];

  const segments = pathname.split('/').filter(Boolean);
  let currentPath = '';

  for (let i = 0; i < segments.length; i++) {
    const segment = segments[i];
    currentPath += `/${segment}`;
    const isLast = i === segments.length - 1;

    if (isLikelyId(segment)) {
      // Preserve href for intermediate ID segments so mid-path navigation works
      crumbs.push({ label: 'Dettaglio', href: isLast ? null : currentPath, isCurrent: isLast });
    } else {
      // eslint-disable-next-line security/detect-object-injection -- segment comes from URL, not user input for object injection
      const label = SEGMENT_LABELS[segment] ?? formatFallbackLabel(segment);
      crumbs.push({
        label,
        href: isLast ? null : currentPath,
        isCurrent: isLast,
      });
    }
  }

  return crumbs;
}

// ─── Component ────────────────────────────────────────────────────────────────

export interface DesktopBreadcrumbProps {
  className?: string;
}

/**
 * DesktopBreadcrumb
 *
 * Renders a horizontal breadcrumb trail for desktop screens.
 * Returns null on dashboard and root pages so any border/spacing passed
 * via className is not rendered when there is no content.
 *
 * Usage in layout:
 *   <DesktopBreadcrumb className="hidden md:flex py-3 border-b border-border/40 mb-2" />
 */
export function DesktopBreadcrumb({ className }: DesktopBreadcrumbProps) {
  const pathname = usePathname();

  if (!pathname || pathname === '/') return null;

  const crumbs = buildBreadcrumbs(pathname);

  // Don't render a single-item breadcrumb (i.e. when already on dashboard)
  if (crumbs.length <= 1) return null;

  return (
    <nav
      aria-label="Percorso di navigazione"
      className={cn(
        'flex items-center gap-1 text-sm text-muted-foreground',
        className
      )}
    >
      {crumbs.map((crumb, index) => (
        <span key={`${crumb.href ?? crumb.label}-${index}`} className="flex items-center gap-1">
          {index > 0 && (
            <ChevronRight className="h-3.5 w-3.5 shrink-0 opacity-40" aria-hidden="true" />
          )}

          {crumb.isCurrent || !crumb.href ? (
            <span
              className={cn(
                'font-medium',
                crumb.isCurrent ? 'text-foreground' : 'text-muted-foreground'
              )}
              aria-current={crumb.isCurrent ? 'page' : undefined}
            >
              {crumb.label}
            </span>
          ) : (
            <Link
              href={crumb.href}
              className="flex items-center gap-1 hover:text-foreground transition-colors duration-150"
            >
              {index === 0 && (
                <Home className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
              )}
              {crumb.label}
            </Link>
          )}
        </span>
      ))}
    </nav>
  );
}

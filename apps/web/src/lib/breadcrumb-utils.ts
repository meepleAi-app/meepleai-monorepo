/**
 * Breadcrumb utility functions
 *
 * Shared helpers for building breadcrumb trails from URL pathnames.
 * Extracted from DesktopBreadcrumb so TopBar and other layouts can reuse them.
 */

// ─── Segment label map ────────────────────────────────────────────────────────

export const SEGMENT_LABELS: Record<string, string> = {
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
  'game-nights': 'Serate di Gioco',
  new: 'Nuovo',
  edit: 'Modifica',
  toolkit: 'Toolkit',
  badges: 'Badge',
};

// ─── Types ────────────────────────────────────────────────────────────────────

export interface BreadcrumbItem {
  label: string;
  href: string | null;
  isCurrent: boolean;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

/** Detects whether a URL segment is likely a dynamic ID (UUID or numeric). */
export function isLikelyId(segment: string): boolean {
  // UUID (standard format used throughout this codebase)
  if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(segment)) return true;
  // Numeric ID
  if (/^\d+$/.test(segment)) return true;
  return false;
}

/** Converts an unrecognised segment to a human-readable label. */
export function formatFallbackLabel(segment: string): string {
  return segment
    .split('-')
    .map(word => word.charAt(0).toUpperCase() + word.slice(1))
    .join(' ');
}

/**
 * Builds a breadcrumb trail from the current pathname.
 * Always prepends Dashboard as the root for authenticated pages.
 */
export function buildBreadcrumbs(pathname: string): BreadcrumbItem[] {
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

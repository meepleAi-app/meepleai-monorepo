export interface CTAConfig {
  label: string;
  href: string;
}

/**
 * Resolves the contextual CTA for the given pathname.
 * Shared between ActionPill (desktop) and ActionBar (mobile).
 */
export function resolveCTA(pathname: string): CTAConfig | null {
  if (pathname === '/dashboard') {
    return { label: '+ Aggiungi gioco', href: '/library?action=add' };
  }
  if (pathname === '/library' || pathname.startsWith('/library/')) {
    return { label: '+ Aggiungi', href: '/library?action=add' };
  }
  if (/^\/games\/[^/]+$/.test(pathname)) {
    return { label: '▶ Nuova sessione', href: `${pathname}/sessions/new` };
  }
  if (/^\/games\/[^/]+\/sessions$/.test(pathname)) {
    return { label: '▶ Nuova sessione', href: `${pathname}/new` };
  }
  if (/^\/games\/[^/]+\/kb$/.test(pathname)) {
    return { label: '↑ Carica PDF', href: `${pathname}/upload` };
  }
  if (/^\/agents\/[^/]+$/.test(pathname)) {
    return { label: '💬 Inizia chat', href: `${pathname}/chat` };
  }
  if (pathname === '/sessions' || pathname.startsWith('/sessions/')) {
    return { label: '▶ Nuova sessione', href: '/sessions/new' };
  }
  return null;
}

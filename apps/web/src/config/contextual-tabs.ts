export interface ContextualTab {
  label: string;
  href: string;
}

/**
 * Route prefix → contextual tabs shown in navbar row 2.
 * Matching: pathname.startsWith(key).
 * Active tab: exact match of href against pathname + search params.
 */
export const CONTEXTUAL_TABS: Record<string, ContextualTab[]> = {
  '/library': [
    { label: 'Collezione', href: '/library?tab=collection' },
    { label: 'Wishlist', href: '/library?tab=wishlist' },
    { label: 'Documenti', href: '/library?tab=private' },
    { label: 'Cronologia', href: '/library?tab=history' },
  ],
  '/sessions': [
    { label: 'In corso', href: '/sessions?tab=active' },
    { label: 'Completate', href: '/sessions?tab=completed' },
    { label: 'Pianificate', href: '/sessions?tab=planned' },
  ],
  '/chat': [
    { label: 'Thread', href: '/chat' },
    { label: 'Agenti', href: '/agents' },
  ],
  '/agents': [
    { label: 'Thread', href: '/chat' },
    { label: 'Agenti', href: '/agents' },
  ],
  '/dashboard': [
    { label: 'Overview', href: '/dashboard' },
    { label: 'Attivita recente', href: '/dashboard?tab=activity' },
  ],
  '/settings': [
    { label: 'Profilo', href: '/settings?tab=profile' },
    { label: 'Preferenze', href: '/settings?tab=preferences' },
    { label: 'Account', href: '/settings?tab=account' },
  ],
};

/** Find tabs for the current pathname. Returns undefined if none match. */
export function getTabsForPathname(pathname: string): ContextualTab[] | undefined {
  for (const [prefix, tabs] of Object.entries(CONTEXTUAL_TABS)) {
    if (pathname.startsWith(prefix)) return tabs;
  }
  return undefined;
}

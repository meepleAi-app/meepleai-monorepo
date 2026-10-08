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
    { label: 'Libreria', href: '/library' },
    { label: 'Wishlist', href: '/library/wishlist' },
  ],
  '/sessions': [
    { label: 'In corso', href: '/sessions?tab=active' },
    { label: 'Completate', href: '/sessions?tab=completed' },
    { label: 'Pianificate', href: '/sessions?tab=planned' },
  ],
  // Issue #4138: the Thread/Agenti pair is gone with the /agents section. A
  // single remaining tab is not a tab bar, so /chat has no contextual tabs.
  '/dashboard': [
    { label: 'Overview', href: '/dashboard' },
    { label: 'Attivita recente', href: '/dashboard?tab=activity' },
  ],
  // #3938: these were inert — nothing navigated to `/settings`, so
  // `getTabsForPathname` was never called with it. They also used `?tab=` and
  // named sections (`account`) that do not exist. The hub is addressed by
  // sub-route (ADR-091) and its ids come from `SETTINGS_SECTIONS`.
  // The criterion for listing a section is `placeholder: true` in
  // `settings-sections.ts`, which now holds for `services` alone: `notifications`
  // renders the real `NotificationPreferences` panel since #3961, and it is where
  // the footer of every email points, so leaving it out hid the one destination
  // users arrive at from outside the app.
  '/settings': [
    { label: 'Profilo', href: '/settings/profile' },
    { label: 'Sicurezza', href: '/settings/security' },
    { label: 'AI e dati', href: '/settings/ai-consent' },
    { label: 'Notifiche', href: '/settings/notifications' },
    { label: 'Preferenze', href: '/settings/preferences' },
    { label: 'Chiavi API', href: '/settings/api-keys' },
  ],
};

/** Find tabs for the current pathname. Returns undefined if none match. */
export function getTabsForPathname(pathname: string): ContextualTab[] | undefined {
  for (const [prefix, tabs] of Object.entries(CONTEXTUAL_TABS)) {
    if (pathname.startsWith(prefix)) return tabs;
  }
  return undefined;
}

export interface CtaConfig {
  /** Button label shown in TopNav (desktop) and FAB tooltip (mobile) */
  label: string;
  /** Route to navigate to on click */
  href: string;
  /**
   * Full Tailwind gradient classes — must be complete strings so Tailwind
   * includes them during build (e.g. 'from-orange-600 to-amber-500').
   */
  gradient: string;
}

/**
 * Ordered list of pathname prefix → CTA mappings.
 * First match wins. More specific prefixes should come before broader ones.
 */
const CTA_MAP: Array<{ prefix: string; config: CtaConfig }> = [
  {
    prefix: '/library',
    config: {
      label: '+ Aggiungi gioco',
      href: '/catalog',
      gradient: 'from-orange-600 to-amber-500',
    },
  },
  {
    prefix: '/sessions',
    config: {
      label: '+ Nuova sessione',
      href: '/sessions/new',
      gradient: 'from-emerald-600 to-teal-500',
    },
  },
  {
    prefix: '/chat',
    config: {
      label: '+ Nuova chat',
      href: '/chat',
      gradient: 'from-violet-600 to-purple-500',
    },
  },
  {
    prefix: '/game-nights',
    config: {
      label: '+ Organizza serata',
      href: '/game-nights/new',
      gradient: 'from-sky-600 to-blue-500',
    },
  },
  {
    prefix: '/agents',
    config: {
      label: '+ Nuovo agente',
      href: '/agents/new',
      gradient: 'from-pink-600 to-rose-500',
    },
  },
];

/**
 * Returns the contextual CTA config for the given pathname,
 * or null if no primary action is defined for this section.
 */
export function getCtaForPathname(pathname: string): CtaConfig | null {
  const entry = CTA_MAP.find(({ prefix }) => pathname.startsWith(prefix));
  return entry?.config ?? null;
}

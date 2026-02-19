/**
 * SVG icon components for entity navigation.
 * Each icon is a small, inline SVG matching the MeepleCard design system.
 *
 * @see Issue #4689 - CardNavigationFooter
 */

import type { MeepleEntityType } from '../meeple-card';

interface IconProps {
  className?: string;
}

function GameIcon({ className }: IconProps) {
  return (
    <svg className={className} viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
      <rect x="2" y="4" width="12" height="9" rx="1.5" />
      <circle cx="5.5" cy="8.5" r="1" />
      <circle cx="10.5" cy="8.5" r="1" />
      <path d="M6 4V2.5A2 2 0 0 1 10 2.5V4" />
    </svg>
  );
}

function AgentIcon({ className }: IconProps) {
  return (
    <svg className={className} viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="3" width="10" height="8" rx="2" />
      <circle cx="6" cy="7" r="0.8" fill="currentColor" stroke="none" />
      <circle cx="10" cy="7" r="0.8" fill="currentColor" stroke="none" />
      <path d="M5 13h6" />
      <path d="M8 11v2" />
    </svg>
  );
}

function DocumentIcon({ className }: IconProps) {
  return (
    <svg className={className} viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
      <path d="M4 2h5.5L13 5.5V13a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1Z" />
      <path d="M9 2v4h4" />
      <path d="M5.5 8.5h5" />
      <path d="M5.5 11h3" />
    </svg>
  );
}

function SessionIcon({ className }: IconProps) {
  return (
    <svg className={className} viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
      <rect x="2" y="3" width="12" height="10" rx="2" />
      <path d="M6.5 7l2 1.5-2 1.5V7Z" fill="currentColor" stroke="none" />
      <path d="M2 6h12" />
    </svg>
  );
}

function PlayerIcon({ className }: IconProps) {
  return (
    <svg className={className} viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="8" cy="5" r="2.5" />
      <path d="M3 14c0-2.8 2.2-5 5-5s5 2.2 5 5" />
    </svg>
  );
}

function ChatIcon({ className }: IconProps) {
  return (
    <svg className={className} viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
      <path d="M3 3h10a1 1 0 0 1 1 1v6a1 1 0 0 1-1 1H6l-3 2.5V4a1 1 0 0 1 1-1Z" />
      <path d="M5.5 6.5h5" />
      <path d="M5.5 9h3" />
    </svg>
  );
}

/**
 * Map entity types to their navigation icon components.
 */
export const ENTITY_NAV_ICONS: Record<MeepleEntityType, React.ComponentType<IconProps>> = {
  game: GameIcon,
  agent: AgentIcon,
  document: DocumentIcon,
  session: SessionIcon,
  player: PlayerIcon,
  chatSession: ChatIcon,
  event: GameIcon,   // fallback
  custom: GameIcon,  // fallback
};

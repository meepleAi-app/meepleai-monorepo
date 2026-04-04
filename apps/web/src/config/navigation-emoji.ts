export const SECTION_EMOJI: Record<string, string> = {
  '/dashboard': '🏠',
  '/library': '📚',
  '/sessions': '🎲',
  '/chat': '✨',
  '/agents': '🤖',
  '/notifications': '🔔',
  '/settings': '⚙️',
};

/** Given a pathname, return the matching section emoji, or null if no match */
export function getSectionEmoji(pathname: string): string | null {
  for (const [prefix, emoji] of Object.entries(SECTION_EMOJI)) {
    if (pathname.startsWith(prefix)) return emoji;
  }
  return null;
}

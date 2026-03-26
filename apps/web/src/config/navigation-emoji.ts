export const SECTION_EMOJI: Record<string, string> = {
  '/dashboard': '🏠',
  '/library': '📚',
  '/sessions': '🎲',
  '/chat': '✨',
  '/agents': '🤖',
  '/settings': '⚙️',
};

/** Given a pathname, return the matching section emoji */
export function getSectionEmoji(pathname: string): string {
  for (const [prefix, emoji] of Object.entries(SECTION_EMOJI)) {
    if (pathname.startsWith(prefix)) return emoji;
  }
  return '🏠';
}

/**
 * personInitials — #2634 C4.
 *
 * Avatar initials for a PERSON's display name (not a game title — `extractInitials` is
 * game-title-tuned with stop-words and would mangle a player name). Code-point safe: it never
 * uses `charAt(0)`, which splits a surrogate pair (emoji) into a broken half (panel must-fix #7);
 * a guest DisplayName (max 50 chars) may legitimately contain emoji.
 *
 * - Two+ words → first code point of the first and last words ("Mario Rossi" → "MR").
 * - One word → up to the first two code points ("Alice" → "AL", "🎲" → "🎲").
 * - Empty / whitespace-only → a stable "?" fallback.
 */
export function personInitials(name: string): string {
  const trimmed = name.trim();
  if (trimmed.length === 0) return '?';

  const words = trimmed.split(/\s+/).filter(Boolean);
  const firstCodePoint = (word: string): string => {
    const cp = [...word][0]; // spread iterates by code point, not UTF-16 unit
    return cp ? cp.toUpperCase() : '';
  };

  if (words.length === 1) {
    const initials = [...words[0]].slice(0, 2).join('').toUpperCase();
    return initials.length > 0 ? initials : '?';
  }

  const initials = firstCodePoint(words[0]) + firstCodePoint(words[words.length - 1]);
  return initials.length > 0 ? initials : '?';
}

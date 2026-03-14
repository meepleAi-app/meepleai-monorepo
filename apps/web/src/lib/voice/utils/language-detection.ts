/**
 * Language Detection for TTS Output (#330)
 *
 * Simple heuristic-based language detection for Italian vs English.
 * Uses character frequency, common word matching, and diacritical markers
 * to determine the most likely language of a text snippet.
 *
 * Designed for MeepleAI where responses are either Italian or English.
 */

// ============================================================================
// Types
// ============================================================================

export type DetectedLanguage = 'it-IT' | 'en-US';

// ============================================================================
// Constants
// ============================================================================

/**
 * High-frequency Italian function words unlikely to appear in English text.
 * Weighted toward short, unambiguous markers.
 */
const ITALIAN_MARKERS: ReadonlySet<string> = new Set([
  'il',
  'lo',
  'la',
  'le',
  'gli',
  'un',
  'una',
  'uno',
  'di',
  'del',
  'della',
  'dei',
  'delle',
  'dello',
  'degli',
  'che',
  'è',
  'sono',
  'hai',
  'ha',
  'hanno',
  'sei',
  'siamo',
  'non',
  'per',
  'con',
  'nel',
  'nella',
  'questo',
  'questa',
  'come',
  'anche',
  'più',
  'perché',
  'quando',
  'dove',
  'gioco',
  'giocatori',
  'turno',
  'regole',
  'carta',
  'carte',
  'pedina',
  'pedine',
  'dado',
  'dadi',
  'tabellone',
  'punteggio',
]);

/**
 * High-frequency English function words unlikely to appear in Italian text.
 */
const ENGLISH_MARKERS: ReadonlySet<string> = new Set([
  'the',
  'is',
  'are',
  'was',
  'were',
  'been',
  'being',
  'have',
  'has',
  'had',
  'do',
  'does',
  'did',
  'will',
  'would',
  'could',
  'should',
  'might',
  'this',
  'that',
  'these',
  'those',
  'which',
  'what',
  'from',
  'with',
  'about',
  'into',
  'through',
  'game',
  'player',
  'players',
  'turn',
  'rules',
  'card',
  'cards',
  'board',
  'dice',
  'score',
  'piece',
  'pieces',
]);

/**
 * Italian diacritical characters — strong signal for Italian.
 */
const ITALIAN_DIACRITICS = /[àèéìòù]/;

// ============================================================================
// Detection
// ============================================================================

/**
 * Detect whether text is Italian or English.
 *
 * @param text - Text to analyze (typically an AI response)
 * @param fallback - Language to return when detection is inconclusive (default: 'it-IT')
 * @returns BCP 47 language tag
 */
export function detectLanguage(
  text: string,
  fallback: DetectedLanguage = 'it-IT'
): DetectedLanguage {
  if (!text || text.trim().length === 0) return fallback;

  // Normalize: lowercase, extract words (strip markdown/punctuation)
  const cleaned = text
    .replace(/```[\s\S]*?```/g, '') // strip code blocks
    .replace(/`[^`]+`/g, '') // strip inline code
    .replace(/https?:\/\/[^\s]+/g, '') // strip URLs
    .toLowerCase();

  const words = cleaned.match(/[a-zàèéìòùa-z]+/g);
  if (!words || words.length < 3) return fallback;

  // Check for Italian diacritics — strong signal
  const hasDiacritics = ITALIAN_DIACRITICS.test(cleaned);

  // Count marker word hits
  let italianHits = 0;
  let englishHits = 0;

  for (const word of words) {
    if (ITALIAN_MARKERS.has(word)) italianHits++;
    if (ENGLISH_MARKERS.has(word)) englishHits++;
  }

  // Diacritics bonus for Italian
  if (hasDiacritics) {
    italianHits += 3;
  }

  // Calculate ratios
  const totalHits = italianHits + englishHits;
  if (totalHits === 0) return fallback;

  const italianRatio = italianHits / totalHits;

  // Threshold: >55% Italian markers → Italian, otherwise English
  if (italianRatio > 0.55) return 'it-IT';
  if (italianRatio < 0.45) return 'en-US';

  return fallback;
}

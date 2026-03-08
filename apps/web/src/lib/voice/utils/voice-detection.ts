/**
 * Voice Detection Utilities
 *
 * Browser capability detection for speech recognition and synthesis.
 * All functions are SSR-safe with typeof window checks.
 */

export function isSpeechRecognitionSupported(): boolean {
  if (typeof window === 'undefined') return false;
  return 'SpeechRecognition' in window || 'webkitSpeechRecognition' in window;
}

export function isSpeechSynthesisSupported(): boolean {
  if (typeof window === 'undefined') return false;
  return 'speechSynthesis' in window;
}

export function getSpeechRecognitionConstructor(): typeof SpeechRecognition | null {
  if (typeof window === 'undefined') return null;
  return (
    (window as unknown as Record<string, typeof SpeechRecognition>).SpeechRecognition ||
    (window as unknown as Record<string, typeof SpeechRecognition>).webkitSpeechRecognition ||
    null
  );
}

/**
 * Find the best available voice for the given language.
 *
 * Priority order:
 * 1. Edge "Natural" voices (highest quality neural TTS)
 * 2. Chrome "Google" voices (good quality)
 * 3. Any voice matching the language prefix
 *
 * @param lang - BCP 47 language tag (e.g. 'it-IT', 'en-US')
 * @returns Best matching voice or null if none found
 */
export function findBestVoice(lang: string): SpeechSynthesisVoice | null {
  if (typeof window === 'undefined') return null;

  const voices = speechSynthesis.getVoices();
  const langPrefix = lang.substring(0, 2);

  const natural = voices.find(v => v.lang.startsWith(langPrefix) && v.name.includes('Natural'));
  if (natural) return natural;

  const google = voices.find(v => v.lang.startsWith(langPrefix) && v.name.includes('Google'));
  if (google) return google;

  return voices.find(v => v.lang.startsWith(langPrefix)) ?? null;
}

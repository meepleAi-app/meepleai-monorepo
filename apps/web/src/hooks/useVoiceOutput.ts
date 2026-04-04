'use client';

/**
 * useVoiceOutput - Text-to-Speech React Hook
 *
 * Manages SpeechSynthesis TTS: voice selection, text sanitization,
 * truncation, and Firefox cancel() cooldown workaround.
 *
 * @example
 * ```tsx
 * const { speak, stop, isSpeaking, availableVoices, selectedVoice, setVoice } =
 *   useVoiceOutput({ language: 'it-IT', rate: 1.0 });
 *
 * speak("Benvenuto in MeepleAI!");
 * ```
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import { detectLanguage } from '@/lib/voice/utils/language-detection';
import { sanitizeForTts } from '@/lib/voice/utils/text-sanitizer';
import { findBestVoice, isSpeechSynthesisSupported } from '@/lib/voice/utils/voice-detection';

// ============================================================================
// Types
// ============================================================================

export interface UseVoiceOutputOptions {
  /** BCP 47 language tag (default: 'it-IT') */
  language?: string;
  /** Preferred SpeechSynthesisVoice.voiceURI to use */
  preferredVoiceURI?: string;
  /** Max character length before truncation (default: 500) */
  maxTextLength?: number;
  /** Speech rate 0.5-2.0 (default: 1.0) */
  rate?: number;
  /** Auto-detect response language for TTS (#330). Default: false */
  autoDetectLanguage?: boolean;
}

export interface UseVoiceOutputReturn {
  /** Speak the given text (sanitized and truncated automatically).
   *  If autoDetectLanguage is enabled, the language is detected from text content. */
  speak: (text: string) => void;
  /** Stop current speech immediately */
  stop: () => void;
  /** Whether speech is currently playing */
  isSpeaking: boolean;
  /** Whether the browser supports speech synthesis */
  isSupported: boolean;
  /** All available voices for the current language */
  availableVoices: SpeechSynthesisVoice[];
  /** Currently selected voice, or null */
  selectedVoice: SpeechSynthesisVoice | null;
  /** Set voice by voiceURI string */
  setVoice: (voiceURI: string) => void;
}

// ============================================================================
// Constants
// ============================================================================

const DEFAULT_LANGUAGE = 'it-IT';
const DEFAULT_MAX_TEXT_LENGTH = 500;
const DEFAULT_RATE = 1.0;
const TRUNCATION_SUFFIX_IT = '... leggi la risposta completa sullo schermo';
const TRUNCATION_SUFFIX_EN = '... read the full response on screen';

/**
 * Firefox workaround: calling speechSynthesis.speak() too quickly after
 * speechSynthesis.cancel() causes silent failures. Wait at least this
 * many milliseconds after cancel() before speaking again.
 */
const CANCEL_COOLDOWN_MS = 600;

// ============================================================================
// Hook Implementation
// ============================================================================

export function useVoiceOutput(options: UseVoiceOutputOptions = {}): UseVoiceOutputReturn {
  const {
    language = DEFAULT_LANGUAGE,
    preferredVoiceURI,
    maxTextLength = DEFAULT_MAX_TEXT_LENGTH,
    rate = DEFAULT_RATE,
    autoDetectLanguage = false,
  } = options;

  const [isSpeaking, setIsSpeaking] = useState(false);
  const [availableVoices, setAvailableVoices] = useState<SpeechSynthesisVoice[]>([]);
  const [selectedVoice, setSelectedVoice] = useState<SpeechSynthesisVoice | null>(null);
  const [isSupported] = useState(() => isSpeechSynthesisSupported());

  // Mutable refs for timers and cooldown tracking
  const lastCancelTimeRef = useRef<number>(0);
  const pendingSpeakRef = useRef<string | null>(null);
  const pendingLangRef = useRef<string | null>(null);
  const cooldownTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const utteranceRef = useRef<SpeechSynthesisUtterance | null>(null);

  // ------------------------------------------------------------------
  // Voice loading
  // ------------------------------------------------------------------

  const loadVoices = useCallback(() => {
    if (!isSupported) return;

    const voices = speechSynthesis.getVoices();
    if (voices.length === 0) return;

    setAvailableVoices(voices);

    // Try to match preferred voice first, then auto-detect best
    if (preferredVoiceURI) {
      const preferred = voices.find(v => v.voiceURI === preferredVoiceURI);
      if (preferred) {
        setSelectedVoice(preferred);
        return;
      }
    }

    const best = findBestVoice(language);
    if (best) {
      setSelectedVoice(best);
    }
  }, [isSupported, language, preferredVoiceURI]);

  useEffect(() => {
    if (!isSupported) return;

    // Some browsers load voices asynchronously
    loadVoices();

    const handler = () => loadVoices();
    speechSynthesis.addEventListener('voiceschanged', handler);

    return () => {
      speechSynthesis.removeEventListener('voiceschanged', handler);
    };
  }, [isSupported, loadVoices]);

  // ------------------------------------------------------------------
  // Voice selection
  // ------------------------------------------------------------------

  const setVoice = useCallback(
    (voiceURI: string) => {
      const voice = availableVoices.find(v => v.voiceURI === voiceURI);
      if (voice) {
        setSelectedVoice(voice);
      }
    },
    [availableVoices]
  );

  // ------------------------------------------------------------------
  // Text processing
  // ------------------------------------------------------------------

  const processText = useCallback(
    (text: string, lang?: string): string => {
      let processed = sanitizeForTts(text);

      if (processed.length > maxTextLength) {
        const suffix = lang?.startsWith('en') ? TRUNCATION_SUFFIX_EN : TRUNCATION_SUFFIX_IT;
        processed = processed.substring(0, maxTextLength) + suffix;
      }

      return processed;
    },
    [maxTextLength]
  );

  // ------------------------------------------------------------------
  // Core speak/stop
  // ------------------------------------------------------------------

  const speakNow = useCallback(
    (text: string, langOverride?: string) => {
      if (!isSupported) return;

      const effectiveLang = langOverride ?? language;
      const processed = processText(text, effectiveLang);
      if (!processed) return;

      // Cancel any current speech first
      if (speechSynthesis.speaking) {
        speechSynthesis.cancel();
      }
      const utterance = new SpeechSynthesisUtterance(processed);
      utterance.lang = effectiveLang;
      utterance.rate = Math.max(0.5, Math.min(2.0, rate));

      // When language differs from default, pick the best voice for that language
      if (langOverride && langOverride !== language) {
        const langVoice = findBestVoice(langOverride);
        if (langVoice) {
          utterance.voice = langVoice;
        }
      } else if (selectedVoice) {
        utterance.voice = selectedVoice;
      }

      utterance.onstart = () => {
        setIsSpeaking(true);
      };

      utterance.onend = () => {
        setIsSpeaking(false);
        utteranceRef.current = null;
      };

      utterance.onerror = event => {
        // 'canceled' is expected when stop() is called
        if (event.error !== 'canceled') {
          setIsSpeaking(false);
        }
        utteranceRef.current = null;
      };

      utteranceRef.current = utterance;
      speechSynthesis.speak(utterance);
    },
    [isSupported, processText, language, rate, selectedVoice]
  );

  const speak = useCallback(
    (text: string) => {
      if (!isSupported) return;

      // Auto-detect language when enabled (#330)
      const langOverride = autoDetectLanguage
        ? detectLanguage(text, language as 'it-IT' | 'en-US')
        : undefined;

      const elapsed = Date.now() - lastCancelTimeRef.current;

      if (elapsed < CANCEL_COOLDOWN_MS) {
        // Firefox workaround: defer speaking until cooldown passes
        pendingSpeakRef.current = text;
        pendingLangRef.current = langOverride ?? null;

        // Clear any existing cooldown timer
        if (cooldownTimerRef.current !== null) {
          clearTimeout(cooldownTimerRef.current);
        }

        cooldownTimerRef.current = setTimeout(() => {
          cooldownTimerRef.current = null;
          const pending = pendingSpeakRef.current;
          const pendingLang = pendingLangRef.current;
          pendingSpeakRef.current = null;
          pendingLangRef.current = null;
          if (pending) {
            speakNow(pending, pendingLang ?? undefined);
          }
        }, CANCEL_COOLDOWN_MS - elapsed);
      } else {
        pendingSpeakRef.current = null;
        pendingLangRef.current = null;
        speakNow(text, langOverride);
      }
    },
    [isSupported, autoDetectLanguage, language, speakNow]
  );

  const stop = useCallback(() => {
    if (!isSupported) return;

    // Clear any pending deferred speak
    if (cooldownTimerRef.current !== null) {
      clearTimeout(cooldownTimerRef.current);
      cooldownTimerRef.current = null;
    }
    pendingSpeakRef.current = null;
    pendingLangRef.current = null;

    if (speechSynthesis.speaking || speechSynthesis.pending) {
      speechSynthesis.cancel();
      lastCancelTimeRef.current = Date.now();
      setIsSpeaking(false);
      utteranceRef.current = null;
    }
  }, [isSupported]);

  // ------------------------------------------------------------------
  // Cleanup on unmount
  // ------------------------------------------------------------------

  useEffect(() => {
    return () => {
      if (cooldownTimerRef.current !== null) {
        clearTimeout(cooldownTimerRef.current);
        cooldownTimerRef.current = null;
      }

      if (isSupported && (speechSynthesis.speaking || speechSynthesis.pending)) {
        speechSynthesis.cancel();
      }

      utteranceRef.current = null;
    };
  }, [isSupported]);

  return {
    speak,
    stop,
    isSpeaking,
    isSupported,
    availableVoices,
    selectedVoice,
    setVoice,
  };
}

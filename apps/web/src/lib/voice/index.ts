/**
 * Voice Input Foundation - Public API
 *
 * Provides speech recognition and text-to-speech utilities
 * for the MeepleAI voice input system.
 *
 * @example
 * import { createSpeechRecognitionProvider, DEFAULT_VOICE_CONFIG } from '@/lib/voice';
 * import { sanitizeForTts, findBestVoice } from '@/lib/voice';
 */

// Types
export type {
  ISpeechRecognitionProvider,
  SpeechRecognitionConfig,
  SpeechRecognitionEvents,
  VoiceError,
  VoiceErrorCode,
  VoicePreferences,
  VoiceRecognitionState,
} from './types';

export { DEFAULT_VOICE_CONFIG, DEFAULT_VOICE_PREFERENCES } from './types';

// Provider factory
export { createSpeechRecognitionProvider } from './providers/provider-factory';

// Provider implementations (for testing / advanced usage)
export { WebSpeechProvider } from './providers/web-speech-provider';

// Utilities
export { sanitizeForTts } from './utils/text-sanitizer';
export {
  findBestVoice,
  getSpeechRecognitionConstructor,
  isSpeechRecognitionSupported,
  isSpeechSynthesisSupported,
} from './utils/voice-detection';

/**
 * Speech Recognition Provider Factory
 *
 * Creates the appropriate ISpeechRecognitionProvider based on
 * environment configuration.
 *
 * Phase 1: Always returns WebSpeechProvider (browser native API)
 * Phase 2: Will check NEXT_PUBLIC_VOICE_SERVER_STT for server-side STT
 */

import { WebSpeechProvider } from './web-speech-provider';

import type { ISpeechRecognitionProvider } from '../types';

export function createSpeechRecognitionProvider(): ISpeechRecognitionProvider {
  // Phase 1: always use WebSpeechProvider (browser native API)
  // Phase 2: check NEXT_PUBLIC_VOICE_SERVER_STT env var for server-side STT
  return new WebSpeechProvider();
}

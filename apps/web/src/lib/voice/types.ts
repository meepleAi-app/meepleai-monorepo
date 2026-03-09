/**
 * Voice Input Foundation - Type Definitions
 *
 * Core types for speech recognition and text-to-speech
 * in the MeepleAI voice input system.
 */

export type VoiceRecognitionState = 'idle' | 'requesting' | 'listening' | 'processing' | 'error';

export interface SpeechRecognitionEvents {
  onInterimResult: (text: string) => void;
  onFinalResult: (text: string, confidence: number) => void;
  onSpeechStart: () => void;
  onSpeechEnd: () => void;
  onError: (error: VoiceError) => void;
  onStateChange: (state: VoiceRecognitionState) => void;
}

export interface VoiceError {
  code: VoiceErrorCode;
  message: string;
  recoverable: boolean;
}

export type VoiceErrorCode =
  | 'permission_denied'
  | 'not_supported'
  | 'no_speech'
  | 'audio_capture_error'
  | 'network_error'
  | 'server_error'
  | 'aborted'
  | 'language_not_supported';

export interface SpeechRecognitionConfig {
  language: string;
  interimResults: boolean;
  silenceTimeoutMs: number;
  maxDurationMs: number;
  noSpeechTimeoutMs: number;
}

export interface ISpeechRecognitionProvider {
  readonly isSupported: boolean;
  readonly state: VoiceRecognitionState;
  start(config: SpeechRecognitionConfig, events: SpeechRecognitionEvents): void;
  stop(): void;
  abort(): void;
  dispose(): void;
}

export interface VoicePreferences {
  ttsEnabled: boolean;
  autoSend: boolean;
  language: string;
  voiceURI: string | null;
  rate: number;
}

export const DEFAULT_VOICE_PREFERENCES: VoicePreferences = {
  ttsEnabled: false,
  autoSend: true,
  language: 'it-IT',
  voiceURI: null,
  rate: 1.0,
};

export const DEFAULT_VOICE_CONFIG: SpeechRecognitionConfig = {
  language: 'it-IT',
  interimResults: true,
  silenceTimeoutMs: 2000,
  maxDurationMs: 30000,
  noSpeechTimeoutMs: 5000,
};

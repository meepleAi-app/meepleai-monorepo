'use client';

/**
 * Web Speech API Provider
 *
 * Production implementation of ISpeechRecognitionProvider wrapping
 * the browser's native SpeechRecognition / webkitSpeechRecognition API.
 *
 * Handles:
 * - Interim and final result streaming with confidence scores
 * - Silence timeout (auto-stop after speech ends with no new results)
 * - Max duration timeout (hard limit on recognition session)
 * - No-speech timeout (no speech detected after start)
 * - Browser error code mapping to VoiceErrorCode
 * - Clean state machine transitions
 */

import { getSpeechRecognitionConstructor } from '../utils/voice-detection';

import type {
  ISpeechRecognitionProvider,
  SpeechRecognitionConfig,
  SpeechRecognitionEvents,
  VoiceErrorCode,
  VoiceRecognitionState,
} from '../types';

const BROWSER_ERROR_MAP: Record<string, VoiceErrorCode> = {
  'not-allowed': 'permission_denied',
  'no-speech': 'no_speech',
  'audio-capture': 'audio_capture_error',
  network: 'network_error',
  aborted: 'aborted',
  'service-not-allowed': 'permission_denied',
  'language-not-supported': 'language_not_supported',
};

const RECOVERABLE_ERRORS = new Set<VoiceErrorCode>([
  'no_speech',
  'audio_capture_error',
  'network_error',
  'aborted',
]);

export class WebSpeechProvider implements ISpeechRecognitionProvider {
  private _state: VoiceRecognitionState = 'idle';
  private recognition: SpeechRecognition | null = null;
  private events: SpeechRecognitionEvents | null = null;

  private silenceTimer: ReturnType<typeof setTimeout> | null = null;
  private maxDurationTimer: ReturnType<typeof setTimeout> | null = null;
  private noSpeechTimer: ReturnType<typeof setTimeout> | null = null;

  private config: SpeechRecognitionConfig | null = null;
  private hasSpeechStarted = false;

  get isSupported(): boolean {
    return getSpeechRecognitionConstructor() !== null;
  }

  get state(): VoiceRecognitionState {
    return this._state;
  }

  start(config: SpeechRecognitionConfig, events: SpeechRecognitionEvents): void {
    if (this._state !== 'idle' && this._state !== 'error') {
      return;
    }

    const SpeechRecognitionCtor = getSpeechRecognitionConstructor();
    if (!SpeechRecognitionCtor) {
      this.setState('error', events);
      events.onError({
        code: 'not_supported',
        message: 'Speech recognition is not supported in this browser.',
        recoverable: false,
      });
      return;
    }

    this.config = config;
    this.events = events;
    this.hasSpeechStarted = false;

    this.setState('requesting', events);

    const recognition = new SpeechRecognitionCtor();
    recognition.lang = config.language;
    recognition.interimResults = config.interimResults;
    recognition.continuous = false;
    recognition.maxAlternatives = 1;

    this.attachHandlers(recognition);
    this.recognition = recognition;

    try {
      recognition.start();
    } catch (err) {
      this.clearAllTimers();
      this.setState('error', events);
      events.onError({
        code: 'audio_capture_error',
        message: err instanceof Error ? err.message : 'Failed to start speech recognition.',
        recoverable: true,
      });
    }
  }

  stop(): void {
    if (!this.recognition) return;

    if (this._state === 'listening') {
      this.setState('processing', this.events);
    }

    this.clearAllTimers();

    try {
      this.recognition.stop();
    } catch {
      // Recognition may already be stopped
      this.cleanup();
    }
  }

  abort(): void {
    this.clearAllTimers();

    if (!this.recognition) return;

    try {
      this.recognition.abort();
    } catch {
      // Recognition may already be stopped
    }

    this.cleanup();
  }

  dispose(): void {
    this.abort();
    this.events = null;
    this.config = null;
  }

  // ---------------------------------------------------------------
  // Private helpers
  // ---------------------------------------------------------------

  private attachHandlers(recognition: SpeechRecognition): void {
    recognition.onaudiostart = () => {
      // Microphone permission granted, audio is flowing
      this.setState('listening', this.events);
      this.startNoSpeechTimer();
      this.startMaxDurationTimer();
    };

    recognition.onspeechstart = () => {
      this.hasSpeechStarted = true;
      this.clearTimer('noSpeech');
      this.events?.onSpeechStart();
    };

    recognition.onspeechend = () => {
      this.events?.onSpeechEnd();
      this.startSilenceTimer();
    };

    recognition.onresult = (event: SpeechRecognitionEvent) => {
      this.clearTimer('silence');
      this.clearTimer('noSpeech');

      const lastResultIndex = event.results.length - 1;
      const result = event.results[lastResultIndex];
      if (!result) return;

      const alternative = result[0];
      if (!alternative) return;

      if (result.isFinal) {
        this.events?.onFinalResult(alternative.transcript, alternative.confidence);
      } else {
        this.events?.onInterimResult(alternative.transcript);
        // Restart silence timer after each interim result
        if (this.hasSpeechStarted) {
          this.startSilenceTimer();
        }
      }
    };

    recognition.onerror = (event: SpeechRecognitionErrorEvent) => {
      this.clearAllTimers();

      const code = mapBrowserError(event.error);

      // 'no-speech' and 'aborted' can happen during normal flow
      // when user stops before speaking — don't treat as hard error
      // if we explicitly called stop/abort
      if (event.error === 'aborted' && this._state === 'processing') {
        return;
      }

      this.setState('error', this.events);
      this.events?.onError({
        code,
        message: errorMessage(code, event.message),
        recoverable: RECOVERABLE_ERRORS.has(code),
      });
    };

    recognition.onend = () => {
      this.clearAllTimers();

      // onend fires after both stop() and abort(), and after errors.
      // Only transition to idle if we haven't already moved to error.
      if (this._state !== 'error') {
        this.setState('idle', this.events);
      }

      this.recognition = null;
    };
  }

  private startSilenceTimer(): void {
    this.clearTimer('silence');
    if (!this.config) return;

    this.silenceTimer = setTimeout(() => {
      this.stop();
    }, this.config.silenceTimeoutMs);
  }

  private startMaxDurationTimer(): void {
    this.clearTimer('maxDuration');
    if (!this.config) return;

    this.maxDurationTimer = setTimeout(() => {
      this.stop();
    }, this.config.maxDurationMs);
  }

  private startNoSpeechTimer(): void {
    this.clearTimer('noSpeech');
    if (!this.config) return;

    this.noSpeechTimer = setTimeout(() => {
      if (!this.hasSpeechStarted) {
        this.abort();
        this.setState('error', this.events);
        this.events?.onError({
          code: 'no_speech',
          message: 'No speech was detected. Please try again.',
          recoverable: true,
        });
      }
    }, this.config.noSpeechTimeoutMs);
  }

  private clearTimer(which: 'silence' | 'maxDuration' | 'noSpeech'): void {
    switch (which) {
      case 'silence':
        if (this.silenceTimer !== null) {
          clearTimeout(this.silenceTimer);
          this.silenceTimer = null;
        }
        break;
      case 'maxDuration':
        if (this.maxDurationTimer !== null) {
          clearTimeout(this.maxDurationTimer);
          this.maxDurationTimer = null;
        }
        break;
      case 'noSpeech':
        if (this.noSpeechTimer !== null) {
          clearTimeout(this.noSpeechTimer);
          this.noSpeechTimer = null;
        }
        break;
    }
  }

  private clearAllTimers(): void {
    this.clearTimer('silence');
    this.clearTimer('maxDuration');
    this.clearTimer('noSpeech');
  }

  private setState(newState: VoiceRecognitionState, events: SpeechRecognitionEvents | null): void {
    if (this._state === newState) return;
    this._state = newState;
    events?.onStateChange(newState);
  }

  private cleanup(): void {
    this.clearAllTimers();
    if (this._state !== 'error') {
      this.setState('idle', this.events);
    }
    this.recognition = null;
  }
}

// ---------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------

function mapBrowserError(browserError: string): VoiceErrorCode {
  return BROWSER_ERROR_MAP[browserError] ?? 'server_error';
}

function errorMessage(code: VoiceErrorCode, browserMessage?: string): string {
  const messages: Record<VoiceErrorCode, string> = {
    permission_denied:
      'Microphone permission was denied. Please allow microphone access and try again.',
    not_supported: 'Speech recognition is not supported in this browser.',
    no_speech: 'No speech was detected. Please try again.',
    audio_capture_error: 'Could not capture audio. Please check your microphone.',
    network_error: 'A network error occurred during speech recognition.',
    server_error: 'The speech recognition service encountered an error.',
    aborted: 'Speech recognition was cancelled.',
    language_not_supported: 'The selected language is not supported for speech recognition.',
  };

  return browserMessage || messages[code];
}

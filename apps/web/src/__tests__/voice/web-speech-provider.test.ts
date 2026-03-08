import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { SpeechRecognitionConfig, SpeechRecognitionEvents } from '@/lib/voice/types';
import { WebSpeechProvider } from '@/lib/voice/providers/web-speech-provider';

// ---------------------------------------------------------------------------
// Mock SpeechRecognition — tracks last created instance
// ---------------------------------------------------------------------------

let lastMockInstance: MockSpeechRecognition | null = null;

class MockSpeechRecognition {
  lang = '';
  interimResults = false;
  continuous = false;
  maxAlternatives = 1;

  onaudiostart: (() => void) | null = null;
  onspeechstart: (() => void) | null = null;
  onspeechend: (() => void) | null = null;
  onresult: ((event: unknown) => void) | null = null;
  onerror: ((event: unknown) => void) | null = null;
  onend: (() => void) | null = null;

  start = vi.fn();
  stop = vi.fn();
  abort = vi.fn();

  constructor() {
    lastMockInstance = this;
  }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeConfig(overrides?: Partial<SpeechRecognitionConfig>): SpeechRecognitionConfig {
  return {
    language: 'en-US',
    interimResults: true,
    silenceTimeoutMs: 2000,
    maxDurationMs: 30000,
    noSpeechTimeoutMs: 5000,
    ...overrides,
  };
}

function makeEvents(overrides?: Partial<SpeechRecognitionEvents>): SpeechRecognitionEvents {
  return {
    onInterimResult: vi.fn(),
    onFinalResult: vi.fn(),
    onSpeechStart: vi.fn(),
    onSpeechEnd: vi.fn(),
    onError: vi.fn(),
    onStateChange: vi.fn(),
    ...overrides,
  };
}

/** Start the provider and simulate audiostart so it reaches 'listening'. */
function startAndListen(
  provider: WebSpeechProvider,
  events: SpeechRecognitionEvents,
  config?: SpeechRecognitionConfig
): MockSpeechRecognition {
  provider.start(config ?? makeConfig(), events);
  const instance = lastMockInstance!;
  // Simulate browser granting mic access
  instance.onaudiostart?.();
  return instance;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('WebSpeechProvider', () => {
  let provider: WebSpeechProvider;

  beforeEach(() => {
    vi.useFakeTimers();
    lastMockInstance = null;
    provider = new WebSpeechProvider();

    Object.defineProperty(window, 'webkitSpeechRecognition', {
      value: MockSpeechRecognition,
      configurable: true,
      writable: true,
    });
  });

  afterEach(() => {
    provider.dispose();
    vi.useRealTimers();
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    delete (window as any).webkitSpeechRecognition;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    delete (window as any).SpeechRecognition;
  });

  // ------------------------------------------------------------------
  // isSupported
  // ------------------------------------------------------------------
  describe('isSupported', () => {
    it('returns true when webkitSpeechRecognition is available', () => {
      expect(provider.isSupported).toBe(true);
    });

    it('returns false when no recognition API is available', () => {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      delete (window as any).webkitSpeechRecognition;
      expect(provider.isSupported).toBe(false);
    });
  });

  // ------------------------------------------------------------------
  // start()
  // ------------------------------------------------------------------
  describe('start()', () => {
    it('transitions to requesting immediately', () => {
      const events = makeEvents();
      provider.start(makeConfig(), events);

      expect(provider.state).toBe('requesting');
      expect(events.onStateChange).toHaveBeenCalledWith('requesting');
    });

    it('transitions to listening on audiostart', () => {
      const events = makeEvents();
      startAndListen(provider, events);

      expect(provider.state).toBe('listening');
      expect(events.onStateChange).toHaveBeenCalledWith('listening');
    });

    it('configures recognition with provided config', () => {
      const events = makeEvents();
      provider.start(makeConfig({ language: 'it-IT' }), events);

      const instance = lastMockInstance!;
      expect(instance.lang).toBe('it-IT');
      expect(instance.interimResults).toBe(true);
      expect(instance.continuous).toBe(false);
      expect(instance.maxAlternatives).toBe(1);
    });

    it('calls recognition.start()', () => {
      const events = makeEvents();
      provider.start(makeConfig(), events);
      expect(lastMockInstance!.start).toHaveBeenCalled();
    });

    it('reports not_supported error when no constructor available', () => {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      delete (window as any).webkitSpeechRecognition;
      const events = makeEvents();
      provider.start(makeConfig(), events);

      expect(provider.state).toBe('error');
      expect(events.onError).toHaveBeenCalledWith(
        expect.objectContaining({
          code: 'not_supported',
          recoverable: false,
        })
      );
    });

    it('does nothing if already listening', () => {
      const events = makeEvents();
      startAndListen(provider, events);

      const events2 = makeEvents();
      provider.start(makeConfig(), events2);
      expect(events2.onStateChange).not.toHaveBeenCalled();
    });

    it('reports audio_capture_error if recognition.start() throws', () => {
      const events = makeEvents();

      // Make the constructor's start throw
      const ThrowingMock = class extends MockSpeechRecognition {
        start = vi.fn(() => {
          throw new Error('mic busy');
        });
      };
      Object.defineProperty(window, 'webkitSpeechRecognition', {
        value: ThrowingMock,
        configurable: true,
        writable: true,
      });

      provider.start(makeConfig(), events);

      expect(provider.state).toBe('error');
      expect(events.onError).toHaveBeenCalledWith(
        expect.objectContaining({
          code: 'audio_capture_error',
          message: 'mic busy',
          recoverable: true,
        })
      );
    });
  });

  // ------------------------------------------------------------------
  // stop()
  // ------------------------------------------------------------------
  describe('stop()', () => {
    it('calls recognition.stop() and transitions to processing', () => {
      const events = makeEvents();
      const instance = startAndListen(provider, events);

      provider.stop();

      expect(instance.stop).toHaveBeenCalled();
      expect(provider.state).toBe('processing');
    });

    it('does nothing when no recognition exists', () => {
      expect(() => provider.stop()).not.toThrow();
    });
  });

  // ------------------------------------------------------------------
  // abort()
  // ------------------------------------------------------------------
  describe('abort()', () => {
    it('calls recognition.abort() and transitions to idle', () => {
      const events = makeEvents();
      const instance = startAndListen(provider, events);

      provider.abort();

      expect(instance.abort).toHaveBeenCalled();
      expect(provider.state).toBe('idle');
    });

    it('does nothing when no recognition exists', () => {
      expect(() => provider.abort()).not.toThrow();
    });
  });

  // ------------------------------------------------------------------
  // Error mapping
  // ------------------------------------------------------------------
  describe('error events', () => {
    it('maps not-allowed to permission_denied', () => {
      const events = makeEvents();
      const instance = startAndListen(provider, events);

      instance.onerror?.({ error: 'not-allowed', message: '' });

      expect(events.onError).toHaveBeenCalledWith(
        expect.objectContaining({
          code: 'permission_denied',
          recoverable: false,
        })
      );
    });

    it('maps no-speech to no_speech (recoverable)', () => {
      const events = makeEvents();
      const instance = startAndListen(provider, events);

      instance.onerror?.({ error: 'no-speech', message: '' });

      expect(events.onError).toHaveBeenCalledWith(
        expect.objectContaining({
          code: 'no_speech',
          recoverable: true,
        })
      );
    });

    it('maps audio-capture to audio_capture_error (recoverable)', () => {
      const events = makeEvents();
      const instance = startAndListen(provider, events);

      instance.onerror?.({ error: 'audio-capture', message: '' });

      expect(events.onError).toHaveBeenCalledWith(
        expect.objectContaining({
          code: 'audio_capture_error',
          recoverable: true,
        })
      );
    });

    it('maps network to network_error (recoverable)', () => {
      const events = makeEvents();
      const instance = startAndListen(provider, events);

      instance.onerror?.({ error: 'network', message: '' });

      expect(events.onError).toHaveBeenCalledWith(
        expect.objectContaining({
          code: 'network_error',
          recoverable: true,
        })
      );
    });

    it('maps unknown errors to server_error', () => {
      const events = makeEvents();
      const instance = startAndListen(provider, events);

      instance.onerror?.({ error: 'some-unknown-error', message: '' });

      expect(events.onError).toHaveBeenCalledWith(
        expect.objectContaining({ code: 'server_error' })
      );
    });

    it('maps language-not-supported to language_not_supported', () => {
      const events = makeEvents();
      const instance = startAndListen(provider, events);

      instance.onerror?.({ error: 'language-not-supported', message: '' });

      expect(events.onError).toHaveBeenCalledWith(
        expect.objectContaining({ code: 'language_not_supported' })
      );
    });

    it('sets state to error on browser error', () => {
      const events = makeEvents();
      const instance = startAndListen(provider, events);

      instance.onerror?.({ error: 'not-allowed', message: '' });

      expect(provider.state).toBe('error');
      expect(events.onStateChange).toHaveBeenCalledWith('error');
    });
  });

  // ------------------------------------------------------------------
  // onend
  // ------------------------------------------------------------------
  describe('onend', () => {
    it('transitions to idle on normal end', () => {
      const events = makeEvents();
      const instance = startAndListen(provider, events);

      instance.onend?.();

      expect(provider.state).toBe('idle');
    });

    it('stays in error state if error occurred before end', () => {
      const events = makeEvents();
      const instance = startAndListen(provider, events);

      instance.onerror?.({ error: 'not-allowed', message: '' });
      instance.onend?.();

      expect(provider.state).toBe('error');
    });
  });

  // ------------------------------------------------------------------
  // dispose()
  // ------------------------------------------------------------------
  describe('dispose()', () => {
    it('aborts and cleans up without throwing', () => {
      const events = makeEvents();
      startAndListen(provider, events);

      expect(() => provider.dispose()).not.toThrow();
      expect(provider.state).toBe('idle');
    });

    it('is safe to call multiple times', () => {
      expect(() => {
        provider.dispose();
        provider.dispose();
      }).not.toThrow();
    });

    it('can start again after dispose (from a new idle state)', () => {
      const events = makeEvents();
      startAndListen(provider, events);
      provider.dispose();

      // Re-install the mock since dispose may have cleaned up
      Object.defineProperty(window, 'webkitSpeechRecognition', {
        value: MockSpeechRecognition,
        configurable: true,
        writable: true,
      });

      const events2 = makeEvents();
      provider.start(makeConfig(), events2);
      expect(provider.state).toBe('requesting');
    });
  });

  // ------------------------------------------------------------------
  // Result handling
  // ------------------------------------------------------------------
  describe('result events', () => {
    it('calls onInterimResult for non-final results', () => {
      const events = makeEvents();
      const instance = startAndListen(provider, events);

      instance.onresult?.({
        results: [
          {
            0: { transcript: 'hello wo', confidence: 0.5 },
            length: 1,
            isFinal: false,
          },
        ],
      });

      expect(events.onInterimResult).toHaveBeenCalledWith('hello wo');
    });

    it('calls onFinalResult for final results', () => {
      const events = makeEvents();
      const instance = startAndListen(provider, events);

      instance.onresult?.({
        results: [
          {
            0: { transcript: 'hello world', confidence: 0.95 },
            length: 1,
            isFinal: true,
          },
        ],
      });

      expect(events.onFinalResult).toHaveBeenCalledWith('hello world', 0.95);
    });
  });

  // ------------------------------------------------------------------
  // Speech lifecycle events
  // ------------------------------------------------------------------
  describe('speech lifecycle', () => {
    it('fires onSpeechStart when speech begins', () => {
      const events = makeEvents();
      const instance = startAndListen(provider, events);

      instance.onspeechstart?.();
      expect(events.onSpeechStart).toHaveBeenCalled();
    });

    it('fires onSpeechEnd when speech ends', () => {
      const events = makeEvents();
      const instance = startAndListen(provider, events);

      instance.onspeechend?.();
      expect(events.onSpeechEnd).toHaveBeenCalled();
    });
  });
});

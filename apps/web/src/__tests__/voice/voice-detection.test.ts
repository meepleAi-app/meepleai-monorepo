import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  findBestVoice,
  getSpeechRecognitionConstructor,
  isSpeechRecognitionSupported,
  isSpeechSynthesisSupported,
} from '@/lib/voice/utils/voice-detection';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function mockVoice(overrides: Partial<SpeechSynthesisVoice>): SpeechSynthesisVoice {
  return {
    default: false,
    lang: 'en-US',
    localService: true,
    name: 'Test Voice',
    voiceURI: 'test-voice-uri',
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('isSpeechRecognitionSupported', () => {
  afterEach(() => {
    // Clean up any properties we set
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    delete (window as any).SpeechRecognition;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    delete (window as any).webkitSpeechRecognition;
  });

  it('returns true when SpeechRecognition exists on window', () => {
    Object.defineProperty(window, 'SpeechRecognition', {
      value: vi.fn(),
      configurable: true,
    });
    expect(isSpeechRecognitionSupported()).toBe(true);
  });

  it('returns true when webkitSpeechRecognition exists on window', () => {
    Object.defineProperty(window, 'webkitSpeechRecognition', {
      value: vi.fn(),
      configurable: true,
    });
    expect(isSpeechRecognitionSupported()).toBe(true);
  });

  it('returns false when neither exists', () => {
    expect(isSpeechRecognitionSupported()).toBe(false);
  });
});

describe('isSpeechSynthesisSupported', () => {
  it('returns true when speechSynthesis exists on window', () => {
    // speechSynthesis is typically defined in jsdom — but to be explicit:
    if (!('speechSynthesis' in window)) {
      Object.defineProperty(window, 'speechSynthesis', {
        value: { getVoices: () => [] },
        configurable: true,
      });
    }
    expect(isSpeechSynthesisSupported()).toBe(true);
  });

  it('returns false when speechSynthesis is absent', () => {
    const original = window.speechSynthesis;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    delete (window as any).speechSynthesis;
    expect(isSpeechSynthesisSupported()).toBe(false);

    // Restore
    if (original) {
      Object.defineProperty(window, 'speechSynthesis', {
        value: original,
        configurable: true,
      });
    }
  });
});

describe('getSpeechRecognitionConstructor', () => {
  afterEach(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    delete (window as any).SpeechRecognition;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    delete (window as any).webkitSpeechRecognition;
  });

  it('returns SpeechRecognition constructor when available', () => {
    const ctor = vi.fn();
    Object.defineProperty(window, 'SpeechRecognition', {
      value: ctor,
      configurable: true,
    });
    expect(getSpeechRecognitionConstructor()).toBe(ctor);
  });

  it('falls back to webkitSpeechRecognition', () => {
    const ctor = vi.fn();
    Object.defineProperty(window, 'webkitSpeechRecognition', {
      value: ctor,
      configurable: true,
    });
    expect(getSpeechRecognitionConstructor()).toBe(ctor);
  });

  it('prefers SpeechRecognition over webkit prefix', () => {
    const standard = vi.fn();
    const webkit = vi.fn();
    Object.defineProperty(window, 'SpeechRecognition', {
      value: standard,
      configurable: true,
    });
    Object.defineProperty(window, 'webkitSpeechRecognition', {
      value: webkit,
      configurable: true,
    });
    expect(getSpeechRecognitionConstructor()).toBe(standard);
  });

  it('returns null when neither is available', () => {
    expect(getSpeechRecognitionConstructor()).toBeNull();
  });
});

describe('findBestVoice', () => {
  let getVoicesSpy: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    getVoicesSpy = vi.fn().mockReturnValue([]);
    if (!('speechSynthesis' in window)) {
      Object.defineProperty(window, 'speechSynthesis', {
        value: { getVoices: getVoicesSpy },
        configurable: true,
        writable: true,
      });
    } else {
      vi.spyOn(window.speechSynthesis, 'getVoices').mockImplementation(getVoicesSpy);
    }
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('returns null when no voices are available', () => {
    getVoicesSpy.mockReturnValue([]);
    expect(findBestVoice('en-US')).toBeNull();
  });

  it('prefers Edge Natural voices (highest priority)', () => {
    const natural = mockVoice({
      name: 'Microsoft Aria Natural',
      lang: 'en-US',
    });
    const google = mockVoice({ name: 'Google US English', lang: 'en-US' });
    const generic = mockVoice({ name: 'English Voice', lang: 'en-GB' });

    getVoicesSpy.mockReturnValue([generic, google, natural]);
    expect(findBestVoice('en-US')).toBe(natural);
  });

  it('falls back to Chrome Google voices when no Natural voice', () => {
    const google = mockVoice({ name: 'Google US English', lang: 'en-US' });
    const generic = mockVoice({ name: 'English Voice', lang: 'en-GB' });

    getVoicesSpy.mockReturnValue([generic, google]);
    expect(findBestVoice('en-US')).toBe(google);
  });

  it('falls back to any language-matching voice', () => {
    const italian = mockVoice({ name: 'Paolo', lang: 'it-IT' });
    const german = mockVoice({ name: 'Hans', lang: 'de-DE' });

    getVoicesSpy.mockReturnValue([german, italian]);
    expect(findBestVoice('it-IT')).toBe(italian);
  });

  it('matches by language prefix (it matches it-IT)', () => {
    const voice = mockVoice({ name: 'Alice', lang: 'it-IT' });
    getVoicesSpy.mockReturnValue([voice]);
    expect(findBestVoice('it')).toBe(voice);
  });

  it('returns null when no voice matches the language', () => {
    const english = mockVoice({ name: 'English', lang: 'en-US' });
    getVoicesSpy.mockReturnValue([english]);
    expect(findBestVoice('ja-JP')).toBeNull();
  });
});

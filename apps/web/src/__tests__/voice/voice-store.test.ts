import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { DEFAULT_VOICE_PREFERENCES } from '@/lib/voice/types';
import { useVoicePreferencesStore } from '@/stores/voice/store';

describe('useVoicePreferencesStore', () => {
  beforeEach(() => {
    // Reset store to defaults before each test
    useVoicePreferencesStore.getState().reset();
  });

  afterEach(() => {
    useVoicePreferencesStore.getState().reset();
  });

  // ------------------------------------------------------------------
  // Initial state
  // ------------------------------------------------------------------
  describe('initial state', () => {
    it('matches DEFAULT_VOICE_PREFERENCES', () => {
      const state = useVoicePreferencesStore.getState();
      expect(state.ttsEnabled).toBe(DEFAULT_VOICE_PREFERENCES.ttsEnabled);
      expect(state.autoSend).toBe(DEFAULT_VOICE_PREFERENCES.autoSend);
      expect(state.language).toBe(DEFAULT_VOICE_PREFERENCES.language);
      expect(state.voiceURI).toBe(DEFAULT_VOICE_PREFERENCES.voiceURI);
      expect(state.rate).toBe(DEFAULT_VOICE_PREFERENCES.rate);
    });

    it('has ttsEnabled=false by default', () => {
      expect(useVoicePreferencesStore.getState().ttsEnabled).toBe(false);
    });

    it('has autoSend=true by default', () => {
      expect(useVoicePreferencesStore.getState().autoSend).toBe(true);
    });

    it('has language="it-IT" by default', () => {
      expect(useVoicePreferencesStore.getState().language).toBe('it-IT');
    });

    it('has voiceURI=null by default', () => {
      expect(useVoicePreferencesStore.getState().voiceURI).toBeNull();
    });

    it('has rate=1.0 by default', () => {
      expect(useVoicePreferencesStore.getState().rate).toBe(1.0);
    });
  });

  // ------------------------------------------------------------------
  // Setters
  // ------------------------------------------------------------------
  describe('setters', () => {
    it('setTtsEnabled updates ttsEnabled', () => {
      useVoicePreferencesStore.getState().setTtsEnabled(true);
      expect(useVoicePreferencesStore.getState().ttsEnabled).toBe(true);

      useVoicePreferencesStore.getState().setTtsEnabled(false);
      expect(useVoicePreferencesStore.getState().ttsEnabled).toBe(false);
    });

    it('setAutoSend updates autoSend', () => {
      useVoicePreferencesStore.getState().setAutoSend(false);
      expect(useVoicePreferencesStore.getState().autoSend).toBe(false);
    });

    it('setLanguage updates language', () => {
      useVoicePreferencesStore.getState().setLanguage('en-US');
      expect(useVoicePreferencesStore.getState().language).toBe('en-US');
    });

    it('setVoiceURI updates voiceURI', () => {
      useVoicePreferencesStore.getState().setVoiceURI('some-voice-uri');
      expect(useVoicePreferencesStore.getState().voiceURI).toBe('some-voice-uri');
    });

    it('setVoiceURI accepts null', () => {
      useVoicePreferencesStore.getState().setVoiceURI('test');
      useVoicePreferencesStore.getState().setVoiceURI(null);
      expect(useVoicePreferencesStore.getState().voiceURI).toBeNull();
    });

    it('setRate updates rate', () => {
      useVoicePreferencesStore.getState().setRate(1.5);
      expect(useVoicePreferencesStore.getState().rate).toBe(1.5);
    });
  });

  // ------------------------------------------------------------------
  // Rate clamping
  // ------------------------------------------------------------------
  describe('setRate clamping', () => {
    it('clamps rate to minimum 0.5', () => {
      useVoicePreferencesStore.getState().setRate(0.1);
      expect(useVoicePreferencesStore.getState().rate).toBe(0.5);
    });

    it('clamps rate to maximum 2.0', () => {
      useVoicePreferencesStore.getState().setRate(5.0);
      expect(useVoicePreferencesStore.getState().rate).toBe(2.0);
    });

    it('allows rate at lower boundary (0.5)', () => {
      useVoicePreferencesStore.getState().setRate(0.5);
      expect(useVoicePreferencesStore.getState().rate).toBe(0.5);
    });

    it('allows rate at upper boundary (2.0)', () => {
      useVoicePreferencesStore.getState().setRate(2.0);
      expect(useVoicePreferencesStore.getState().rate).toBe(2.0);
    });

    it('clamps negative values to 0.5', () => {
      useVoicePreferencesStore.getState().setRate(-1);
      expect(useVoicePreferencesStore.getState().rate).toBe(0.5);
    });

    it('allows values within range (1.25)', () => {
      useVoicePreferencesStore.getState().setRate(1.25);
      expect(useVoicePreferencesStore.getState().rate).toBe(1.25);
    });
  });

  // ------------------------------------------------------------------
  // Reset
  // ------------------------------------------------------------------
  describe('reset', () => {
    it('restores all fields to defaults', () => {
      const store = useVoicePreferencesStore.getState();

      // Modify every field
      store.setTtsEnabled(true);
      store.setAutoSend(false);
      store.setLanguage('en-US');
      store.setVoiceURI('custom-voice');
      store.setRate(1.8);

      // Verify modified
      const modified = useVoicePreferencesStore.getState();
      expect(modified.ttsEnabled).toBe(true);
      expect(modified.autoSend).toBe(false);
      expect(modified.language).toBe('en-US');
      expect(modified.voiceURI).toBe('custom-voice');
      expect(modified.rate).toBe(1.8);

      // Reset
      useVoicePreferencesStore.getState().reset();

      // Verify restored
      const reset = useVoicePreferencesStore.getState();
      expect(reset.ttsEnabled).toBe(DEFAULT_VOICE_PREFERENCES.ttsEnabled);
      expect(reset.autoSend).toBe(DEFAULT_VOICE_PREFERENCES.autoSend);
      expect(reset.language).toBe(DEFAULT_VOICE_PREFERENCES.language);
      expect(reset.voiceURI).toBe(DEFAULT_VOICE_PREFERENCES.voiceURI);
      expect(reset.rate).toBe(DEFAULT_VOICE_PREFERENCES.rate);
    });
  });
});

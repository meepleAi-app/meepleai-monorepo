'use client';

/**
 * Voice Preferences Store
 *
 * Persists user voice preferences (TTS, STT, language, voice selection)
 * to localStorage with the standard MeepleAI middleware stack:
 * devtools + persist + immer.
 */

import { create } from 'zustand';
import { createJSONStorage, devtools, persist } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

import type { VoicePreferences } from '@/lib/voice/types';
import { DEFAULT_VOICE_PREFERENCES } from '@/lib/voice/types';

// ============================================================================
// Types
// ============================================================================

export interface VoicePreferencesState extends VoicePreferences {
  setTtsEnabled: (enabled: boolean) => void;
  setAutoSend: (autoSend: boolean) => void;
  setLanguage: (lang: string) => void;
  setVoiceURI: (uri: string | null) => void;
  setRate: (rate: number) => void;
  reset: () => void;
}

// ============================================================================
// Store Creation
// ============================================================================

export const useVoicePreferencesStore = create<VoicePreferencesState>()(
  devtools(
    persist(
      immer(set => ({
        // Initial state
        ...DEFAULT_VOICE_PREFERENCES,

        setTtsEnabled: (enabled: boolean) => {
          set(state => {
            state.ttsEnabled = enabled;
          });
        },

        setAutoSend: (autoSend: boolean) => {
          set(state => {
            state.autoSend = autoSend;
          });
        },

        setLanguage: (lang: string) => {
          set(state => {
            state.language = lang;
          });
        },

        setVoiceURI: (uri: string | null) => {
          set(state => {
            state.voiceURI = uri;
          });
        },

        setRate: (rate: number) => {
          set(state => {
            state.rate = Math.max(0.5, Math.min(2.0, rate));
          });
        },

        reset: () => {
          set(state => {
            state.ttsEnabled = DEFAULT_VOICE_PREFERENCES.ttsEnabled;
            state.autoSend = DEFAULT_VOICE_PREFERENCES.autoSend;
            state.language = DEFAULT_VOICE_PREFERENCES.language;
            state.voiceURI = DEFAULT_VOICE_PREFERENCES.voiceURI;
            state.rate = DEFAULT_VOICE_PREFERENCES.rate;
          });
        },
      })),
      {
        name: 'meepleai-voice-prefs',
        storage: createJSONStorage(() =>
          typeof window !== 'undefined'
            ? localStorage
            : {
                getItem: () => null,
                setItem: () => {},
                removeItem: () => {},
              }
        ),
        // Only persist preference values, not action functions
        partialize: state => ({
          ttsEnabled: state.ttsEnabled,
          autoSend: state.autoSend,
          language: state.language,
          voiceURI: state.voiceURI,
          rate: state.rate,
        }),
      }
    ),
    {
      name: 'VoicePreferencesStore',
    }
  )
);

// ============================================================================
// Selectors
// ============================================================================

export const selectTtsEnabled = (state: VoicePreferencesState) => state.ttsEnabled;
export const selectAutoSend = (state: VoicePreferencesState) => state.autoSend;
export const selectLanguage = (state: VoicePreferencesState) => state.language;
export const selectVoiceURI = (state: VoicePreferencesState) => state.voiceURI;
export const selectRate = (state: VoicePreferencesState) => state.rate;

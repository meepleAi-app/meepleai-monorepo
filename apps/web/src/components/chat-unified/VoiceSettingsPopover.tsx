'use client';

import { useCallback, useMemo } from 'react';

import { Settings2 } from 'lucide-react';

import { Switch } from '@/components/ui/forms/switch';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/overlays/popover';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Slider } from '@/components/ui/slider';
import { cn } from '@/lib/utils';
import type { VoicePreferences } from '@/lib/voice/types';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface VoiceSettingsPopoverProps {
  preferences: VoicePreferences;
  onPreferencesChange: (prefs: Partial<VoicePreferences>) => void;
  availableVoices: SpeechSynthesisVoice[];
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const SUPPORTED_LANGUAGES = [
  { value: 'it-IT', label: 'Italiano' },
  { value: 'en-US', label: 'English (US)' },
] as const;

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function VoiceSettingsPopover({
  preferences,
  onPreferencesChange,
  availableVoices,
}: VoiceSettingsPopoverProps) {
  const filteredVoices = useMemo(
    () => availableVoices.filter(v => v.lang.startsWith(preferences.language.split('-')[0])),
    [availableVoices, preferences.language]
  );

  const handleTtsToggle = useCallback(
    (checked: boolean) => {
      onPreferencesChange({ ttsEnabled: checked });
    },
    [onPreferencesChange]
  );

  const handleAutoSendToggle = useCallback(
    (checked: boolean) => {
      onPreferencesChange({ autoSend: checked });
    },
    [onPreferencesChange]
  );

  const handleLanguageChange = useCallback(
    (value: string) => {
      // Reset voice when language changes
      onPreferencesChange({ language: value, voiceURI: null });
    },
    [onPreferencesChange]
  );

  const handleVoiceChange = useCallback(
    (value: string) => {
      onPreferencesChange({ voiceURI: value });
    },
    [onPreferencesChange]
  );

  const handleRateChange = useCallback(
    (value: number[]) => {
      onPreferencesChange({ rate: value[0] });
    },
    [onPreferencesChange]
  );

  return (
    <Popover>
      <PopoverTrigger asChild>
        <button
          type="button"
          aria-label="Voice settings"
          data-testid="voice-settings-trigger"
          className={cn(
            'inline-flex items-center justify-center rounded-md p-1.5',
            'text-muted-foreground hover:text-foreground',
            'hover:bg-muted/50 transition-colors',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
          )}
        >
          <Settings2 className="h-4 w-4" aria-hidden="true" />
        </button>
      </PopoverTrigger>
      <PopoverContent
        align="end"
        sideOffset={8}
        className="w-[280px] p-4"
        data-testid="voice-settings-popover"
      >
        <div className="space-y-4">
          <h4 className="text-sm font-medium font-quicksand">Voice Settings</h4>

          {/* Auto-read answers */}
          <SettingsRow label="Auto-read answers" htmlFor="voice-tts-toggle">
            <Switch
              id="voice-tts-toggle"
              checked={preferences.ttsEnabled}
              onCheckedChange={handleTtsToggle}
              data-testid="voice-settings-tts"
              aria-label="Toggle auto-read answers"
            />
          </SettingsRow>

          {/* Auto-send after voice */}
          <SettingsRow label="Auto-send after voice" htmlFor="voice-autosend-toggle">
            <Switch
              id="voice-autosend-toggle"
              checked={preferences.autoSend}
              onCheckedChange={handleAutoSendToggle}
              data-testid="voice-settings-autosend"
              aria-label="Toggle auto-send after voice input"
            />
          </SettingsRow>

          {/* Language */}
          <div className="space-y-1.5">
            <label htmlFor="voice-language-select" className="text-xs text-muted-foreground">
              Language
            </label>
            <Select value={preferences.language} onValueChange={handleLanguageChange}>
              <SelectTrigger
                id="voice-language-select"
                data-testid="voice-settings-language"
                className="h-8 text-xs"
              >
                <SelectValue placeholder="Select language" />
              </SelectTrigger>
              <SelectContent>
                {SUPPORTED_LANGUAGES.map(lang => (
                  <SelectItem key={lang.value} value={lang.value}>
                    {lang.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Voice (only when TTS enabled and voices available) */}
          {preferences.ttsEnabled && filteredVoices.length > 0 && (
            <div className="space-y-1.5">
              <label htmlFor="voice-voice-select" className="text-xs text-muted-foreground">
                Voice
              </label>
              <Select value={preferences.voiceURI ?? ''} onValueChange={handleVoiceChange}>
                <SelectTrigger
                  id="voice-voice-select"
                  data-testid="voice-settings-voice"
                  className="h-8 text-xs"
                >
                  <SelectValue placeholder="Default voice" />
                </SelectTrigger>
                <SelectContent>
                  {filteredVoices.map(voice => (
                    <SelectItem key={voice.voiceURI} value={voice.voiceURI}>
                      {voice.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}

          {/* Speed slider (only when TTS enabled) */}
          {preferences.ttsEnabled && (
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <label htmlFor="voice-speed-slider" className="text-xs text-muted-foreground">
                  Speed
                </label>
                <span className="text-xs text-muted-foreground tabular-nums" aria-live="polite">
                  {preferences.rate.toFixed(1)}x
                </span>
              </div>
              <Slider
                id="voice-speed-slider"
                min={0.5}
                max={2.0}
                step={0.1}
                value={[preferences.rate]}
                onValueChange={handleRateChange}
                data-testid="voice-settings-speed"
                aria-label="Speech rate"
              />
            </div>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}

// ---------------------------------------------------------------------------
// Internal
// ---------------------------------------------------------------------------

function SettingsRow({
  label,
  htmlFor,
  children,
}: {
  label: string;
  htmlFor: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex items-center justify-between">
      <label htmlFor={htmlFor} className="text-xs text-muted-foreground">
        {label}
      </label>
      {children}
    </div>
  );
}

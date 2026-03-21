/**
 * ChatInputArea - Input section for ChatThreadView
 *
 * Contains:
 * - RAG enhancements badge
 * - Message textarea
 * - Voice mic button
 * - Voice settings popover
 * - Send button
 */

'use client';

import React from 'react';

import { cn } from '@/lib/utils';
import type { VoiceRecognitionState } from '@/lib/voice/types';
import type { VoicePreferencesState } from '@/store/voice/store';

import { RagEnhancementsBadge } from './RagEnhancementsBadge';
import { VoiceMicButton } from './VoiceMicButton';
import { VoiceSettingsPopover } from './VoiceSettingsPopover';

// ============================================================================
// Types
// ============================================================================

export interface ChatInputAreaProps {
  inputValue: string;
  onInputChange: (value: string) => void;
  onSend: () => void;
  onKeyDown: (e: React.KeyboardEvent) => void;
  isSending: boolean;
  isStreaming: boolean;
  /** Voice input */
  voiceState: VoiceRecognitionState;
  onVoiceTap: () => void;
  isVoiceSupported: boolean;
  voicePrefs: VoicePreferencesState;
  availableVoices: SpeechSynthesisVoice[];
}

// ============================================================================
// Component
// ============================================================================

export function ChatInputArea({
  inputValue,
  onInputChange,
  onSend,
  onKeyDown,
  isSending,
  isStreaming,
  voiceState,
  onVoiceTap,
  isVoiceSupported,
  voicePrefs,
  availableVoices,
}: ChatInputAreaProps) {
  return (
    <div
      className="border-t border-border/50 dark:border-border/30 p-4 bg-background/95 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none"
      data-testid="chat-input-area"
    >
      {/* RAG Enhancement Cost Indicator */}
      <div className="max-w-3xl mx-auto mb-2">
        <RagEnhancementsBadge />
      </div>

      <div className="flex items-end gap-2 max-w-3xl mx-auto">
        <textarea
          value={inputValue}
          onChange={e => onInputChange(e.target.value)}
          onKeyDown={onKeyDown}
          placeholder="Scrivi un messaggio..."
          rows={1}
          className={cn(
            'flex-1 resize-none rounded-xl border border-border/50 px-4 py-3',
            'bg-white/70 dark:bg-card/70 backdrop-blur-md text-sm font-nunito',
            'placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-amber-500/40',
            'max-h-32'
          )}
          disabled={isSending || isStreaming}
          data-testid="message-input"
        />
        <VoiceMicButton
          state={voiceState}
          onTap={onVoiceTap}
          disabled={!isVoiceSupported || isSending || isStreaming}
          size="md"
        />
        {isVoiceSupported && (
          <VoiceSettingsPopover
            preferences={voicePrefs}
            onPreferencesChange={updates => {
              if (updates.ttsEnabled !== undefined) voicePrefs.setTtsEnabled(updates.ttsEnabled);
              if (updates.autoSend !== undefined) voicePrefs.setAutoSend(updates.autoSend);
              if (updates.language !== undefined) voicePrefs.setLanguage(updates.language);
              if (updates.voiceURI !== undefined) voicePrefs.setVoiceURI(updates.voiceURI);
              if (updates.rate !== undefined) voicePrefs.setRate(updates.rate);
            }}
            availableVoices={availableVoices}
          />
        )}
        <button
          onClick={onSend}
          disabled={!inputValue.trim() || isSending || isStreaming}
          className={cn(
            'p-3 rounded-xl transition-all duration-200 flex-shrink-0',
            inputValue.trim() && !isSending && !isStreaming
              ? 'bg-amber-500 hover:bg-amber-600 text-white cursor-pointer'
              : 'bg-muted text-muted-foreground cursor-not-allowed'
          )}
          aria-label="Invia messaggio"
          data-testid="send-btn"
        >
          {isSending ? (
            <div className="h-5 w-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
          ) : (
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8"
              />
            </svg>
          )}
        </button>
      </div>
      <p className="text-[10px] text-muted-foreground/60 text-center mt-1 max-w-3xl mx-auto font-nunito">
        <kbd className="px-1 py-0.5 rounded border border-border/30 text-[9px]">Enter</kbd> invia
        {' · '}
        <kbd className="px-1 py-0.5 rounded border border-border/30 text-[9px]">
          Shift+Enter
        </kbd>{' '}
        nuova riga
        {' · '}
        <kbd className="px-1 py-0.5 rounded border border-border/30 text-[9px]">Esc</kbd> cancella
      </p>
    </div>
  );
}

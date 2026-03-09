'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

import { Loader2, Send, X } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { VoiceRecognitionState } from '@/lib/voice/types';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface VoiceTranscriptOverlayProps {
  interimText: string;
  finalText: string;
  state: VoiceRecognitionState;
  onEdit: (text: string) => void;
  onSend: () => void;
  onCancel: () => void;
  autoSend: boolean;
}

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

function ListeningIndicator({ text }: { text: string }) {
  return (
    <p className="text-sm text-muted-foreground italic" aria-live="polite">
      {text || 'Listening'}
      <span aria-hidden="true" className="inline-block w-6 motion-safe:animate-pulse">
        ...
      </span>
    </p>
  );
}

function ProcessingIndicator() {
  return (
    <div className="flex items-center gap-2 text-sm text-muted-foreground">
      <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
      <span>Processing...</span>
    </div>
  );
}

function AutoSendFlash() {
  return (
    <p className="text-sm text-amber-700 dark:text-amber-300 font-medium animate-pulse">
      Sending...
    </p>
  );
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function VoiceTranscriptOverlay({
  interimText,
  finalText,
  state,
  onEdit,
  onSend,
  onCancel,
  autoSend,
}: VoiceTranscriptOverlayProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const [editableText, setEditableText] = useState(finalText);
  const [autoSendTriggered, setAutoSendTriggered] = useState(false);

  // Sync editable text when finalText arrives
  useEffect(() => {
    if (finalText) {
      setEditableText(finalText);
    }
  }, [finalText]);

  // Auto-send logic: brief flash then send
  useEffect(() => {
    if (finalText && autoSend && !autoSendTriggered) {
      setAutoSendTriggered(true);
      const timer = setTimeout(() => {
        onSend();
        setAutoSendTriggered(false);
      }, 600);
      return () => clearTimeout(timer);
    }
  }, [finalText, autoSend, autoSendTriggered, onSend]);

  // Reset auto-send flag when state returns to idle
  useEffect(() => {
    if (state === 'idle') {
      setAutoSendTriggered(false);
    }
  }, [state]);

  // Focus textarea when final text appears in manual mode
  useEffect(() => {
    if (finalText && !autoSend && textareaRef.current) {
      textareaRef.current.focus();
    }
  }, [finalText, autoSend]);

  const handleTextChange = useCallback(
    (e: React.ChangeEvent<HTMLTextAreaElement>) => {
      const value = e.target.value;
      setEditableText(value);
      onEdit(value);
    },
    [onEdit]
  );

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
      // Ctrl+Enter or Cmd+Enter to send
      if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
        e.preventDefault();
        onSend();
      }
      // Escape to cancel
      if (e.key === 'Escape') {
        e.preventDefault();
        onCancel();
      }
    },
    [onSend, onCancel]
  );

  // Don't render when idle
  if (state === 'idle' && !finalText) {
    return null;
  }

  const showListening = state === 'listening';
  const showProcessing = state === 'processing';
  const showFinalManual = finalText && !autoSend && !autoSendTriggered;
  const showAutoSendFlash = finalText && autoSend && autoSendTriggered;

  return (
    <div
      role="status"
      aria-live="polite"
      aria-label="Voice transcript"
      data-testid="voice-transcript-overlay"
      className={cn(
        'bg-amber-50/80 dark:bg-amber-950/40 backdrop-blur-md',
        'border border-amber-200/50 dark:border-amber-800/50 rounded-xl',
        'px-4 py-3',
        'animate-fade-in'
      )}
    >
      {showListening && <ListeningIndicator text={interimText} />}
      {showProcessing && <ProcessingIndicator />}
      {showAutoSendFlash && <AutoSendFlash />}

      {showFinalManual && (
        <div className="space-y-2">
          <textarea
            ref={textareaRef}
            value={editableText}
            onChange={handleTextChange}
            onKeyDown={handleKeyDown}
            rows={2}
            aria-label="Edit transcribed text"
            data-testid="voice-transcript-textarea"
            className={cn(
              'w-full resize-none rounded-lg border border-amber-200/50 dark:border-amber-800/50',
              'bg-white/60 dark:bg-white/5 backdrop-blur-sm',
              'px-3 py-2 text-sm text-foreground',
              'focus:outline-none focus:ring-2 focus:ring-amber-400/50',
              'placeholder:text-muted-foreground'
            )}
          />
          <div className="flex items-center justify-end gap-2">
            <button
              type="button"
              onClick={onCancel}
              aria-label="Cancel voice input"
              data-testid="voice-transcript-cancel"
              className={cn(
                'inline-flex items-center gap-1 px-3 py-1.5 rounded-lg text-sm',
                'text-muted-foreground hover:text-foreground',
                'hover:bg-muted/50 transition-colors'
              )}
            >
              <X className="h-3.5 w-3.5" aria-hidden="true" />
              Cancel
            </button>
            <button
              type="button"
              onClick={onSend}
              aria-label="Send voice message"
              data-testid="voice-transcript-send"
              className={cn(
                'inline-flex items-center gap-1 px-3 py-1.5 rounded-lg text-sm font-medium',
                'bg-amber-500 text-white hover:bg-amber-600',
                'transition-colors'
              )}
            >
              <Send className="h-3.5 w-3.5" aria-hidden="true" />
              Send
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

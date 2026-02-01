/**
 * ChatInput - Chat input with auto-resize and SSE integration (Issue #3245)
 *
 * Features:
 * - Auto-resize textarea (max 5 rows)
 * - Character limit (1000) with counter
 * - Send button (disabled when empty or streaming)
 * - Attachment button (disabled for MVP)
 * - Enter sends, Shift+Enter newline
 * - Loading spinner during streaming
 * - SSE error handling
 */

import React, { useState, useRef, useEffect, useCallback, KeyboardEvent } from 'react';

import { Send, Paperclip, Loader2, AlertCircle, RefreshCw } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Textarea } from '@/components/ui/primitives/textarea';
import { cn } from '@/lib/utils';

interface ChatInputProps {
  /** Whether SSE streaming is active */
  isStreaming?: boolean;

  /** Send message callback */
  onSendMessage: (message: string) => void;

  /** SSE error state */
  error?: string | null;

  /** Retry callback for failed SSE connections */
  onRetry?: () => void;

  /** Custom class name */
  className?: string;

  /** Placeholder text */
  placeholder?: string;
}

const MAX_CHARS = 1000;
const MAX_ROWS = 5;
const LINE_HEIGHT = 24; // Approximate line height in pixels

export function ChatInput({
  isStreaming = false,
  onSendMessage,
  error = null,
  onRetry,
  className,
  placeholder = 'Scrivi un messaggio...',
}: ChatInputProps) {
  const [input, setInput] = useState('');
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  /**
   * Auto-resize textarea (max 5 rows)
   */
  const autoResize = useCallback(() => {
    const textarea = textareaRef.current;
    if (!textarea) return;

    // Reset height to auto to get correct scrollHeight
    textarea.style.height = 'auto';

    // Calculate max height (5 rows)
    const maxHeight = LINE_HEIGHT * MAX_ROWS;
    const newHeight = Math.min(textarea.scrollHeight, maxHeight);

    textarea.style.height = `${newHeight}px`;
  }, []);

  /**
   * Auto-resize on input change
   */
  useEffect(() => {
    autoResize();
  }, [input, autoResize]);

  /**
   * Handle input change with character limit
   */
  const handleInputChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const value = e.target.value;

    // Enforce character limit
    if (value.length <= MAX_CHARS) {
      setInput(value);
    }
  };

  /**
   * Handle send message
   */
  const handleSend = useCallback(() => {
    const trimmed = input.trim();

    if (!trimmed || isStreaming) return;

    // Send message
    onSendMessage(trimmed);

    // Clear input
    setInput('');

    // Reset textarea height
    setTimeout(() => {
      if (textareaRef.current) {
        textareaRef.current.style.height = 'auto';
      }
    }, 0);
  }, [input, isStreaming, onSendMessage]);

  /**
   * Handle keyboard events
   * - Enter: Send message
   * - Shift+Enter: New line
   */
  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  // Check if send button should be disabled
  const isSendDisabled = !input.trim() || isStreaming;

  // Character counter display
  const charCount = input.length;
  const isNearLimit = charCount > MAX_CHARS * 0.9; // Show warning at 90%

  return (
    <div className={cn('flex flex-col gap-2 border-t border-border bg-card p-4', className)}>
      {/* Input container */}
      <div className="flex items-end gap-2">
        {/* Textarea */}
        <div className="relative flex-1">
          <Textarea
            ref={textareaRef}
            value={input}
            onChange={handleInputChange}
            onKeyDown={handleKeyDown}
            placeholder={placeholder}
            disabled={isStreaming}
            className={cn(
              'resize-none overflow-y-auto pr-16',
              'min-h-[60px]',
              isStreaming && 'opacity-60 cursor-not-allowed'
            )}
            aria-label="Message input"
            aria-describedby="char-counter"
          />

          {/* Character counter (absolute positioned inside textarea) */}
          <div
            id="char-counter"
            className={cn(
              'absolute bottom-2 right-3 text-xs',
              isNearLimit ? 'text-red-500 font-medium' : 'text-muted-foreground'
            )}
            aria-live="polite"
          >
            {charCount}/{MAX_CHARS}
          </div>
        </div>

        {/* Attachment button (disabled for MVP) */}
        <Button
          type="button"
          variant="outline"
          size="icon"
          disabled={true}
          title="Allega file (disponibile in futuro)"
          aria-label="Allega file (disabilitato)"
        >
          <Paperclip className="h-4 w-4" />
        </Button>

        {/* Send button */}
        <Button
          type="button"
          onClick={handleSend}
          disabled={isSendDisabled}
          aria-label={isStreaming ? 'Invio in corso...' : 'Invia messaggio'}
          className="min-w-[44px]"
        >
          {isStreaming ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <Send className="h-4 w-4" />
          )}
        </Button>
      </div>

      {/* SSE Error display */}
      {error && (
        <div
          className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800 dark:border-red-900/50 dark:bg-red-950/50 dark:text-red-200"
          role="alert"
        >
          <AlertCircle className="h-4 w-4 flex-shrink-0" />
          <span className="flex-1">{error}</span>
          {onRetry && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={onRetry}
              className="ml-2 h-8 gap-1"
            >
              <RefreshCw className="h-3 w-3" />
              Riprova
            </Button>
          )}
        </div>
      )}
    </div>
  );
}

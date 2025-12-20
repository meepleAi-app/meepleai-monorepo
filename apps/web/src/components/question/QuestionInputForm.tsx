/**
 * QuestionInputForm - Standalone question input component
 *
 * A reusable form component for submitting questions about board games.
 * Designed for the Playful Boardroom design system (Opzione A).
 *
 * Features:
 * - Mobile-first design with touch-friendly targets (44x44px min)
 * - Optional attachment button
 * - Optional response mode toggle (Fast/Complete)
 * - Loading and disabled states
 * - WCAG 2.1 AA accessible
 * - Italian-first placeholder text
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md
 * @issue BGAI-061
 */

import React, { FormEvent, useCallback, useRef, KeyboardEvent } from 'react';

import { Send, Paperclip, Loader2, Zap, Target } from 'lucide-react';
import { useTranslations } from 'next-intl';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';

export type ResponseMode = 'fast' | 'complete';

export interface QuestionInputFormProps {
  /** Current input value */
  value: string;
  /** Callback when input value changes */
  onChange: (value: string) => void;
  /** Callback when form is submitted */
  onSubmit: (question: string) => void;
  /** Input placeholder text */
  placeholder?: string;
  /** Disable all interactions */
  disabled?: boolean;
  /** Show loading state on submit button */
  isLoading?: boolean;
  /** Show attachment button */
  showAttachment?: boolean;
  /** Callback when attachment button is clicked */
  onAttach?: () => void;
  /** Current response mode */
  responseMode?: ResponseMode;
  /** Callback when response mode changes */
  onResponseModeChange?: (mode: ResponseMode) => void;
  /** Show response mode toggle */
  showResponseModeToggle?: boolean;
  /** Maximum character length */
  maxLength?: number;
  /** Additional className for the container */
  className?: string;
  /** Auto-focus input on mount */
  autoFocus?: boolean;
}

/**
 * QuestionInputForm component
 *
 * @example
 * ```tsx
 * <QuestionInputForm
 *   value={question}
 *   onChange={setQuestion}
 *   onSubmit={handleAsk}
 *   placeholder="Fai una domanda sul gioco..."
 *   isLoading={isAsking}
 * />
 * ```
 */
export function QuestionInputForm({
  value,
  onChange,
  onSubmit,
  placeholder,
  disabled = false,
  isLoading = false,
  showAttachment = false,
  onAttach,
  responseMode = 'fast',
  onResponseModeChange,
  showResponseModeToggle = false,
  maxLength = 1000,
  className,
  autoFocus = false,
}: QuestionInputFormProps) {
  const t = useTranslations('questionInput');
  const inputRef = useRef<HTMLInputElement>(null);

  // Use provided placeholder or i18n default
  const effectivePlaceholder = placeholder || t('placeholder');

  const isSubmitDisabled = disabled || isLoading || !value.trim();

  const handleSubmit = useCallback(
    (e: FormEvent) => {
      e.preventDefault();
      if (isSubmitDisabled) return;

      const trimmedValue = value.trim();
      if (trimmedValue) {
        onSubmit(trimmedValue);
      }
    },
    [value, onSubmit, isSubmitDisabled]
  );

  const handleKeyDown = useCallback(
    (e: KeyboardEvent<HTMLInputElement>) => {
      // Submit on Enter (without Shift)
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        if (!isSubmitDisabled) {
          const trimmedValue = value.trim();
          if (trimmedValue) {
            onSubmit(trimmedValue);
          }
        }
      }
    },
    [value, onSubmit, isSubmitDisabled]
  );

  const handleResponseModeChange = useCallback(
    (mode: ResponseMode) => {
      onResponseModeChange?.(mode);
    },
    [onResponseModeChange]
  );

  const handleAttachClick = useCallback(() => {
    onAttach?.();
  }, [onAttach]);

  return (
    <div
      className={cn('flex flex-col gap-3 p-4 bg-card border-t border-border shadow-lg', className)}
      role="search"
      aria-label={t('searchLabel')}
    >
      {/* Response Mode Toggle */}
      {showResponseModeToggle && (
        <div className="flex gap-2" role="radiogroup" aria-label={t('responseModeLabel')}>
          <Button
            type="button"
            variant={responseMode === 'fast' ? 'default' : 'outline'}
            size="sm"
            onClick={() => handleResponseModeChange('fast')}
            disabled={disabled || isLoading}
            className={cn(
              'flex-1 gap-1.5 transition-all duration-200',
              responseMode === 'fast' && 'bg-primary text-primary-foreground hover:bg-primary/90'
            )}
            aria-pressed={responseMode === 'fast'}
            aria-label={t('fastResponse')}
          >
            <Zap className="h-4 w-4" aria-hidden="true" />
            <span>{t('fastResponse')}</span>
          </Button>
          <Button
            type="button"
            variant={responseMode === 'complete' ? 'default' : 'outline'}
            size="sm"
            onClick={() => handleResponseModeChange('complete')}
            disabled={disabled || isLoading}
            className={cn(
              'flex-1 gap-1.5 transition-all duration-200',
              responseMode === 'complete' &&
                'bg-secondary text-secondary-foreground hover:bg-secondary/90'
            )}
            aria-pressed={responseMode === 'complete'}
            aria-label={t('completeResponse')}
          >
            <Target className="h-4 w-4" aria-hidden="true" />
            <span>{t('completeResponse')}</span>
          </Button>
        </div>
      )}

      {/* Input Form */}
      <form onSubmit={handleSubmit} className="flex gap-2 items-center">
        {/* Attachment Button */}
        {showAttachment && (
          <Button
            type="button"
            variant="ghost"
            size="icon"
            onClick={handleAttachClick}
            disabled={disabled || isLoading}
            className="shrink-0 h-10 w-10 text-muted-foreground hover:text-foreground transition-colors"
            aria-label={t('attachFile')}
          >
            <Paperclip className="h-5 w-5" aria-hidden="true" />
          </Button>
        )}

        {/* Text Input */}
        <div className="relative flex-1">
          <label htmlFor="question-input" className="sr-only">
            {effectivePlaceholder}
          </label>
          <Input
            ref={inputRef}
            id="question-input"
            type="text"
            value={value}
            onChange={e => onChange(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder={effectivePlaceholder}
            disabled={disabled || isLoading}
            maxLength={maxLength}
            autoFocus={autoFocus}
            className={cn(
              'h-11 pr-12 rounded-lg border-input bg-background',
              'focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-0',
              'placeholder:text-muted-foreground',
              'transition-shadow duration-200'
            )}
            aria-describedby={maxLength ? 'char-count' : undefined}
          />
          {/* Character Counter */}
          {maxLength && (
            <span id="char-count" className="sr-only" aria-live="polite" aria-atomic="true">
              {t('charCount', { count: value.length, max: maxLength })}
            </span>
          )}
        </div>

        {/* Submit Button */}
        <Button
          type="submit"
          variant="default"
          size="icon"
          disabled={isSubmitDisabled}
          className={cn(
            'shrink-0 h-11 w-11 rounded-lg',
            'bg-primary text-primary-foreground',
            'hover:bg-primary/90 hover:shadow-md',
            'active:scale-95',
            'transition-all duration-200',
            'disabled:opacity-50 disabled:cursor-not-allowed'
          )}
          aria-label={isLoading ? t('sending') : t('sendQuestion')}
        >
          {isLoading ? (
            <Loader2 className="h-5 w-5 animate-spin" aria-hidden="true" />
          ) : (
            <Send className="h-5 w-5" aria-hidden="true" />
          )}
        </Button>
      </form>
    </div>
  );
}

export default QuestionInputForm;

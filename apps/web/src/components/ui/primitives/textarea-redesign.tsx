/**
 * Textarea Component - Redesigned
 * Multi-line input with auto-resize option
 * Matches Input component styling
 */

import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const textareaVariants = cva(
  'flex min-h-[80px] w-full transition-all duration-200 placeholder:text-[var(--text-tertiary)] focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-50 resize-none',
  {
    variants: {
      variant: {
        default:
          'border border-[var(--border-primary)] bg-[var(--bg-elevated)] text-[var(--text-primary)] focus:border-[var(--color-primary-500)] focus:ring-2 focus:ring-[var(--color-primary-500)]/20',

        filled:
          'border-0 bg-[var(--bg-secondary)] text-[var(--text-primary)] focus:bg-[var(--bg-tertiary)] focus:ring-2 focus:ring-[var(--color-primary-500)]/20',
      },

      size: {
        sm: 'px-3 py-2 text-[var(--font-size-sm)] rounded-[var(--radius-md)]',
        md: 'px-4 py-3 text-[var(--font-size-base)] rounded-[var(--radius-lg)]',
        lg: 'px-5 py-4 text-[var(--font-size-lg)] rounded-[var(--radius-lg)]',
      },

      state: {
        default: '',
        error:
          'border-[var(--color-red)] focus:border-[var(--color-red)] focus:ring-[var(--color-red)]/20',
        success:
          'border-[var(--color-green)] focus:border-[var(--color-green)] focus:ring-[var(--color-green)]/20',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'md',
      state: 'default',
    },
  }
);

export interface TextareaProps
  extends React.TextareaHTMLAttributes<HTMLTextAreaElement>, VariantProps<typeof textareaVariants> {
  error?: string;
  helperText?: string;
  autoResize?: boolean;
  maxHeight?: number;
}

const Textarea = React.forwardRef<HTMLTextAreaElement, TextareaProps>(
  (
    {
      className,
      variant,
      size,
      state,
      error,
      helperText,
      autoResize = false,
      maxHeight = 400,
      onChange,
      ...props
    },
    ref
  ) => {
    const textareaRef = React.useRef<HTMLTextAreaElement | null>(null);
    const hasError = !!error;
    const finalState = hasError ? 'error' : state;

    // Auto-resize logic
    React.useEffect(() => {
      if (autoResize && textareaRef.current) {
        const textarea = textareaRef.current;
        textarea.style.height = 'auto';
        const newHeight = Math.min(textarea.scrollHeight, maxHeight);
        textarea.style.height = `${newHeight}px`;
      }
    }, [autoResize, maxHeight, props.value]);

    const handleChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
      if (autoResize && textareaRef.current) {
        textareaRef.current.style.height = 'auto';
        const newHeight = Math.min(textareaRef.current.scrollHeight, maxHeight);
        textareaRef.current.style.height = `${newHeight}px`;
      }
      onChange?.(e);
    };

    return (
      <div className="textarea-wrapper w-full">
        <textarea
          className={cn(
            textareaVariants({ variant, size, state: finalState, className }),
            autoResize && 'overflow-y-auto'
          )}
          ref={node => {
            textareaRef.current = node;
            if (typeof ref === 'function') {
              ref(node);
            } else if (ref) {
              ref.current = node;
            }
          }}
          onChange={handleChange}
          {...props}
        />

        {(error || helperText) && (
          <p
            className={cn(
              'mt-1.5 text-[var(--font-size-sm)]',
              hasError ? 'text-[var(--color-red)]' : 'text-[var(--text-tertiary)]'
            )}
          >
            {error || helperText}
          </p>
        )}
      </div>
    );
  }
);

Textarea.displayName = 'Textarea';

export { Textarea, textareaVariants };

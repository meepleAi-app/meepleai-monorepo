import { forwardRef, useId, type ChangeEvent, type JSX, type ReactNode } from 'react';

import clsx from 'clsx';

export interface InputFieldProps {
  readonly label: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
  readonly type?: string;
  readonly placeholder?: string;
  readonly error?: string;
  readonly hint?: string;
  readonly right?: ReactNode;
  readonly autoComplete?: string;
  readonly name?: string;
  readonly id?: string;
  readonly disabled?: boolean;
  readonly required?: boolean;
  readonly className?: string;
}

export const InputField = forwardRef<HTMLInputElement, InputFieldProps>(function InputField(
  {
    label,
    value,
    onChange,
    type = 'text',
    placeholder,
    error,
    hint,
    right,
    autoComplete,
    name,
    id,
    disabled = false,
    required = false,
    className,
  },
  ref
): JSX.Element {
  const autoId = useId();
  const inputId = id ?? `if-${autoId}`;
  const errorId = `${inputId}-error`;
  const hintId = `${inputId}-hint`;
  const describedBy = error ? errorId : hint ? hintId : undefined;

  const handleChange = (e: ChangeEvent<HTMLInputElement>): void => {
    onChange(e.target.value);
  };

  return (
    <div className={clsx('flex flex-col gap-1.5', className)}>
      <label htmlFor={inputId} className="text-sm font-medium text-foreground font-body">
        {label}
      </label>
      <div className="relative">
        <input
          ref={ref}
          id={inputId}
          name={name}
          type={type}
          value={value}
          onChange={handleChange}
          placeholder={placeholder}
          autoComplete={autoComplete}
          disabled={disabled}
          required={required}
          aria-invalid={!!error}
          aria-describedby={describedBy}
          className={clsx(
            'w-full h-10 px-3 rounded-lg bg-background text-foreground',
            'border transition-colors font-body text-sm',
            'focus:outline-none focus:ring-2 focus:ring-offset-0',
            error
              ? 'border-[hsl(var(--c-danger))] focus:ring-[hsl(var(--c-danger)/0.3)]'
              : 'border-border focus:ring-[hsl(var(--c-toolkit)/0.3)] focus:border-[hsl(var(--c-toolkit))]',
            right && 'pr-10',
            disabled && 'opacity-50 cursor-not-allowed'
          )}
        />
        {right && <div className="absolute inset-y-0 right-0 flex items-center pr-2">{right}</div>}
      </div>
      {error ? (
        <p
          id={errorId}
          role="alert"
          aria-live="polite"
          className="text-xs text-[hsl(var(--c-danger))] font-body"
        >
          {error}
        </p>
      ) : hint ? (
        <p id={hintId} className="text-xs text-muted-foreground font-body">
          {hint}
        </p>
      ) : null}
    </div>
  );
});

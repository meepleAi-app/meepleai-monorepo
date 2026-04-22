import { forwardRef, useState, type JSX } from 'react';

import { Eye, EyeOff } from 'lucide-react';

import { InputField } from '../input-field';
import { StrengthMeter, type StrengthLabels } from '../strength-meter';

export interface PwdInputProps {
  readonly label: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
  readonly placeholder?: string;
  readonly error?: string;
  readonly hint?: string;
  readonly autoComplete?: string;
  readonly name?: string;
  readonly id?: string;
  readonly disabled?: boolean;
  readonly required?: boolean;
  readonly showStrength?: boolean;
  /** Labels forwarded to StrengthMeter when showStrength is true. */
  readonly strengthLabels?: StrengthLabels;
  /** Prefix forwarded to StrengthMeter when showStrength is true. */
  readonly strengthPrefix?: string;
  /** aria-label for the show/hide toggle button (e.g. "Show password"). */
  readonly toggleShowLabel?: string;
  /** aria-label for the toggle button when password is currently visible. */
  readonly toggleHideLabel?: string;
  readonly className?: string;
}

export const PwdInput = forwardRef<HTMLInputElement, PwdInputProps>(function PwdInput(
  {
    label,
    value,
    onChange,
    placeholder,
    error,
    hint,
    autoComplete,
    name,
    id,
    disabled = false,
    required = false,
    showStrength = false,
    strengthLabels,
    strengthPrefix,
    toggleShowLabel = 'Mostra password',
    toggleHideLabel = 'Nascondi password',
    className,
  },
  ref
): JSX.Element {
  const [show, setShow] = useState(false);

  const toggle = (
    <button
      type="button"
      onClick={() => setShow(s => !s)}
      aria-label={show ? toggleHideLabel : toggleShowLabel}
      aria-pressed={show}
      className="h-8 w-8 flex items-center justify-center rounded-md text-muted-foreground hover:text-foreground hover:bg-muted focus:outline-none focus:ring-2 focus:ring-[hsl(var(--c-toolkit)/0.3)]"
    >
      {show ? <EyeOff size={16} aria-hidden="true" /> : <Eye size={16} aria-hidden="true" />}
    </button>
  );

  return (
    <div className={className}>
      <InputField
        ref={ref}
        label={label}
        value={value}
        onChange={onChange}
        type={show ? 'text' : 'password'}
        placeholder={placeholder}
        error={error}
        hint={hint}
        autoComplete={autoComplete}
        name={name}
        id={id}
        disabled={disabled}
        required={required}
        right={toggle}
      />
      {showStrength && value.length > 0 && (
        <div className="mt-2">
          <StrengthMeter value={value} labels={strengthLabels} prefix={strengthPrefix} />
        </div>
      )}
    </div>
  );
});

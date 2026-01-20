/**
 * RadioGroup Component
 *
 * Radio button group for single selection from multiple options.
 * Lightweight implementation using native HTML radio inputs.
 */

'use client';

import * as React from 'react';

import { cn } from '@/lib/utils';

interface RadioGroupContextValue {
  value?: string;
  onValueChange?: (value: string) => void;
  name?: string;
}

const RadioGroupContext = React.createContext<RadioGroupContextValue>({});

interface RadioGroupProps extends React.HTMLAttributes<HTMLDivElement> {
  value?: string;
  onValueChange?: (value: string) => void;
  name?: string;
  disabled?: boolean;
}

const RadioGroup = React.forwardRef<HTMLDivElement, RadioGroupProps>(
  ({ className, value, onValueChange, name, children, disabled, ...props }, ref) => {
    const contextValue = React.useMemo(
      () => ({ value, onValueChange, name }),
      [value, onValueChange, name]
    );

    return (
      <RadioGroupContext.Provider value={contextValue}>
        <div
          ref={ref}
          className={cn('grid gap-2', className)}
          role="radiogroup"
          aria-disabled={disabled}
          {...props}
        >
          {children}
        </div>
      </RadioGroupContext.Provider>
    );
  }
);
RadioGroup.displayName = 'RadioGroup';

interface RadioGroupItemProps extends React.InputHTMLAttributes<HTMLInputElement> {
  value: string;
}

const RadioGroupItem = React.forwardRef<HTMLInputElement, RadioGroupItemProps>(
  ({ className, value: itemValue, disabled, ...props }, ref) => {
    const { value, onValueChange, name } = React.useContext(RadioGroupContext);
    const isChecked = value === itemValue;

    return (
      <input
        ref={ref}
        type="radio"
        className={cn(
          'aspect-square h-4 w-4 rounded-full border border-primary text-primary ring-offset-background focus:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50',
          className
        )}
        value={itemValue}
        checked={isChecked}
        onChange={(e) => onValueChange?.(e.target.value)}
        name={name}
        disabled={disabled}
        {...props}
      />
    );
  }
);
RadioGroupItem.displayName = 'RadioGroupItem';

export { RadioGroup, RadioGroupItem };

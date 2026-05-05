import type { JSX } from 'react';

export interface DividerProps {
  readonly label?: string;
  readonly className?: string;
}

export function Divider({ label, className }: DividerProps): JSX.Element {
  return (
    <div
      role="separator"
      aria-orientation="horizontal"
      className={`flex items-center gap-3 my-4 ${className ?? ''}`}
    >
      <div className="flex-1 h-px bg-border" />
      {label ? (
        <>
          <span
            aria-hidden="true"
            className="text-xs text-muted-foreground uppercase tracking-wider font-body"
          >
            {label}
          </span>
          <div className="flex-1 h-px bg-border" />
        </>
      ) : null}
    </div>
  );
}

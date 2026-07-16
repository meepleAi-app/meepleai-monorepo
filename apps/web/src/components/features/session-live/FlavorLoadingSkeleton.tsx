import { type ReactElement } from 'react';

export function FlavorLoadingSkeleton(): ReactElement {
  return (
    <div
      role="status"
      aria-live="polite"
      data-slot="flavor-loading-skeleton"
      className="flex flex-col gap-2 p-3 animate-pulse"
    >
      <div className="h-10 rounded-lg bg-muted/40" />
      <div className="h-6 rounded-md bg-muted/40" />
      <div className="h-6 rounded-md bg-muted/40" />
    </div>
  );
}

/**
 * DetailPageLayout — composer primitive for entity detail pages.
 *
 * Stage 3 cross-cutting (Issue #1026, umbrella #1023). Arranges hero,
 * connections, tabs, main content and footer ReactNode slots with WAI-ARIA
 * landmarks. The composer has no state and no business logic; callers pass
 * already-rendered children for each slot.
 */

import type { JSX, ReactNode } from 'react';

import clsx from 'clsx';

export interface DetailPageLayoutProps {
  /** Required slot rendered inside the page `<header>` (banner landmark). */
  readonly hero: ReactNode;
  /** Optional slot rendered inside an `<aside>` with accessible name "related entities". */
  readonly connections?: ReactNode;
  /** Optional slot rendered inside a `<nav>` with accessible name "detail sections". */
  readonly tabs?: ReactNode;
  /** Main content — tab panels or flat sections — rendered inside `<main>`. */
  readonly children: ReactNode;
  /** Optional slot rendered inside the page `<footer>` (contentinfo landmark). */
  readonly footer?: ReactNode;
  /** Optional passthrough class applied to the root wrapper. */
  readonly className?: string;
}

export function DetailPageLayout({
  hero,
  connections,
  tabs,
  children,
  footer,
  className,
}: DetailPageLayoutProps): JSX.Element {
  return (
    <div data-slot="detail-page-layout" className={clsx('flex flex-col gap-6', className)}>
      <header>{hero}</header>
      {connections !== undefined && (
        <aside aria-label="related entities">{connections}</aside>
      )}
      {tabs !== undefined && <nav aria-label="detail sections">{tabs}</nav>}
      <main>{children}</main>
      {footer !== undefined && <footer>{footer}</footer>}
    </div>
  );
}

/**
 * Showcase Layout — Dev-only guard
 *
 * This route group is only accessible in development mode.
 * In production, any request returns 404.
 */

import { notFound } from 'next/navigation';
import type { ReactNode } from 'react';

export default function ShowcaseRootLayout({ children }: { children: ReactNode }) {
  if (process.env.NODE_ENV !== 'development') {
    notFound();
  }

  return <>{children}</>;
}

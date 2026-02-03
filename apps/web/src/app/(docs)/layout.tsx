/**
 * Documentation Route Group Layout
 *
 * Applies a minimal layout for documentation pages:
 * - /rag (RAG Strategy Dashboard)
 * - Future: /api, /guides, etc.
 *
 * Features:
 * - Full-width content area for dashboards
 * - Dark mode support
 * - Minimal chrome for focus on content
 */

'use client';

import { ReactNode } from 'react';

import { cn } from '@/lib/utils';

interface DocsLayoutProps {
  children: ReactNode;
}

export default function DocsLayout({ children }: DocsLayoutProps) {
  return (
    <div className={cn(
      'min-h-screen bg-background',
      'selection:bg-primary/20 selection:text-primary'
    )}>
      {children}
    </div>
  );
}

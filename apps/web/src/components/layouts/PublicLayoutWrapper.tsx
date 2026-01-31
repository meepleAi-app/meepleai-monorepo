/**
 * PublicLayoutWrapper - Client Component Wrapper
 *
 * Wrapper client-side per PublicLayout.
 * Updated: Issue #3104 - Simplified since UnifiedHeader handles auth internally.
 */

'use client';

import { ReactNode } from 'react';

import { PublicLayout } from './PublicLayout';

export interface PublicLayoutWrapperProps {
  children: ReactNode;
  containerWidth?: 'sm' | 'md' | 'lg' | 'xl' | 'full';
  className?: string;
}

export function PublicLayoutWrapper({
  children,
  containerWidth,
  className,
}: PublicLayoutWrapperProps) {
  return (
    <PublicLayout containerWidth={containerWidth} className={className}>
      {children}
    </PublicLayout>
  );
}

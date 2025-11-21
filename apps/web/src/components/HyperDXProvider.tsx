'use client';

import { useEffect } from 'react';
import { initializeHyperDX } from '@/lib/hyperdx';

/**
 * HyperDX Provider Component
 *
 * Initializes HyperDX session replay and telemetry on the client side.
 * This component must be a Client Component ('use client') to use useEffect,
 * while the root layout remains a Server Component for optimal performance.
 *
 * @see docs/05-operations/migration/hyperdx-implementation-plan.md (P0-4 Fix)
 */
export function HyperDXProvider({ children }: { children: React.ReactNode }) {
  useEffect(() => {
    // Initialize HyperDX only on client side
    if (typeof window !== 'undefined') {
      initializeHyperDX();
    }
  }, []);

  // Pass through children without additional wrapper
  return <>{children}</>;
}

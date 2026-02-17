'use client';

/**
 * HyperDXProvider - Client-side telemetry provider for Next.js App Router
 *
 * Initializes HyperDX browser SDK for session replay and frontend telemetry.
 * This component is SSR-safe and designed for Next.js App Router architecture.
 *
 * Issue #1566: [P3] Implement HyperDX Browser SDK (Next.js)
 * Epic: #1561 - HyperDX Unified Observability Integration
 *
 * @see https://www.hyperdx.io/docs/install/browser
 */

import { ReactNode, useEffect, useState } from 'react';

interface HyperDXProviderProps {
  children: ReactNode;
}

export function HyperDXProvider({ children }: HyperDXProviderProps) {
  const [initialized, setInitialized] = useState(false);

  useEffect(() => {
    if (!initialized) {
      // Dynamically import to avoid Turbopack module factory issues
      import('@/lib/hyperdx').then(({ initializeHyperDX }) => {
        initializeHyperDX();
      });
      setInitialized(true);
    }
  }, [initialized]);

  return <>{children}</>;
}

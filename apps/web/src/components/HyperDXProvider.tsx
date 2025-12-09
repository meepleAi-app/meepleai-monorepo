'use client';

/**
 * HyperDXProvider - Client-side telemetry provider for Next.js App Router
 *
 * Initializes HyperDX browser SDK for session replay and frontend telemetry.
 * This component is SSR-safe and designed for Next.js App Router architecture.
 *
 * Issue #1566: [P3] ⚛️ Implement HyperDX Browser SDK (Next.js)
 * Epic: #1561 - HyperDX Unified Observability Integration
 *
 * Architecture:
 * - Wraps AppProviders in layout.tsx
 * - Initializes HyperDX client-side only (useEffect)
 * - Ensures early initialization before app components mount
 *
 * @see https://www.hyperdx.io/docs/install/browser
 */

import { ReactNode, useEffect, useState } from 'react';
import { initializeHyperDX } from '@/lib/hyperdx';

interface HyperDXProviderProps {
  children: ReactNode;
}

/**
 * HyperDX Provider Component
 *
 * Initializes the HyperDX browser SDK on the client side.
 * Safe for SSR - initialization only happens in the browser (useEffect).
 *
 * Usage:
 * ```tsx
 * // app/layout.tsx (Server Component)
 * export default function RootLayout({ children }) {
 *   return (
 *     <html>
 *       <body>
 *         <HyperDXProvider>
 *           <AppProviders>
 *             {children}
 *           </AppProviders>
 *         </HyperDXProvider>
 *       </body>
 *     </html>
 *   );
 * }
 * ```
 */
export function HyperDXProvider({ children }: HyperDXProviderProps) {
  const [initialized, setInitialized] = useState(false);

  useEffect(() => {
    // Initialize HyperDX once on client mount
    // useEffect ensures this only runs client-side, never during SSR
    if (!initialized) {
      initializeHyperDX();
      setInitialized(true);
    }
  }, [initialized]);

  // Render children immediately, initialization happens asynchronously
  // This prevents blocking the app while HyperDX loads
  return <>{children}</>;
}

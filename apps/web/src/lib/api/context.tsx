'use client';

/**
 * API Client Context - Issue #2372
 *
 * Provides a React context for accessing the API client throughout the component tree.
 * This enables components to access the API client without prop drilling.
 *
 * @example
 * ```tsx
 * // In a component
 * function MyComponent() {
 *   const { sharedGames, auth } = useApiClient();
 *   const games = await sharedGames.getAll();
 * }
 * ```
 */

import { createContext, useContext, useMemo, type ReactNode } from 'react';

import { api, createApiClient, type ApiClient, type ApiClientConfig } from './index';

// Create context with undefined as default (will be provided by provider)
const ApiClientContext = createContext<ApiClient | undefined>(undefined);

export interface ApiClientProviderProps {
  children: ReactNode;
  /**
   * Optional custom API client configuration.
   * If not provided, uses the default singleton api client.
   */
  config?: ApiClientConfig;
}

/**
 * Provider component that makes the API client available to all child components.
 *
 * @example
 * ```tsx
 * // In _app.tsx or layout.tsx
 * <ApiClientProvider>
 *   <App />
 * </ApiClientProvider>
 *
 * // With custom configuration
 * <ApiClientProvider config={{ baseUrl: 'https://api.example.com' }}>
 *   <App />
 * </ApiClientProvider>
 * ```
 */
export function ApiClientProvider({ children, config }: ApiClientProviderProps) {
  const client = useMemo(() => {
    // If custom config is provided, create a new client
    // Otherwise, use the default singleton
    return config ? createApiClient(config) : api;
  }, [config]);

  return <ApiClientContext.Provider value={client}>{children}</ApiClientContext.Provider>;
}

/**
 * Hook to access the API client from within components.
 *
 * @returns The API client with all feature clients (auth, games, sessions, etc.)
 * @throws Error if used outside of ApiClientProvider
 *
 * @example
 * ```tsx
 * function GamesList() {
 *   const { sharedGames } = useApiClient();
 *
 *   useEffect(() => {
 *     sharedGames.getAll().then(setGames);
 *   }, [sharedGames]);
 * }
 * ```
 */
export function useApiClient(): ApiClient {
  const context = useContext(ApiClientContext);

  if (context === undefined) {
    // Fallback to the default singleton if no provider is found
    // This maintains backward compatibility and simplifies usage
    return api;
  }

  return context;
}

// Re-export types for convenience
export type { ApiClient, ApiClientConfig };

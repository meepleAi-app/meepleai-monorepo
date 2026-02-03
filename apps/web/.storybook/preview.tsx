// eslint-disable-next-line import/order -- Storybook Preview type import (edge case)
import type { Preview } from '@storybook/react';

import React from 'react';

import { withThemeByClassName } from '@storybook/addon-themes';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { initialize, mswLoader } from 'msw-storybook-addon';
import { ThemeProvider } from 'next-themes';

import '../src/styles/globals.css'; // Import Tailwind CSS
import { AuthContext } from '../src/components/auth/AuthProvider';
import { TooltipProvider } from '../src/components/ui/overlays/tooltip';

// Initialize MSW for API mocking in stories
initialize();

// Create QueryClient for React Query
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: false,
      staleTime: Infinity,
    },
  },
});

// Mock AuthProvider for Storybook global context
// Uses real AuthContext to prevent "useAuth must be used within AuthProvider" errors
const MockAuthProvider = ({ children }: { children: React.ReactNode }) => {
  const mockAuthContext = {
    user: {
      id: 'storybook-user',
      email: 'test@example.com',
      displayName: 'Storybook User',
      role: 'user' as const,
    },
    loading: false,
    error: null,
    login: async () => ({ id: 'mock', email: 'mock', displayName: 'Mock', role: 'user' as const }),
    register: async () => ({
      id: 'mock',
      email: 'mock',
      displayName: 'Mock',
      role: 'user' as const,
    }),
    logout: async () => {},
    refreshUser: async () => {},
    clearError: () => {},
  };

  return React.createElement(AuthContext.Provider, {
    value: mockAuthContext,
    children,
  });
};

// Note: Next.js router mocking is now handled by @storybook/nextjs adapter
// Stories that need custom router behavior should use the nextjs.router parameter
// See: https://storybook.js.org/docs/react/configure/integration/nextjs

// Combined provider wrapper for all necessary context
const AllProviders = ({ children }: { children: React.ReactNode }) => {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider attribute="class" defaultTheme="light" enableSystem={false}>
        <TooltipProvider>
          <MockAuthProvider>{children}</MockAuthProvider>
        </TooltipProvider>
      </ThemeProvider>
    </QueryClientProvider>
  );
};

const preview: Preview = {
  loaders: [mswLoader],
  parameters: {
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
      expanded: true,
    },
    backgrounds: {
      disable: true, // Use next-themes instead
    },
    layout: 'centered',
    a11y: {
      config: {
        rules: [
          {
            // Disable color-contrast rule for now (Shadcn handles this)
            id: 'color-contrast',
            enabled: false,
          },
        ],
      },
    },
  },
  decorators: [
    withThemeByClassName({
      themes: {
        light: 'light',
        dark: 'dark',
      },
      defaultTheme: 'light',
    }),
    (Story) => (
      <AllProviders>
        <Story />
      </AllProviders>
    ),
  ],
};

export default preview;

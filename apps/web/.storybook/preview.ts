// eslint-disable-next-line import/order -- Storybook Preview type import (edge case)
import type { Preview } from '@storybook/react';

import React from 'react';

import { withThemeByClassName } from '@storybook/addon-themes';
import { INITIAL_VIEWPORTS, MINIMAL_VIEWPORTS } from 'storybook/viewport';

import '../src/styles/globals.css'; // Import Tailwind CSS
import { AuthContext } from '../src/components/auth/AuthProvider';

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

// MeepleAI custom viewports matching Tailwind breakpoints
const meepleViewports = {
  mobile: {
    name: 'Mobile',
    styles: { width: '375px', height: '667px' },
    type: 'mobile' as const,
  },
  mobileLarge: {
    name: 'Mobile Large',
    styles: { width: '414px', height: '896px' },
    type: 'mobile' as const,
  },
  tablet: {
    name: 'Tablet',
    styles: { width: '768px', height: '1024px' },
    type: 'tablet' as const,
  },
  laptop: {
    name: 'Laptop',
    styles: { width: '1024px', height: '768px' },
    type: 'desktop' as const,
  },
  desktop: {
    name: 'Desktop',
    styles: { width: '1280px', height: '800px' },
    type: 'desktop' as const,
  },
  desktopLarge: {
    name: 'Desktop Large',
    styles: { width: '1440px', height: '900px' },
    type: 'desktop' as const,
  },
};

const preview: Preview = {
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
    viewport: {
      viewports: {
        ...meepleViewports,
        ...MINIMAL_VIEWPORTS,
        ...INITIAL_VIEWPORTS,
      },
      defaultViewport: 'responsive',
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
    Story => React.createElement(MockAuthProvider, null, React.createElement(Story)),
  ],
};

export default preview;

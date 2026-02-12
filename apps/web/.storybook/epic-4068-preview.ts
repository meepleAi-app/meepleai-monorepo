/**
 * Storybook Preview Configuration for Epic #4068
 * Adds global decorators, parameters, and theme support for permission/tag/tooltip stories
 */

import type { Preview } from '@storybook/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PermissionProvider } from '../src/contexts/PermissionContext';
import { MOCK_USERS } from '../../examples/epic-4068/data/mock-data';

// Create query client for Storybook
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: false,
      staleTime: Infinity
    }
  }
});

// Mock permission data for stories
queryClient.setQueryData(['permissions', 'me'], MOCK_USERS.pro); // Default to Pro tier for stories

const preview: Preview = {
  parameters: {
    actions: { argTypesRegex: '^on[A-Z].*' },

    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/
      }
    },

    // Epic #4068: Add theme support
    backgrounds: {
      default: 'light',
      values: [
        {
          name: 'light',
          value: '#ffffff'
        },
        {
          name: 'dark',
          value: '#1a1a1a'
        },
        {
          name: 'card-background',
          value: '#f3f4f6'
        }
      ]
    },

    // Epic #4068: Viewport sizes for responsive testing
    viewport: {
      viewports: {
        mobile: {
          name: 'Mobile',
          styles: {
            width: '375px',
            height: '667px'
          }
        },
        tablet: {
          name: 'Tablet',
          styles: {
            width: '768px',
            height: '1024px'
          }
        },
        desktop: {
          name: 'Desktop',
          styles: {
            width: '1280px',
            height: '800px'
          }
        },
        wide: {
          name: 'Wide Desktop',
          styles: {
            width: '1920px',
            height: '1080px'
          }
        }
      }
    },

    // Epic #4068: Accessibility addon config
    a11y: {
      config: {
        rules: [
          {
            id: 'color-contrast',
            enabled: true
          },
          {
            id: 'label',
            enabled: true
          },
          {
            id: 'aria-required-attr',
            enabled: true
          }
        ]
      },
      manual: false
    }
  },

  // Global decorators (Epic #4068)
  decorators: [
    // Permission Provider wrapper
    (Story, context) => {
      // Allow stories to override default permission context
      const permissionOverride = context.parameters.permissions;

      if (permissionOverride) {
        queryClient.setQueryData(['permissions', 'me'], permissionOverride);
      }

      return (
        <QueryClientProvider client={queryClient}>
          <PermissionProvider>
            <Story />
          </PermissionProvider>
        </QueryClientProvider>
      );
    },

    // Card container decorator (for tag/tooltip visibility)
    (Story, context) => {
      // Only apply to component stories (not pages)
      if (context.parameters.layout === 'fullscreen') {
        return <Story />;
      }

      return (
        <div className="min-h-screen p-8 bg-background">
          <Story />
        </div>
      );
    }
  ],

  // Global types
  globalTypes: {
    // Epic #4068: Permission tier selector
    permissionTier: {
      name: 'Permission Tier',
      description: 'User tier for permission testing',
      defaultValue: 'pro',
      toolbar: {
        icon: 'user',
        items: [
          { value: 'free', title: 'Free Tier' },
          { value: 'normal', title: 'Normal Tier' },
          { value: 'pro', title: 'Pro Tier' },
          { value: 'enterprise', title: 'Enterprise Tier' }
        ],
        dynamicTitle: true
      }
    },

    // Epic #4068: Permission role selector
    permissionRole: {
      name: 'Permission Role',
      description: 'User role for permission testing',
      defaultValue: 'user',
      toolbar: {
        icon: 'shield',
        items: [
          { value: 'user', title: 'User' },
          { value: 'editor', title: 'Editor' },
          { value: 'creator', title: 'Creator' },
          { value: 'admin', title: 'Admin' }
        ]
      }
    }
  },

  // Tags for organizing stories
  tags: ['autodocs']
};

export default preview;

// ============================================================================
// Story Parameters
// ============================================================================

/**
 * Use in individual stories to customize permission context
 *
 * @example
 * ```typescript
 * export const FreeUserStory: Story = {
 *   args: { /* component props */ },
 *   parameters: {
 *     permissions: MOCK_USERS.free // Override to Free tier
 *   }
 * };
 * ```
 */

/**
 * Use for responsive testing
 *
 * @example
 * ```typescript
 * export const MobileStory: Story = {
 *   args: { /* component props */ },
 *   parameters: {
 *     viewport: { defaultViewport: 'mobile' }
 *   }
 * };
 * ```
 */

/**
 * Use for dark mode testing
 *
 * @example
 * ```typescript
 * export const DarkModeStory: Story = {
 *   args: { /* component props */ },
 *   parameters: {
 *     backgrounds: { default: 'dark' }
 *   },
 *   decorators: [(Story) => <div className="dark"><Story /></div>]
 * };
 * ```
 */

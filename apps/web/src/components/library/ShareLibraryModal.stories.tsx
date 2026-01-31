/**
 * ShareLibraryModal Storybook Stories (Issue #2852, Phase 4)
 *
 * Component-level stories for library sharing modal.
 * Tests privacy settings, link management, and revoke flow.
 */

import { within, userEvent } from '@storybook/test';
import { fn } from '@storybook/test';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { ShareLibraryModal } from './ShareLibraryModal';

import type { Meta, StoryObj } from '@storybook/react';

// Create QueryClient for React Query with mock data
const createMockQueryClient = (hasActiveLink = false) => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Infinity },
    },
  });

  // Mock share link query data
  if (hasActiveLink) {
    queryClient.setQueryData(['library', 'share-link'], {
      shareToken: 'abc123token',
      shareUrl: 'https://meepleai.app/shared/abc123token',
      privacyLevel: 'unlisted',
      includeNotes: false,
      expiresAt: null,
      viewCount: 12,
      lastAccessedAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(), // 2 hours ago
      isActive: true,
      createdAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(), // 7 days ago
    });
  }

  return queryClient;
};

const meta: Meta<typeof ShareLibraryModal> = {
  title: 'Components/Library/ShareLibraryModal',
  component: ShareLibraryModal,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 300,
    },
  },
  tags: ['autodocs'],
  argTypes: {
    isOpen: {
      control: 'boolean',
      description: 'Modal open state',
    },
    onClose: { action: 'closed' },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Default States (No Active Link)
// ============================================================================

export const NoActiveLink: Story = {
  args: {
    isOpen: true,
    onClose: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
};

export const PrivacyUnlisted: Story = {
  args: {
    isOpen: true,
    onClose: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
};

export const PrivacyPublic: Story = {
  args: {
    isOpen: true,
    onClose: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const publicRadio = canvas.getByLabelText('Pubblico');

    await userEvent.click(publicRadio);
  },
};

export const WithNotesEnabled: Story = {
  args: {
    isOpen: true,
    onClose: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const notesSwitch = canvas.getByRole('switch', { name: /Includi Note Personali/i });

    await userEvent.click(notesSwitch);
  },
};

// ============================================================================
// Active Link States
// ============================================================================

export const WithActiveLink: Story = {
  args: {
    isOpen: true,
    onClose: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
};

export const HoverCopyButton: Story = {
  args: {
    isOpen: true,
    onClose: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const copyButton = canvas.getByLabelText('Copia link');

    await userEvent.hover(copyButton);
  },
};

// ============================================================================
// Revoke Flow
// ============================================================================

export const RevokeButtonVisible: Story = {
  args: {
    isOpen: true,
    onClose: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
};

export const RevokeConfirmation: Story = {
  args: {
    isOpen: true,
    onClose: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const revokeButton = canvas.getByRole('button', { name: 'Revoca Link' });

    await userEvent.click(revokeButton);
  },
};

export const HoverRevokeConfirm: Story = {
  args: {
    isOpen: true,
    onClose: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const revokeButton = canvas.getByRole('button', { name: 'Revoca Link' });
    await userEvent.click(revokeButton);

    const confirmButton = canvas.getByRole('button', { name: /Conferma Revoca/i });
    await userEvent.hover(confirmButton);
  },
};

// ============================================================================
// Form Interactions
// ============================================================================

export const ExpirationDateFilled: Story = {
  args: {
    isOpen: true,
    onClose: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const dateInput = canvas.getByLabelText(/Data di Scadenza/i);

    await userEvent.type(dateInput, '2024-12-31T23:59');
  },
};

export const HoverCreateButton: Story = {
  args: {
    isOpen: true,
    onClose: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const createButton = canvas.getByRole('button', { name: /Crea Link/i });

    await userEvent.hover(createButton);
  },
};

// ============================================================================
// Loading States
// ============================================================================

export const LoadingLinkData: Story = {
  args: {
    isOpen: true,
    onClose: fn(),
  },
  decorators: [
    (Story) => {
      const loadingQueryClient = new QueryClient({
        defaultOptions: {
          queries: { retry: false, staleTime: Infinity },
        },
      });
      // Don't set any data to simulate loading state
      return (
        <QueryClientProvider client={loadingQueryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

// ============================================================================
// Responsive Layouts
// ============================================================================

export const Mobile: Story = {
  args: {
    isOpen: true,
    onClose: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

export const Tablet: Story = {
  args: {
    isOpen: true,
    onClose: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

export const Desktop: Story = {
  args: {
    isOpen: true,
    onClose: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1920],
    },
  },
};

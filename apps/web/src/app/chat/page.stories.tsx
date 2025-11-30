/**
 * Chat Page - Chromatic Visual Regression Stories
 *
 * Comprehensive visual testing for Chat Page (PAGE-004).
 * Tests sidebar, context chip, message list, and responsive states.
 *
 * @issue #1840 (PAGE-004)
 */

import type { Meta, StoryObj } from '@storybook/react';
// NOTE: @storybook/test incompatible with Storybook v10 (requires v8.6.14)
// Removed interactive tests temporarily until Storybook upgrade
import ChatPage from '@/components/pages/ChatPage';
import { Message } from '@/types';

// NOTE: AuthProvider is now provided globally via .storybook/preview.ts
const meta: Meta<typeof ChatPage> = {
  title: 'Pages/Chat Page',
  component: ChatPage,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1024, 1440],
      delay: 500, // Allow components to render
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof ChatPage>;

// Mock data generators
const generateMessages = (count: number): Message[] =>
  Array.from({ length: count }, (_, i) => ({
    id: `msg-${i}`,
    role: i % 2 === 0 ? 'user' : 'assistant',
    content:
      i % 2 === 0
        ? `User question ${i}: What are the setup rules?`
        : `AI answer ${i}: According to the rulebook, players should place resources...`,
    timestamp: new Date(Date.now() - (count - i) * 60000),
    confidence: i % 2 === 0 ? undefined : 0.85 + Math.random() * 0.15,
    citations:
      i % 2 === 0
        ? undefined
        : [
            {
              id: `cite-${i}`,
              documentId: `doc-${i}`,
              documentTitle: 'Catan Rulebook',
              pageNumber: 5 + i,
              relevanceScore: 0.9,
            },
          ],
  })) as Message[];

/**
 * 1. Default Empty State
 */
export const EmptyChat: Story = {
  name: '1. Empty Chat (No Messages)',
  parameters: {
    docs: {
      description: {
        story: 'Chat page with no selected game or messages. Shows empty state.',
      },
    },
  },
};

/**
 * 2. With Game Context
 */
export const WithContext: Story = {
  name: '2. With Game Context (Catan)',
  parameters: {
    docs: {
      description: {
        story:
          'Chat page with Catan game selected, showing context chip. (Interactive test removed - Storybook v10 incompatibility)',
      },
    },
  },
};

/**
 * 3. Few Messages (5)
 */
export const FewMessages: Story = {
  name: '3. Few Messages (5 Messages)',
  parameters: {
    docs: {
      description: {
        story: 'Chat with 5 messages showing typical conversation.',
      },
    },
    mockData: {
      messages: generateMessages(5),
    },
  },
};

/**
 * 4. Medium Conversation (20 messages)
 */
export const MediumConversation: Story = {
  name: '4. Medium Conversation (20 Messages)',
  parameters: {
    docs: {
      description: {
        story: 'Chat with 20 messages, scrollable content.',
      },
    },
    mockData: {
      messages: generateMessages(20),
    },
  },
};

/**
 * 5. Large Conversation (60 messages) - Virtualization
 */
export const LargeConversation: Story = {
  name: '5. Large Conversation (60 Messages - Virtualized)',
  parameters: {
    docs: {
      description: {
        story: 'Chat with 60 messages, virtualization active (threshold: 50).',
      },
    },
    mockData: {
      messages: generateMessages(60),
    },
  },
};

/**
 * 6. Streaming State
 */
export const StreamingMessage: Story = {
  name: '6. Streaming Message (SSE Active)',
  parameters: {
    docs: {
      description: {
        story: 'Chat with streaming message in progress (typing indicator).',
      },
    },
    mockData: {
      messages: generateMessages(5),
      isStreaming: true,
      streamingAnswer: 'The setup phase begins by placing...',
      streamingState: 'Searching knowledge base...',
    },
  },
};

/**
 * 7. Loading State
 */
export const LoadingMessages: Story = {
  name: '7. Loading Messages',
  parameters: {
    docs: {
      description: {
        story: 'Chat with loading skeleton while fetching messages.',
      },
    },
    mockData: {
      loading: {
        messages: true,
        creating: false,
        sending: false,
      },
    },
  },
};

/**
 * 8. Mobile View (375px)
 */
export const MobileView: Story = {
  name: '8. Mobile View (375px)',
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
    docs: {
      description: {
        story:
          'Chat page on mobile (375px) with swipeable sidebar. (Interactive test removed - Storybook v10 incompatibility)',
      },
    },
    mockData: {
      messages: generateMessages(10),
    },
  },
};

/**
 * 9. Tablet View (768px)
 */
export const TabletView: Story = {
  name: '9. Tablet View (768px)',
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
    docs: {
      description: {
        story: 'Chat page on tablet (768px) with sidebar visible.',
      },
    },
    mockData: {
      messages: generateMessages(10),
    },
  },
};

/**
 * 10. Desktop View (1440px)
 */
export const DesktopView: Story = {
  name: '10. Desktop View (1440px)',
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1440],
    },
    docs: {
      description: {
        story: 'Chat page on desktop (1440px) with full sidebar.',
      },
    },
    mockData: {
      messages: generateMessages(15),
    },
  },
};

/**
 * 11. Error State
 */
export const ErrorState: Story = {
  name: '11. Error State',
  parameters: {
    docs: {
      description: {
        story: 'Chat page showing error message.',
      },
    },
    mockData: {
      messages: generateMessages(5),
      errorMessage: 'Failed to load messages. Please try again.',
    },
  },
};

/**
 * 12. Sidebar Collapsed
 */
export const SidebarCollapsed: Story = {
  name: '12. Sidebar Collapsed (Desktop)',
  parameters: {
    docs: {
      description: {
        story:
          'Desktop view with sidebar collapsed for more chat space. (Interactive test removed - Storybook v10 incompatibility)',
      },
    },
    mockData: {
      messages: generateMessages(10),
      sidebarCollapsed: true,
    },
  },
};

/**
 * 13. Dark Theme (if supported)
 */
export const DarkTheme: Story = {
  name: '13. Dark Theme',
  parameters: {
    backgrounds: {
      default: 'dark',
    },
    docs: {
      description: {
        story: 'Chat page in dark mode (if theme support exists).',
      },
    },
    mockData: {
      messages: generateMessages(10),
    },
  },
};

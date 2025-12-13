/**
 * UserActivityTimeline Storybook Stories - Issue #911
 *
 * Comprehensive visual regression tests for UserActivityTimeline component.
 * Covers all states, variants, and edge cases.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { UserActivityTimeline } from './UserActivityTimeline';
import { UserActivityEvent } from './UserActivityItem';

const meta = {
  title: 'Components/Admin/UserActivityTimeline',
  component: UserActivityTimeline,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [375, 768, 1024],
      modes: {
        light: {},
        dark: {},
      },
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof UserActivityTimeline>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock data generators
const generateMockEvent = (
  id: number,
  overrides?: Partial<UserActivityEvent>
): UserActivityEvent => ({
  id: `event-${id}`,
  eventType: 'UserLogin',
  description: 'User logged in successfully',
  userId: `user-${id}`,
  userEmail: `user${id}@example.com`,
  timestamp: new Date(Date.now() - id * 60000).toISOString(),
  severity: 'Info',
  ...overrides,
});

const mockEventsSmall: UserActivityEvent[] = [
  generateMockEvent(1, {
    eventType: 'UserRegistered',
    description: 'New user registered',
    severity: 'Info',
  }),
  generateMockEvent(2, {
    eventType: 'PdfUploaded',
    description: 'PDF document uploaded',
    severity: 'Info',
    entityId: 'pdf-123',
    entityType: 'Document',
  }),
  generateMockEvent(3, {
    eventType: 'AlertCreated',
    description: 'System alert triggered',
    severity: 'Warning',
  }),
];

const mockEventsLarge: UserActivityEvent[] = Array.from({ length: 50 }, (_, i) =>
  generateMockEvent(i + 1, {
    eventType: ['UserLogin', 'PdfProcessed', 'GameAdded', 'ConfigurationChanged'][i % 4],
    severity: ['Info', 'Warning', 'Error', 'Critical'][i % 4] as UserActivityEvent['severity'],
    metadata:
      i % 3 === 0
        ? {
            ip: '192.168.1.1',
            browser: 'Chrome',
            platform: 'Windows',
          }
        : undefined,
  })
);

// 1. Default State
export const Default: Story = {
  args: {
    events: mockEventsSmall,
  },
};

// 2. Empty State
export const Empty: Story = {
  args: {
    events: [],
  },
};

// 3. Single Event
export const SingleEvent: Story = {
  args: {
    events: [mockEventsSmall[0]],
  },
};

// 4. Many Events (Pagination)
export const ManyEvents: Story = {
  args: {
    events: mockEventsLarge,
    pageSize: 10,
  },
};

// 5. Large Page Size
export const LargePageSize: Story = {
  args: {
    events: mockEventsLarge,
    pageSize: 50,
  },
};

// 6. With Filters Panel Open
export const WithFiltersOpen: Story = {
  args: {
    events: mockEventsLarge,
    showFilters: true,
  },
};

// 7. Without Filters
export const WithoutFilters: Story = {
  args: {
    events: mockEventsSmall,
    showFilters: false,
  },
};

// 8. All Severity Levels
export const AllSeverityLevels: Story = {
  args: {
    events: [
      generateMockEvent(1, { severity: 'Info', description: 'Info event' }),
      generateMockEvent(2, { severity: 'Warning', description: 'Warning event' }),
      generateMockEvent(3, { severity: 'Error', description: 'Error event' }),
      generateMockEvent(4, { severity: 'Critical', description: 'Critical event' }),
    ],
  },
};

// 9. Different Event Types
export const DifferentEventTypes: Story = {
  args: {
    events: [
      generateMockEvent(1, { eventType: 'UserRegistered', description: 'User registered' }),
      generateMockEvent(2, { eventType: 'UserLogin', description: 'User logged in' }),
      generateMockEvent(3, { eventType: 'PdfUploaded', description: 'PDF uploaded' }),
      generateMockEvent(4, { eventType: 'PdfProcessed', description: 'PDF processed' }),
      generateMockEvent(5, { eventType: 'AlertCreated', description: 'Alert created' }),
      generateMockEvent(6, { eventType: 'AlertResolved', description: 'Alert resolved' }),
      generateMockEvent(7, { eventType: 'GameAdded', description: 'Game added' }),
      generateMockEvent(8, {
        eventType: 'ConfigurationChanged',
        description: 'Config changed',
      }),
      generateMockEvent(9, { eventType: 'ErrorOccurred', description: 'Error occurred' }),
      generateMockEvent(10, { eventType: 'SystemEvent', description: 'System event' }),
    ],
  },
};

// 10. Events with Metadata
export const WithMetadata: Story = {
  args: {
    events: [
      generateMockEvent(1, {
        metadata: {
          ip: '192.168.1.100',
          browser: 'Chrome 119',
          platform: 'Windows 11',
          sessionId: 'sess-abc-123',
        },
      }),
      generateMockEvent(2, {
        metadata: {
          documentId: 'doc-456',
          fileSize: 1024000,
          pages: 42,
        },
      }),
    ],
  },
};

// 11. Events without User Email
export const WithoutUserEmail: Story = {
  args: {
    events: [generateMockEvent(1, { userEmail: null }), generateMockEvent(2, { userEmail: null })],
  },
};

// 12. Events with Long Descriptions
export const LongDescriptions: Story = {
  args: {
    events: [
      generateMockEvent(1, {
        description:
          'This is a very long description that should wrap properly and maintain readability even when the text extends beyond a single line in the timeline view',
        userEmail: 'very.long.email.address@example.domain.com',
      }),
    ],
  },
};

// 13. Recent Timestamps (Minutes Ago)
export const RecentTimestamps: Story = {
  args: {
    events: [
      generateMockEvent(1, {
        timestamp: new Date(Date.now() - 1 * 60000).toISOString(),
        description: '1 minute ago',
      }),
      generateMockEvent(2, {
        timestamp: new Date(Date.now() - 5 * 60000).toISOString(),
        description: '5 minutes ago',
      }),
      generateMockEvent(3, {
        timestamp: new Date(Date.now() - 30 * 60000).toISOString(),
        description: '30 minutes ago',
      }),
    ],
  },
};

// 14. Old Timestamps (Days/Weeks Ago)
export const OldTimestamps: Story = {
  args: {
    events: [
      generateMockEvent(1, {
        timestamp: new Date(Date.now() - 1 * 24 * 60 * 60000).toISOString(),
        description: '1 day ago',
      }),
      generateMockEvent(2, {
        timestamp: new Date(Date.now() - 7 * 24 * 60 * 60000).toISOString(),
        description: '1 week ago',
      }),
      generateMockEvent(3, {
        timestamp: new Date(Date.now() - 30 * 24 * 60 * 60000).toISOString(),
        description: '1 month ago',
      }),
    ],
  },
};

// 15. Mobile View (375px)
export const Mobile: Story = {
  args: {
    events: mockEventsSmall,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

// 16. Tablet View (768px)
export const Tablet: Story = {
  args: {
    events: mockEventsSmall,
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

// 17. Desktop View (1024px)
export const Desktop: Story = {
  args: {
    events: mockEventsSmall,
  },
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1024],
    },
  },
};

// 18. First Page
export const FirstPage: Story = {
  args: {
    events: mockEventsLarge,
    currentPage: 1,
    pageSize: 10,
  },
};

// 19. Middle Page
export const MiddlePage: Story = {
  args: {
    events: mockEventsLarge,
    currentPage: 3,
    pageSize: 10,
  },
};

// 20. Last Page
export const LastPage: Story = {
  args: {
    events: mockEventsLarge,
    currentPage: 5,
    pageSize: 10,
  },
};

// 21. Mixed Severities and Types
export const MixedSeveritiesAndTypes: Story = {
  args: {
    events: [
      generateMockEvent(1, {
        eventType: 'UserRegistered',
        severity: 'Info',
        description: 'New user registration',
      }),
      generateMockEvent(2, {
        eventType: 'PdfProcessed',
        severity: 'Warning',
        description: 'PDF processing completed with warnings',
      }),
      generateMockEvent(3, {
        eventType: 'ErrorOccurred',
        severity: 'Error',
        description: 'Database connection failed',
      }),
      generateMockEvent(4, {
        eventType: 'AlertCreated',
        severity: 'Critical',
        description: 'System critical alert',
      }),
      generateMockEvent(5, {
        eventType: 'GameAdded',
        severity: 'Info',
        description: 'New board game added to catalog',
      }),
    ],
  },
};

// 22. Edge Case: No Metadata
export const NoMetadata: Story = {
  args: {
    events: mockEventsSmall.map(e => ({ ...e, metadata: undefined })),
  },
};

// 23. Edge Case: All Fields Populated
export const AllFieldsPopulated: Story = {
  args: {
    events: [
      generateMockEvent(1, {
        eventType: 'PdfProcessed',
        description: 'PDF document processed successfully',
        userId: 'user-abc-123',
        userEmail: 'john.doe@example.com',
        entityId: 'pdf-doc-456',
        entityType: 'Document',
        severity: 'Info',
        metadata: {
          processingTime: '2.3s',
          pages: 15,
          fileSize: '2.4 MB',
          qualityScore: 0.95,
        },
      }),
    ],
  },
};

// 24. Loading State (Placeholder)
export const LoadingPlaceholder: Story = {
  args: {
    events: [],
  },
};

// 25. Error State (No filters applied, but still empty)
export const ErrorStateNoData: Story = {
  args: {
    events: [],
    showFilters: false,
  },
};

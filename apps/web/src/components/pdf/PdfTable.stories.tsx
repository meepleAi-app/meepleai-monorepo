import { PdfTable } from './PdfTable';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * PdfTable - Display uploaded PDFs with actions
 *
 * ## Features
 * - Responsive table with Shadcn/UI components
 * - Empty states, loading states, error states
 * - Language badges (en, it, de, fr, es)
 * - Status display (pending, completed, failed)
 * - Action buttons (view log, retry parsing)
 * - Accessible table markup with ARIA labels
 * - File size formatting
 * - Date formatting (locale-aware)
 *
 * ## Accessibility
 * - ✅ Semantic HTML table structure
 * - ✅ ARIA labels for status badges
 * - ✅ Keyboard-accessible action buttons
 * - ✅ Loading skeletons with proper roles
 * - ✅ Screen reader announcements for state changes
 */
const meta = {
  title: 'PDF/PdfTable',
  component: PdfTable,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Table component for displaying uploaded PDF documents with status tracking, language indicators, and action buttons for log viewing and retry operations.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    pdfs: {
      description: 'Array of PDF documents to display',
      control: 'object',
    },
    loading: {
      control: 'boolean',
      description: 'Show loading skeleton state',
    },
    error: {
      control: 'text',
      description: 'Error message to display',
    },
    retryingPdfId: {
      control: 'text',
      description: 'ID of PDF currently being retried',
    },
  },
} satisfies Meta<typeof PdfTable>;

export default meta;
type Story = StoryObj<typeof meta>;

// Fixed test data
const mockPdfs = [
  {
    id: '1',
    fileName: 'gloomhaven-rules.pdf',
    fileSizeBytes: 5242880, // 5 MB
    uploadedAt: '2024-01-15T10:00:00Z',
    uploadedByUserId: 'user-123',
    language: 'en',
    status: 'completed',
    logUrl: '/logs/1',
  },
  {
    id: '2',
    fileName: 'wingspan-expansion.pdf',
    fileSizeBytes: 3145728, // 3 MB
    uploadedAt: '2024-01-14T15:30:00Z',
    uploadedByUserId: 'user-123',
    language: 'it',
    status: 'pending',
    logUrl: null,
  },
  {
    id: '3',
    fileName: 'terraforming-mars-rules.pdf',
    fileSizeBytes: 7340032, // 7 MB
    uploadedAt: '2024-01-13T09:15:00Z',
    uploadedByUserId: 'user-123',
    language: 'de',
    status: 'failed',
    logUrl: '/logs/3',
  },
  {
    id: '4',
    fileName: 'spirit-island-guide.pdf',
    fileSizeBytes: 4194304, // 4 MB
    uploadedAt: '2024-01-12T14:20:00Z',
    uploadedByUserId: 'user-123',
    language: 'fr',
    status: 'completed',
    logUrl: '/logs/4',
  },
  {
    id: '5',
    fileName: 'scythe-quickstart.pdf',
    fileSizeBytes: 2097152, // 2 MB
    uploadedAt: '2024-01-11T11:45:00Z',
    uploadedByUserId: 'user-123',
    language: 'es',
    status: 'completed',
    logUrl: '/logs/5',
  },
];

/**
 * Default table state with multiple PDFs in various statuses.
 * Shows completed, pending, and failed uploads.
 */
export const Default: Story = {
  args: {
    pdfs: mockPdfs.slice(0, 3),
    onRetryParsing: () => {},
    onOpenLog: () => {},
  },
};

/**
 * Loading state showing skeleton placeholders.
 * Displayed while fetching PDF list from API.
 */
export const Loading: Story = {
  args: {
    pdfs: [],
    loading: true,
  },
};

/**
 * Error state when PDF list fails to load.
 * Shows error message to user.
 */
export const ErrorState: Story = {
  args: {
    pdfs: [],
    error: 'Failed to load PDF list. Please try again.',
  },
};

/**
 * Empty state when no PDFs have been uploaded.
 * Shows helpful message with icon.
 */
export const Empty: Story = {
  args: {
    pdfs: [],
  },
};

/**
 * Table with retry operation in progress.
 * Shows spinner on retry button for specific PDF.
 */
export const WithRetrying: Story = {
  args: {
    pdfs: mockPdfs.slice(0, 3),
    retryingPdfId: '3',
    onRetryParsing: () => {},
    onOpenLog: () => {},
  },
};

/**
 * All language badges displayed.
 * Shows EN, IT, DE, FR, ES language indicators.
 */
export const AllLanguages: Story = {
  args: {
    pdfs: mockPdfs,
    onRetryParsing: () => {},
    onOpenLog: () => {},
  },
};

/**
 * Table with all completed PDFs.
 * Success state with all green status badges.
 */
export const AllCompleted: Story = {
  args: {
    pdfs: mockPdfs.map(pdf => ({ ...pdf, status: 'completed' })),
    onRetryParsing: () => {},
    onOpenLog: () => {},
  },
};

/**
 * Table with all failed PDFs.
 * Shows multiple failures requiring attention.
 */
export const AllFailed: Story = {
  args: {
    pdfs: mockPdfs.map(pdf => ({ ...pdf, status: 'failed' })),
    onRetryParsing: () => {},
    onOpenLog: () => {},
  },
};

/**
 * Large file sizes displayed.
 * Tests file size formatting (MB, KB, Bytes).
 */
export const LargeFiles: Story = {
  args: {
    pdfs: [
      {
        ...mockPdfs[0],
        fileSizeBytes: 104857600, // 100 MB
        fileName: 'massive-rulebook.pdf',
      },
      {
        ...mockPdfs[1],
        fileSizeBytes: 52428800, // 50 MB
        fileName: 'large-expansion.pdf',
      },
      {
        ...mockPdfs[2],
        fileSizeBytes: 1024, // 1 KB
        fileName: 'tiny-errata.pdf',
      },
    ],
    onRetryParsing: () => {},
    onOpenLog: () => {},
  },
};

/**
 * Dark theme variant.
 * Shows table appearance on dark background.
 */
export const DarkTheme: Story = {
  args: {
    pdfs: mockPdfs.slice(0, 3),
    onRetryParsing: () => {},
    onOpenLog: () => {},
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-8 bg-background">
        <Story />
      </div>
    ),
  ],
};

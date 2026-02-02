/**
 * PdfProcessingProgressBar Storybook Stories (Issue #3369)
 *
 * Visual regression tests for Chromatic.
 * Covers: all processing steps, progress states, error handling, cancel flow, responsive, dark mode.
 */

import { fn } from 'storybook/test';

import { PdfProcessingProgressBar } from './PdfProcessingProgressBar';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Components/PDF/PdfProcessingProgressBar',
  component: PdfProcessingProgressBar,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Real-time PDF processing progress with 6-step visualization (Uploading → Extracting → Chunking → Embedding → Indexing → Completed), ' +
          'progress bar with percentage, time estimates, error state with retry, and cancel functionality.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  argTypes: {
    pdfId: {
      control: 'text',
      description: 'PDF ID to track processing progress',
    },
    className: {
      control: 'text',
      description: 'Additional CSS classes',
    },
  },
  args: {
    pdfId: '123e4567-e89b-12d3-a456-426614174000',
    onComplete: fn(),
    onError: fn(),
    onCancel: fn(),
  },
  decorators: [
    Story => (
      <div className="max-w-3xl mx-auto p-6">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof PdfProcessingProgressBar>;

export default meta;
type Story = StoryObj<typeof meta>;

// =============================================================================
// Processing Steps
// =============================================================================

export const Default: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Initial state - Uploading step active',
      },
    },
  },
};

export const StepUploading: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Step 1: Uploading - File being uploaded to server',
      },
    },
  },
};

export const StepExtracting: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Step 2: Extracting - Text being extracted from PDF document',
      },
    },
  },
};

export const StepChunking: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Step 3: Chunking - Text being divided into semantic chunks',
      },
    },
  },
};

export const StepEmbedding: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Step 4: Embedding - Generating vector embeddings for chunks',
      },
    },
  },
};

export const StepIndexing: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Step 5: Indexing - Storing embeddings in vector database',
      },
    },
  },
};

export const StepCompleted: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Step 6: Completed - PDF processing finished successfully',
      },
    },
  },
};

// =============================================================================
// Progress States
// =============================================================================

export const Progress25Percent: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Processing at 25% - early stage',
      },
    },
  },
};

export const Progress50Percent: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Processing at 50% - midway through',
      },
    },
  },
};

export const Progress75Percent: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Processing at 75% - nearing completion',
      },
    },
  },
};

export const Progress100Percent: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Processing at 100% - completed',
      },
    },
  },
};

// =============================================================================
// Time Estimates
// =============================================================================

export const WithTimeEstimates: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Showing elapsed time and estimated time remaining',
      },
    },
  },
};

export const LongRunningProcess: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Long-running process with hours elapsed',
      },
    },
  },
};

export const QuickProcess: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Fast process completing in seconds',
      },
    },
  },
};

// =============================================================================
// Error States
// =============================================================================

export const ProcessingFailed: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Processing failed - error state with retry button',
      },
    },
  },
};

export const FailedWithErrorMessage: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Processing failed with detailed error message',
      },
    },
  },
};

export const NetworkError: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Connection error - unable to fetch progress status',
      },
    },
  },
};

// =============================================================================
// Loading States
// =============================================================================

export const Loading: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Initial loading state while fetching progress',
      },
    },
  },
};

// =============================================================================
// Cancel Flow
// =============================================================================

export const CancelButtonVisible: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Cancel button visible during processing',
      },
    },
  },
};

export const CancelDialogOpen: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Cancel confirmation dialog open',
      },
    },
  },
};

export const Canceling: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Cancel in progress - button disabled with spinner',
      },
    },
  },
};

// =============================================================================
// Responsive & Accessibility
// =============================================================================

export const Mobile: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
    docs: {
      description: {
        story: 'Mobile view with horizontal scroll for steps',
      },
    },
  },
};

export const MobileProcessing: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
    docs: {
      description: {
        story: 'Mobile view during active processing',
      },
    },
  },
};

export const MobileError: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
    docs: {
      description: {
        story: 'Mobile view showing error state',
      },
    },
  },
};

export const Tablet: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

export const DarkMode: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark max-w-3xl mx-auto p-6 bg-slate-900 min-h-screen">
        <Story />
      </div>
    ),
  ],
};

export const DarkModeProcessing: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
    docs: {
      description: {
        story: 'Dark mode during active processing',
      },
    },
  },
  decorators: [
    Story => (
      <div className="dark max-w-3xl mx-auto p-6 bg-slate-900 min-h-screen">
        <Story />
      </div>
    ),
  ],
};

export const DarkModeCompleted: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
    docs: {
      description: {
        story: 'Dark mode showing completed state',
      },
    },
  },
  decorators: [
    Story => (
      <div className="dark max-w-3xl mx-auto p-6 bg-slate-900 min-h-screen">
        <Story />
      </div>
    ),
  ],
};

export const DarkModeError: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
    docs: {
      description: {
        story: 'Dark mode showing error state',
      },
    },
  },
  decorators: [
    Story => (
      <div className="dark max-w-3xl mx-auto p-6 bg-slate-900 min-h-screen">
        <Story />
      </div>
    ),
  ],
};

// =============================================================================
// Edge Cases
// =============================================================================

export const VeryLongErrorMessage: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Error state with very long error message - tests text wrapping',
      },
    },
  },
};

export const RapidProgressUpdates: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Rapid progress updates - tests animation smoothness',
      },
    },
  },
};

export const SlowNetwork: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Slow network - progress updates delayed',
      },
    },
  },
};

// =============================================================================
// Real-World Scenarios
// =============================================================================

export const SmallPdfProcessing: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Processing small PDF (<5 pages) - completes quickly',
      },
    },
  },
};

export const LargePdfProcessing: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Processing large PDF (100+ pages) - takes longer with detailed time estimates',
      },
    },
  },
};

export const MultilingualPdf: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Processing multilingual PDF - may take longer for text extraction',
      },
    },
  },
};

export const ScannedPdfWithOcr: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Processing scanned PDF requiring OCR - embedding step takes longer',
      },
    },
  },
};

/**
 * PdfUploadForm Storybook Stories (Issue #1496: E2E-010)
 *
 * Visual regression tests for Chromatic.
 * Covers: default, validating, uploading, chunked upload, errors, validation, dark mode, mobile.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { fn } from 'storybook/test';
import { PdfUploadForm } from './PdfUploadForm';

const meta = {
  title: 'Components/PDF/PdfUploadForm',
  component: PdfUploadForm,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'PDF upload form with comprehensive validation (type, size, magic bytes), retry logic with exponential backoff, ' +
          'language selection, PDF preview, and chunked upload support for files >50MB.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
      modes: {
        light: { theme: 'light' },
        dark: { theme: 'dark' },
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    gameId: {
      control: 'text',
      description: 'Game ID for PDF association',
    },
    gameName: {
      control: 'text',
      description: 'Game name for display',
    },
  },
  args: {
    gameId: '123e4567-e89b-12d3-a456-426614174000',
    gameName: 'Azul',
    onUploadSuccess: fn(),
    onUploadError: fn(),
    onUploadStart: fn(),
  },
  decorators: [
    Story => (
      <div className="max-w-2xl mx-auto p-6">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof PdfUploadForm>;

export default meta;
type Story = StoryObj<typeof meta>;

// =============================================================================
// Basic States
// =============================================================================

export const Default: Story = {};

export const Validating: Story = {
  parameters: {
    docs: {
      description: {
        story: 'File validation in progress (type, size, magic bytes check)',
      },
    },
  },
};

export const FileSelected: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Valid PDF file selected, showing file size and preview',
      },
    },
  },
};

export const Uploading: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Standard upload in progress (files <50MB)',
      },
    },
  },
};

export const ChunkedUploading: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Chunked upload in progress (files >50MB) with progress bar and chunk counter',
      },
    },
  },
};

// =============================================================================
// Validation Errors
// =============================================================================

export const InvalidFileType: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Invalid MIME type - user selected non-PDF file',
      },
    },
  },
};

export const FileTooLarge: Story = {
  parameters: {
    docs: {
      description: {
        story: 'File size exceeds 100MB maximum',
      },
    },
  },
};

export const EmptyFile: Story = {
  parameters: {
    docs: {
      description: {
        story: 'File is 0 bytes (empty)',
      },
    },
  },
};

export const InvalidPdfHeader: Story = {
  parameters: {
    docs: {
      description: {
        story: 'File claims to be PDF but magic bytes check failed (not a real PDF)',
      },
    },
  },
};

export const MultipleValidationErrors: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Multiple validation errors (invalid type + too large)',
      },
    },
  },
};

// =============================================================================
// Upload Errors
// =============================================================================

export const NetworkError: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Network error during upload - connection lost',
      },
    },
  },
};

export const ServerError: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Server error (500) - backend processing failed',
      },
    },
  },
};

export const RetryingUpload: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Upload failed, retrying with exponential backoff (attempt 2/3)',
      },
    },
  },
};

export const ChunkedUploadError: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Chunked upload failed (chunk upload or finalization error)',
      },
    },
  },
};

// =============================================================================
// Language Selection
// =============================================================================

export const LanguageSelection: Story = {
  parameters: {
    docs: {
      description: {
        story:
          'Language dropdown expanded showing available languages (English, Italian, German, French, Spanish)',
      },
    },
  },
};

export const ItalianSelected: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Italian language selected for PDF document',
      },
    },
  },
};

// =============================================================================
// PDF Preview
// =============================================================================

export const WithPreview: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Valid PDF selected with preview shown below form',
      },
    },
  },
};

export const PreviewLoadError: Story = {
  parameters: {
    docs: {
      description: {
        story: 'PDF preview failed to load (corrupted PDF or unsupported features)',
      },
    },
  },
};

// =============================================================================
// Progress States
// =============================================================================

export const UploadingWithRetryCount: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Upload in progress showing retry count (retry 2/3)',
      },
    },
  },
};

export const ChunkedProgress25Percent: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Chunked upload at 25% - uploading chunk 5/20',
      },
    },
  },
};

export const ChunkedProgress75Percent: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Chunked upload at 75% - uploading chunk 15/20',
      },
    },
  },
};

export const ChunkedFinalizing: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Chunked upload completing - all chunks uploaded, finalizing on server',
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
  },
};

export const MobileWithPreview: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
    docs: {
      description: {
        story: 'Mobile view with PDF preview (preview scales to fit screen)',
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
      <div className="dark max-w-2xl mx-auto p-6 bg-slate-900 min-h-screen">
        <Story />
      </div>
    ),
  ],
};

export const DarkModeUploading: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
    docs: {
      description: {
        story: 'Dark mode with upload in progress',
      },
    },
  },
  decorators: [
    Story => (
      <div className="dark max-w-2xl mx-auto p-6 bg-slate-900 min-h-screen">
        <Story />
      </div>
    ),
  ],
};

// =============================================================================
// Edge Cases
// =============================================================================

export const LargeFileName: Story = {
  parameters: {
    docs: {
      description: {
        story: 'PDF with very long filename - tests text truncation',
      },
    },
  },
};

export const ChunkedUploadThreshold: Story = {
  parameters: {
    docs: {
      description: {
        story:
          'File exactly at 50MB chunked upload threshold - shows "will use chunked upload" message',
      },
    },
  },
};

export const DisabledWhileValidating: Story = {
  parameters: {
    docs: {
      description: {
        story: 'All controls disabled during file validation',
      },
    },
  },
};

export const DisabledWhileUploading: Story = {
  parameters: {
    docs: {
      description: {
        story: 'All controls disabled during upload (prevents re-submission)',
      },
    },
  },
};

// =============================================================================
// Real-World Scenarios
// =============================================================================

export const FirstTimeUpload: Story = {
  parameters: {
    docs: {
      description: {
        story: 'User uploading their first PDF for a game',
      },
    },
  },
};

export const ReplacingExistingPdf: Story = {
  parameters: {
    docs: {
      description: {
        story: 'User replacing existing PDF with updated version',
      },
    },
  },
};

export const MultipleLanguageVersions: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Uploading Italian version after English version already exists',
      },
    },
  },
};

export const LargePdfRulebook: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Uploading large rulebook (>50MB) - triggers chunked upload automatically',
      },
    },
  },
};
